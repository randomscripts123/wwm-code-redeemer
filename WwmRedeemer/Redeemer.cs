using System.Drawing;

namespace WwmRedeemer;

/// <summary>Chay macro: nhan dien/hieu chinh nut, sau do nhap lan luot tung ma.</summary>
public class Redeemer
{
    private const int AfterConfirmMs = 2500; // cho sau khi bam Confirm
    private const int ClickDelayMs = 700;

    private readonly AppConfig _cfg;
    private readonly Action<string> _log;
    private readonly Func<string, Task<bool>> _setClipboard;

    public Redeemer(AppConfig cfg, Action<string> log, Func<string, Task<bool>> setClipboard)
    {
        _cfg = cfg; _log = log; _setClipboard = setClipboard;
    }

    // ------------------------------------------------------------------ vi tri nut

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

    /// <summary>Tim vi tri 3 nut: OCR truoc, nut nao khong thay thi chon tay bang F9. Tra ve false neu huy.</summary>
    public async Task<bool> CalibrateAsync(IntPtr hwnd, CancellationToken ct)
    {
        Pt? btn = null, field = null, confirm = null;
        bool ocrOk = OcrService.Available;

        if (!ocrOk) _log(L.Get("log_ocr_unavail"));
        else
        {
            _log(L.Get("log_detect"));
            try
            {
                var area = Native.ClientRectOnScreen(hwnd);

                // 1) Trang Settings: tim nut Exchange Code
                using (var bmp = OcrService.Capture(area))
                {
                    var lines = await OcrService.ReadAsync(bmp);
                    if (Locator.DialogOpen(lines))
                    {
                        // Hop da mo san: doc luon o nhap + Confirm (nut >> neu van thay phia sau lop toi)
                        (field, confirm) = Locator.FindDialog(lines, area.Width, area.Height);
                        btn = Locator.FindExchangeButton(lines, area.Width, area.Height);
                    }
                    else
                    {
                        btn = Locator.FindExchangeButton(lines, area.Width, area.Height);
                    }
                }

                // 2) Co nut -> bam thu de mo hop roi doc o nhap + Confirm
                if (btn != null)
                {
                    await Native.ClickAsync(area.Left + (int)(btn.X * area.Width), area.Top + (int)(btn.Y * area.Height));
                    await Task.Delay(1200, ct);
                    using var bmp2 = OcrService.Capture(area);
                    var lines2 = await OcrService.ReadAsync(bmp2);
                    (field, confirm) = Locator.FindDialog(lines2, area.Width, area.Height);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log(L.F("log_error", ex.Message)); }
        }

        // Bao ket qua + bu bang F9
        if (btn != null) _log(L.F("log_detect_ok", L.Get("pt_button")));
        if (field != null) _log(L.F("log_detect_ok", L.Get("pt_field")));
        if (confirm != null) _log(L.F("log_detect_ok", L.Get("pt_confirm")));

        if (btn == null)
        {
            if (ocrOk) _log(L.F("log_detect_miss", L.Get("pt_button")));
            btn = await ManualAsync(hwnd, "f9_prompt", "pt_button", ct);
            if (btn == null) { _log(L.Get("log_calib_cancel")); return false; }
        }
        if (field == null)
        {
            if (ocrOk) _log(L.F("log_detect_miss", L.Get("pt_field")));
            field = await ManualAsync(hwnd, "f9_prompt_field", "pt_field", ct);
            if (field == null) { _log(L.Get("log_calib_cancel")); return false; }
        }
        if (confirm == null)
        {
            if (ocrOk) _log(L.F("log_detect_miss", L.Get("pt_confirm")));
            confirm = await ManualAsync(hwnd, "f9_prompt_confirm", "pt_confirm", ct);
            if (confirm == null) { _log(L.Get("log_calib_cancel")); return false; }
        }

        _cfg.Button = btn; _cfg.Field = field; _cfg.Confirm = confirm;
        _cfg.Save();
        _log(L.Get("log_calib_done"));
        return true;
    }

    // ------------------------------------------------------------------ nhap ma

    private static Point ToScreen(Rectangle r, Pt p) =>
        new(r.Left + (int)Math.Round(p.X * r.Width), r.Top + (int)Math.Round(p.Y * r.Height));

    /// <summary>Nhap lan luot cac ma. Tra ve so ma da gui.</summary>
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

            if (!await _setClipboard(code)) { _log(L.F("log_error", "clipboard")); break; }

            var area = Native.ClientRectOnScreen(hwnd);
            var btn = ToScreen(area, _cfg.Button!);
            var field = ToScreen(area, _cfg.Field!);
            var confirm = ToScreen(area, _cfg.Confirm!);

            await Native.ClickAsync(btn.X, btn.Y);        await Task.Delay(ClickDelayMs, ct);  // mo hop nhap
            await Native.ClickAsync(field.X, field.Y);    await Task.Delay(400, ct);           // chon o nhap
            await Native.CtrlVAsync();                    await Task.Delay(400, ct);           // dan ma
            await Native.ClickAsync(confirm.X, confirm.Y);                                      // Confirm
            await Task.Delay(AfterConfirmMs, ct);

            await LogGameMessageAsync(area);

            sent++;
            onStatus(code, "st_sent");
            onProgress(i + 1, codes.Count);
        }
        return sent;
    }

    /// <summary>Doc nhanh thong bao hien tren man hinh sau khi Confirm de ghi vao log.</summary>
    private async Task LogGameMessageAsync(Rectangle area)
    {
        if (!OcrService.Available) return;
        try
        {
            using var bmp = OcrService.Capture(area);
            var lines = await OcrService.ReadAsync(bmp);
            string[] keys = { "success", "redeem", "invalid", "expired", "used", "claimed", "incorrect", "already", "mail", "error", "limit" };
            foreach (var l in lines)
            {
                var t = l.Text.ToLowerInvariant();
                if (l.Text.Length > 6 && keys.Any(k => t.Contains(k)) && !t.Contains("exchange code for rewards"))
                    _log(L.F("log_game", l.Text.Trim()));
            }
        }
        catch { /* khong anh huong luong chinh */ }
    }
}
