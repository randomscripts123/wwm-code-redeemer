using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WwmRedeemer;

public record Pt(double X, double Y);

/// <summary>Cau hinh luu o %AppData%\WwmRedeemer\config.json</summary>
public class AppConfig
{
    public string Language { get; set; } = "vi";
    public bool Accepted { get; set; }
    public Pt? Button { get; set; }
    public Pt? Field { get; set; }
    public Pt? Confirm { get; set; }

    [JsonIgnore] public bool HasPositions => Button != null && Field != null && Confirm != null;

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WwmRedeemer");
    public static string DirPath => Dir;
    private static string FilePath => Path.Combine(Dir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

/// <summary>Lich su ma da nhap, luu o %AppData%\WwmRedeemer\history.json</summary>
public class History
{
    private readonly HashSet<string> _codes = new(StringComparer.OrdinalIgnoreCase);
    private static string FilePath => Path.Combine(AppConfig.DirPath, "history.json");

    public static History Load()
    {
        var h = new History();
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath));
                if (list != null) foreach (var c in list) h._codes.Add(c);
            }
        }
        catch { }
        return h;
    }

    public bool Contains(string code) => _codes.Contains(code);
    public int Count => _codes.Count;

    public void Add(string code) { if (_codes.Add(code)) Save(); }
    public void Clear() { _codes.Clear(); Save(); }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppConfig.DirPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_codes.OrderBy(x => x).ToList()));
        }
        catch { }
    }
}

/// <summary>Lay danh sach ma dang hoat dong tu codes.yar.gg/api/codes (chi muc "active").</summary>
public static class CodeService
{
    private const string Url = "https://codes.yar.gg/api/codes";
    private static readonly HttpClient Http = CreateClient();
    private static string CachePath => Path.Combine(AppConfig.DirPath, "codes-cache.json");

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WWMCodeRedeemer/1.0");
        return c;
    }

    public record FetchResult(List<string> Codes, bool FromCache, string? Error);

    public static async Task<FetchResult> FetchActiveAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(Url);
            var codes = Parse(json);
            try { Directory.CreateDirectory(AppConfig.DirPath); File.WriteAllText(CachePath, json); } catch { }
            return new FetchResult(codes, false, null);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(CachePath))
                    return new FetchResult(Parse(File.ReadAllText(CachePath)), true, ex.Message);
            }
            catch { }
            return new FetchResult(new List<string>(), true, ex.Message);
        }
    }

    /// <summary>Chi doc mang "active"; bo ma da qua han (expiresAt).</summary>
    public static List<string> Parse(string json)
    {
        var result = new List<string>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("active", out var active) || active.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var e in active.EnumerateArray())
        {
            if (!e.TryGetProperty("code", out var c) || c.GetString() is not { } code) continue;
            code = code.Trim();
            if (code.Length == 0) continue;

            if (e.TryGetProperty("expiresAt", out var exp) && exp.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(exp.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var when) && when < DateTimeOffset.UtcNow)
                continue;

            if (!result.Contains(code, StringComparer.OrdinalIgnoreCase)) result.Add(code);
        }
        return result;
    }
}
