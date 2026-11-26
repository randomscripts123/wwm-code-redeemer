using System.Drawing;

namespace WwmRedeemer;

public enum Outcome { Success, Used, Expired, Invalid, Limit, Unknown }

/// <summary>
/// Chay macro. Hop nhap mo/dong duoc nhan biet bang do sang (moi ngon ngu game deu chay duoc);
/// chu OCR chi dung de doc thong bao loi (Anh + Viet), neu khong doc duoc thi xep "Khong ro".
/// </summary>
public class Redeemer
{
    private readonly AppConfig _cfg;
    private readonly Action<string> _log;
    private readonly Func<string, Task<bool>> _setClipboard;

    public Redeemer(AppConfig cfg, Action<string> log, Func<string, Task<bool>> setClipboard)
    {
        _cfg = cfg; _log = log; _setClipboard = setClipboard;
    }

    private static Point ToScreen(Rectangle r, Pt p) =>
        new(r.Left + (int)Math.Round(p.X * r.Width), r.Top + (int)Math.Round(p.Y * r.Height));

    // ------------------------------------------------------------------ hieu chinh vi tri nut

    /// <summary>Doi F9 (co huy). Tra ve ty le vi tri chuot trong vung client game.</summary>
    private async Task<Pt?> WaitF9Async(IntPtr hwnd, CancellationToken ct)
    {
        while (Native.IsDown(Native.VK_F9)) await Task.Delay(40, ct); // doi nha phim truoc
        while (!Native.IsDown(Native.VK_F9))
        {
            if (Native.IsDown(Native.VK_F12)) return null;
            await Task.Delay(40, ct);
        }
        var c = Native.CursorPos();
        var r = Native.ClientRectOnScreen(hwnd);
        await Task.Delay(300, ct);
        return new Pt(Math.Clamp((c.X - r.Left) / (double)r.Width, 0, 1), Math.Clamp((c.Y - r.Top) / (double)r.Height, 0, 1));
    }

    private async Task<Pt?> ManualAsync(IntPtr hwnd, string promptKey, string nameKey, CancellationToken ct)
    {
        _log(L.F(promptKey, L.Get(nameKey)));
        var p = await WaitF9Async(hwnd, ct);
        if (p != null) _log(L.F("log_f9_saved", L.Get(nameKey)));
        return p;
    }

    /// <summary>Bam nut Exchange Code roi cho hop mo (do bang do sang). Tra ve true neu hop da mo.</summary>
    private async Task<bool> OpenByClickAsync(Rectangle area, Pt btn, CancellationToken ct)
    {
        var p = ToScreen(area, btn);
        await Native.ClickAsync(p.X, p.Y);
        for (int i = 0; i < 14; i++)
        {
            await Task.Delay(150, ct);
            if (DialogDetector.IsOpenOnScreen(area)) return true;
        }
        return false;
    }

    /// <summary>
    /// Tim vi tri 3 nut. Thu tu: (1) nut Exchange Code: doc chu tieng Anh neu duoc, khong thi doan vi tri theo bo cuc;
    /// (2) mo hop roi lay o nhap + Confirm: doc chu tieng Anh neu duoc, khong thi tinh tu hinh dang hop.
    /// Cai nao that bai thi chon tay bang F9. Tra ve false neu huy.
    /// </summary>
    public async Task<bool> CalibrateAsync(IntPtr hwnd, CancellationToken ct)
    {
        Pt? btn = null, field = null, confirm = null;
        bool ocrOk = OcrService.Available;
        _log(L.Get("log_detect"));

        try
        {
            var area = Native.ClientRectOnScreen(hwnd);
            bool open = DialogDetector.IsOpenOnScreen(area);

            // (1) Nut Exchange Code
            if (ocrOk)
            {
                using var bmp = OcrService.Capture(area);
                var lines = await OcrService.ReadAsync(bmp);
                btn = Locator.FindExchangeButton(lines, area.Width, area.Height);
            }

            if (!open)
            {
                bool guessed = false;
                if (btn == null) { btn = Locator.GuessButton(); guessed = true; }
                if (await OpenByClickAsync(area, btn, ct)) open = true;
                else if (guessed) { btn = null; _log(L.F("log_detect_miss", L.Get("pt_button"))); }
                else btn = null;
            }

            if (btn == null)
            {
                btn = await ManualAsync(hwnd, "f9_prompt", "pt_button", ct);
                if (btn == null) { _log(L.Get("log_calib_cancel")); return false; }
                if (!open && await OpenByClickAsync(Native.ClientRectOnScreen(hwnd), btn, ct)) open = true;
            }

            // (2) O nhap + Confirm khi hop dang mo
            if (open)
            {
                using var bmp = OcrService.Capture(area);
                if (ocrOk)
                {
                    var lines = await OcrService.ReadAsync(bmp);
                    (field, confirm) = Locator.FindDialog(lines, area.Width, area.Height);
                }
                if (field == null || confirm == null)
                {
                    var ctl = DialogDetector.FindControls(bmp);
                    if (ctl != null) { field ??= ctl.Value.field; confirm ??= ctl.Value.confirm; }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log(L.F("log_error", ex.Message)); }

        if (btn != null) _log(L.F("log_detect_ok", L.Get("pt_button")));
        if (field != null) _log(L.F("log_detect_ok", L.Get("pt_field")));
        if (confirm != null) _log(L.F("log_detect_ok", L.Get("pt_confirm")));

        if (field == null)
        {
            _log(L.F("log_detect_miss", L.Get("pt_field")));
            field = await ManualAsync(hwnd, "f9_prompt_field", "pt_field", ct);
            if (field == null) { _log(L.Get("log_calib_cancel")); return false; }
        }
        if (confirm == null)
        {
            _log(L.F("log_detect_miss", L.Get("pt_confirm")));
            confirm = await ManualAsync(hwnd, "f9_prompt_confirm", "pt_confirm", ct);
            if (confirm == null) { _log(L.Get("log_calib_cancel")); return false; }
        }

        _cfg.Button = btn; _cfg.Field = field; _cfg.Confirm = confirm;
        _cfg.Save();
        _log(L.Get("log_calib_done"));
        return true;
    }

    // ------------------------------------------------------------------ phan loai thong bao

    // Tu khoa da bo dau (xem Locator.Norm). Gom ca tieng Anh va tieng Viet; OCR tieng Anh doc tieng Viet bi nhieu nen chon cum ngan, dac trung.
    private static readonly string[] UsedKeys    = { "alreadyused", "alreadyredeemed", "sametype", "loai", "sudung" };          // "...da su dung ma doi thuong cung loai"
    private static readonly string[] ExpiredKeys = { "expired", "hethan", "dahet" };                                              // "Ma doi thuong da het han"
    private static readonly string[] LimitKeys   = { "toomany", "frequent", "tryagainlater", "quanhieu", "quanhanh", "gioihan", "thulai" };
    private static readonly string[] InvalidKeys = { "invalid", "incorrect", "notexist", "hople", "honghop", "khonghop", "khongton" }; // "Ma doi thuong khong hop le"

    /// <summary>
    /// Phan loai thong bao noi (nam o phan tren giua man hinh) tu chu OCR. w,h la kich thuoc anh da OCR.
    /// Chi xet vung thong bao nen khong bi chu tinh cua trang Settings gay nhieu.
    /// </summary>
    public static (Outcome outcome, string? message) Classify(List<OcrLine> lines, int w, int h)
    {
        var toast = lines.Where(l => l.Cy / h is > 0.06 and < 0.30 && l.Cx / w is > 0.26 and < 0.74 && l.Text.Trim().Length > 3)
                         .Select(l => (text: l.Text.Trim(), norm: Locator.Norm(l.Text))).ToList();

        foreach (var t in toast) if (UsedKeys.Any(t.norm.Contains)) return (Outcome.Used, t.text);
        foreach (var t in toast) if (ExpiredKeys.Any(t.norm.Contains)) return (Outcome.Expired, t.text);
        foreach (var t in toast) if (LimitKeys.Any(t.norm.Contains)) return (Outcome.Limit, t.text);
        foreach (var t in toast) if (InvalidKeys.Any(t.norm.Contains)) return (Outcome.Invalid, t.text);

        return (Outcome.Unknown, toast.Count > 0 ? string.Join(" | ", toast.Select(t => t.text).Take(2)) : null);
    }

    private async Task<(Outcome outcome, string? message)> ReadToastAsync(Rectangle area)
    {
        if (!OcrService.Available) return (Outcome.Unknown, null);
        try
        {
            var (lines, scale) = await OcrService.ReadAreaAsync(area);
            return Classify(lines, (int)(area.Width * scale), (int)(area.Height * scale));
        }
        catch { return (Outcome.Unknown, null); }
    }

    // ------------------------------------------------------------------ cac buoc nhap ma

    /// <summary>Dam bao hop nhap dang mo: neu chua mo thi bam nut Exchange Code roi cho (toi da 3 lan).</summary>
    private async Task<bool> EnsureOpenAsync(Rectangle area, Point btn, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (DialogDetector.IsOpenOnScreen(area)) return true;
            await Native.ClickAsync(btn.X, btn.Y);
            for (int i = 0; i < 12; i++)
            {
                await Task.Delay(150, ct);
                if (DialogDetector.IsOpenOnScreen(area)) return true;
            }
        }
        return false;
    }

    private static async Task<bool> WaitClosedAsync(Rectangle area, int maxMs, CancellationToken ct)
    {
        for (int t = 0; t < maxMs; t += 100)
        {
            if (!DialogDetector.IsOpenOnScreen(area)) return true;
            await Task.Delay(100, ct);
        }
        return !DialogDetector.IsOpenOnScreen(area);
    }

    /// <summary>Dong hop nhap bang Esc (nhu thao tac tay); neu van mo thi bam nut Huy/Cancel.</summary>
    private async Task<bool> CloseDialogAsync(Rectangle area, Pt confirm, CancellationToken ct)
    {
        await Native.TapAsync(Native.ScanEsc);
        if (await WaitClosedAsync(area, 700, ct)) return true;

        // Nut Huy/Cancel nam cung hang voi Confirm, lech trai ~8.6% chieu rong
        var c = ToScreen(area, new Pt(Math.Max(0.05, confirm.X - 0.086), confirm.Y));
        await Native.ClickAsync(c.X, c.Y);
        if (await WaitClosedAsync(area, 700, ct)) { _log(L.Get("log_closed_cancel")); return true; }
        return false;
    }

    /// <summary>Nhap lan luot cac ma. Tra ve so ma thanh cong.</summary>
    public async Task<int> RunAsync(IReadOnlyList<string> codes, Action<string, string> onStatus, Action<int, int> onProgress, CancellationToken ct)
    {
        var hwnd = Native.FindGameWindow();
        if (hwnd == IntPtr.Zero) { _log(L.Get("log_no_game")); return 0; }

        _log(L.Get("log_foreground"));
        Native.BringToFront(hwnd);
        await Task.Delay(600, ct);

        if (!_cfg.HasPositions)
        {
            if (!await CalibrateAsync(hwnd, ct)) return 0;
            Native.BringToFront(hwnd);
            await Task.Delay(500, ct);
        }
        else _log(L.Get("log_saved_pos"));

        if (!OcrService.Available) _log(L.Get("log_blind"));

        _log(L.F("log_running", codes.Count));
        int sent = 0;

        for (int i = 0; i < codes.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            hwnd = Native.FindGameWindow();
            if (hwnd == IntPtr.Zero) { _log(L.Get("log_game_lost")); break; }
            if (!Native.IsForeground(hwnd)) { Native.BringToFront(hwnd); await Task.Delay(400, ct); }

            var code = codes[i];
            _log(L.F("log_code", i + 1, codes.Count, code));
            onStatus(code, "st_running");

            if (!await _setClipboard(code)) { _log(L.F("log_error", "clipboard")); onStatus(code, "st_pending"); break; }

            var area = Native.ClientRectOnScreen(hwnd);
            var btn = ToScreen(area, _cfg.Button!);
            var field = ToScreen(area, _cfg.Field!);
            var confirm = ToScreen(area, _cfg.Confirm!);

            // 1) Mo hop nhap
            if (!await EnsureOpenAsync(area, btn, ct))
            {
                _log(L.Get("log_open_fail"));
                onStatus(code, "st_pending");
                break;
            }

            // 2) Chon o nhap, xoa chu cu, dan ma
            await Native.ClickAsync(field.X, field.Y); await Task.Delay(300, ct);
            await Native.SelectAllAsync();             await Task.Delay(120, ct);
            await Native.CtrlVAsync();                 await Task.Delay(350, ct);

            // 3) Confirm
            await Native.ClickAsync(confirm.X, confirm.Y);

            // 4) Hop tu dong dong => thanh cong. Neu van mo => doc thong bao loi.
            Outcome outcome; string? msg = null;
            if (await WaitClosedAsync(area, 900, ct)) outcome = Outcome.Success;
            else
            {
                (outcome, msg) = await ReadToastAsync(area);
                if (outcome == Outcome.Unknown)
                {
                    // Co the game tra loi cham: cho them roi kiem tra lai
                    if (await WaitClosedAsync(area, 500, ct)) outcome = Outcome.Success;
                    else (outcome, msg) = await ReadToastAsync(area);
                }
            }

            bool stop = false;
            string status;
            switch (outcome)
            {
                case Outcome.Success:
                    _log(L.Get("log_res_ok")); status = "st_sent"; sent++; break;
                case Outcome.Used:
                    _log(L.F("log_game", msg ?? "")); _log(L.Get("log_res_used")); status = "st_used"; break;
                case Outcome.Expired:
                    _log(L.F("log_game", msg ?? "")); _log(L.Get("log_res_expired")); status = "st_expired"; break;
                case Outcome.Invalid:
                    _log(L.F("log_game", msg ?? "")); _log(L.Get("log_res_invalid")); status = "st_invalid"; break;
                case Outcome.Limit:
                    _log(L.F("log_game", msg ?? "")); _log(L.Get("log_res_limit")); status = "st_pending"; stop = true; break;
                default:
                    if (!string.IsNullOrEmpty(msg)) _log(L.F("log_game", msg));
                    _log(L.Get("log_res_unknown")); status = "st_failed"; break;
            }
            onStatus(code, status);
            onProgress(i + 1, codes.Count);

            // 5) Hop van mo (bao loi) => dong lai de ma sau khong bi dan noi vao
            if (outcome != Outcome.Success && DialogDetector.IsOpenOnScreen(area))
            {
                if (!await CloseDialogAsync(area, _cfg.Confirm!, ct))
                {
                    _log(L.Get("log_close_fail"));
                    break;
                }
            }
            if (stop) break;
        }
        return sent;
    }
}
