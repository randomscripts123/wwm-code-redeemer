using System.Drawing;
using System.Drawing.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace WwmRedeemer;

public record OcrWord(string Text, double X, double Y, double W, double H)
{
    public double Cx => X + W / 2;
    public double Cy => Y + H / 2;
}

public record OcrLine(string Text, double X, double Y, double W, double H, List<OcrWord> Words)
{
    public double Cx => X + W / 2;
    public double Cy => Y + H / 2;
}

/// <summary>Doc chu tren anh bang Windows OCR co san (khong can cai them).</summary>
public static class OcrService
{
    private static OcrEngine? _engine;

    private static OcrEngine? Engine =>
        _engine ??= OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))
                    ?? OcrEngine.TryCreateFromUserProfileLanguages();

    public static bool Available => Engine != null;

    public static Bitmap Capture(Rectangle area)
    {
        var bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size);
        return bmp;
    }

    /// <summary>Chup vung va OCR nhanh (thu nho neu rong qua maxWidth). Toa do dong tra ve nam trong anh da thu nho: chia cho scale de ra pixel that.</summary>
    public static async Task<(List<OcrLine> lines, double scale)> ReadAreaAsync(Rectangle area, int maxWidth = 1600)
    {
        using var src = Capture(area);
        if (src.Width <= maxWidth) return (await ReadAsync(src), 1.0);
        double scale = maxWidth / (double)src.Width;
        using var small = new Bitmap(src, (int)(src.Width * scale), (int)(src.Height * scale));
        return (await ReadAsync(small), scale);
    }

    public static async Task<List<OcrLine>> ReadAsync(Bitmap bmp)
    {
        var engine = Engine ?? throw new InvalidOperationException("Windows OCR engine unavailable");

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        using var ras = ms.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(ras);
        using var sb = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(sb);

        var lines = new List<OcrLine>();
        foreach (var line in result.Lines)
        {
            var words = line.Words
                .Select(w => new OcrWord(w.Text, w.BoundingRect.X, w.BoundingRect.Y, w.BoundingRect.Width, w.BoundingRect.Height))
                .ToList();
            if (words.Count == 0) continue;
            double x = words.Min(w => w.X), y = words.Min(w => w.Y);
            double r = words.Max(w => w.X + w.W), b = words.Max(w => w.Y + w.H);
            lines.Add(new OcrLine(line.Text, x, y, r - x, b - y, words));
        }
        return lines;
    }
}

/// <summary>Tim vi tri 3 nut dua tren chu OCR doc duoc. Ket qua la ty le (0..1) trong vung client cua game.</summary>
public static class Locator
{
    /// <summary>Chuan hoa de so khop: bo dau (a, o, u, d...), chi giu chu cai thuong. Giup khop ca khi OCR doc tieng Viet bi nhieu dau.</summary>
    public static string Norm(string s)
    {
        var d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var c in d)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (c == 'đ' || c == 'Đ') { sb.Append('d'); continue; }
            if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Vi tri uoc luong nut &gt;&gt; o dong dau tien cua muc tai khoan (khong phu thuoc ngon ngu).</summary>
    public static Pt GuessButton() => new(0.543, 0.243);

    /// <summary>Trang Settings &gt; Other: tim dong "Exchange Code" va uoc luong vi tri nut &gt;&gt; o cuoi dong.</summary>
    public static Pt? FindExchangeButton(List<OcrLine> lines, int w, int h)
    {
        // Chi nhan dong o nua trai (bo qua tieu de "Exchange Code" o panel ben phai)
        var row = lines.Where(l => Norm(l.Text).Contains("exchangecode") && l.X < 0.3 * w)
                       .OrderBy(l => l.X).FirstOrDefault();
        if (row == null) return null;
        // Nut >> nam cach chu "Exchange Code" khoang 47% chieu rong man hinh (giao dien 16:9)
        double x = Math.Min(row.X + 0.47 * w, w - 4);
        return new Pt(x / w, row.Cy / h);
    }

    /// <summary>Hop "Exchange Rewards": tim o nhap (duoi tieu de) va nut Confirm.</summary>
    public static (Pt? field, Pt? confirm) FindDialog(List<OcrLine> lines, int w, int h)
    {
        var title = lines.FirstOrDefault(l => Norm(l.Text).Contains("exchangerewards"));
        if (title == null) return (null, null);

        var field = new Pt(title.Cx / w, (title.Cy + 0.0757 * h) / h);

        // Nut Confirm: tu "Confirm" nam trong thanh duoi hop (lay tu o thap nhat, nam duoi tieu de)
        var confirmWord = lines.SelectMany(l => l.Words)
            .Where(x => Norm(x.Text).Contains("confirm") && x.Cy > title.Cy)
            .OrderByDescending(x => x.Cy)
            .FirstOrDefault();

        Pt confirm = confirmWord != null
            ? new Pt(confirmWord.Cx / w, confirmWord.Cy / h)
            : new Pt((title.Cx + 0.057 * w) / w, (title.Cy + 0.22 * h) / h); // uoc luong

        return (field, confirm);
    }

    public static bool DialogOpen(List<OcrLine> lines) =>
        lines.Any(l => Norm(l.Text).Contains("exchangerewards"));
}

/// <summary>
/// Nhan biet hop "Exchange Rewards" dang mo hay dong bang do sang, khong doc chu nen dung cho moi ngon ngu.
/// Khi hop mo, game phu mot dai toi len giua man hinh (ke ca le trai); khi dong thi le trai la nen sang cua trang Settings.
/// </summary>
public static class DialogDetector
{
    public const double Threshold = 110;               // do sang trung binh < nguong => hop dang mo
    private const double X0 = 0.02, X1 = 0.05, Y0 = 0.36, Y1 = 0.60;

    private static double Lum(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;

    private static double MeanLum(Bitmap b, Rectangle r)
    {
        double sum = 0; int n = 0;
        int sx = Math.Max(1, r.Width / 6), sy = Math.Max(1, r.Height / 12);
        for (int y = r.Top; y < r.Bottom; y += sy)
            for (int x = r.Left; x < r.Right; x += sx) { sum += Lum(b.GetPixel(x, y)); n++; }
        return n == 0 ? 255 : sum / n;
    }

    /// <summary>Do sang vung kiem tra tren anh day du cua vung game (dung cho test).</summary>
    public static double ProbeLum(Bitmap full)
    {
        var r = new Rectangle((int)(X0 * full.Width), (int)(Y0 * full.Height),
            Math.Max(6, (int)((X1 - X0) * full.Width)), Math.Max(12, (int)((Y1 - Y0) * full.Height)));
        return MeanLum(full, r);
    }

    public static bool IsOpen(Bitmap full) => ProbeLum(full) < Threshold;

    /// <summary>Chi chup mot vung nho tren man hinh: rat nhanh, khong can OCR.</summary>
    public static bool IsOpenOnScreen(Rectangle area)
    {
        var r = new Rectangle(area.Left + (int)(X0 * area.Width), area.Top + (int)(Y0 * area.Height),
            Math.Max(6, (int)((X1 - X0) * area.Width)), Math.Max(12, (int)((Y1 - Y0) * area.Height)));
        using var b = OcrService.Capture(r);
        return MeanLum(b, new Rectangle(0, 0, b.Width, b.Height)) < Threshold;
    }

    /// <summary>
    /// Tim o nhap va nut Confirm khi hop dang mo, khong doc chu (dung cho moi ngon ngu):
    /// o nhap la dai sang hon nen (do sang ~76 so voi ~44); phim Esc/Space la hai khoi xam sang (~162) o thanh duoi.
    /// Tra ve null neu khong tim thay.
    /// </summary>
    public static (Pt field, Pt confirm)? FindControls(Bitmap full)
    {
        int w = full.Width, h = full.Height;

        // --- O nhap: cot x=60% chieu rong, dai lien tuc co do sang 60..105 trong khoang y 38%..62%
        int x = (int)(0.60 * w);
        int? f0 = null, f1 = null;
        for (int y = (int)(0.38 * h); y < (int)(0.62 * h); y++)
        {
            double l = Lum(full.GetPixel(x, y));
            if (l >= 60 && l <= 105) { f0 ??= y; f1 = y; }
            else if (f0 != null && y - f1!.Value > 3) break;
        }
        if (f0 == null || f1!.Value - f0.Value < 0.025 * h) return null;
        double fieldY = (f0.Value + f1.Value) / 2.0 / h;

        // --- Phim Esc / Space: cac doan ngang lien tuc (>= 0.8% chieu rong) co do sang 135..190 o thanh duoi
        int minRun = Math.Max(6, (int)(0.008 * w));
        int xEnd = (int)(0.66 * w);
        var segs = new List<(int y, int x0, int x1)>();
        for (int y = (int)(0.58 * h); y < (int)(0.76 * h); y++)
        {
            int runStart = -1;
            for (int xx = (int)(0.36 * w); xx <= xEnd; xx++)
            {
                bool lit = xx < xEnd && Lum(full.GetPixel(xx, y)) is >= 135 and <= 190;
                if (lit) { if (runStart < 0) runStart = xx; }
                else if (runStart >= 0)
                {
                    if (xx - runStart >= minRun) segs.Add((y, runStart, xx - 1));
                    runStart = -1;
                }
            }
        }
        if (segs.Count == 0) return null;

        // Gom thanh cac khoi theo x (khoang cach > 2% chieu rong => khoi moi)
        var clusters = new List<(int x0, int x1, int y0, int y1)>();
        int gap = (int)(0.02 * w);
        foreach (var s in segs.OrderBy(s => s.x0))
        {
            int idx = clusters.FindIndex(c => s.x0 <= c.x1 + gap && s.x1 >= c.x0 - gap);
            if (idx < 0) clusters.Add((s.x0, s.x1, s.y, s.y));
            else
            {
                var c = clusters[idx];
                clusters[idx] = (Math.Min(c.x0, s.x0), Math.Max(c.x1, s.x1), Math.Min(c.y0, s.y), Math.Max(c.y1, s.y));
            }
        }
        clusters = clusters.Where(c => c.y1 - c.y0 >= 0.015 * h).ToList();
        if (clusters.Count < 2) return null;

        var space = clusters.OrderByDescending(c => c.x1).First();   // phim ben phai = Space (Confirm)
        double capX = (space.x0 + space.x1) / 2.0 / w;
        double capY = (space.y0 + space.y1) / 2.0 / h;

        // Nhan "Confirm / Xac nhan" nam ngay ben phai phim, cach tam phim ~4.7% chieu rong
        return (new Pt(0.52, fieldY), new Pt(capX + 0.047, capY));
    }
}
