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
    private static string Norm(string s) => new string(s.Where(char.IsLetter).ToArray()).ToLowerInvariant();

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
