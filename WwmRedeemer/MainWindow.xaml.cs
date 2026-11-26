using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;

namespace WwmRedeemer;

public class CodeItem : INotifyPropertyChanged
{
    public CodeItem(string code, string statusKey) { Code = code; _key = statusKey; }

    public string Code { get; }
    private string _key;

    public string StatusKey
    {
        get => _key;
        set { _key = value; Raise(nameof(StatusKey)); Raise(nameof(StatusText)); Raise(nameof(StatusBrush)); }
    }

    public string StatusText => L.Get(_key);

    public string StatusBrush => _key switch
    {
        "st_sent" => "#4FD1C5",
        "st_running" => "#F6C85F",
        "st_done" => "#6B7385",
        _ => "#A0A8BC"
    };

    public void RefreshText() => Raise(nameof(StatusText));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public partial class MainWindow : Window
{
    private readonly AppConfig _cfg = AppConfig.Load();
    private readonly History _hist = History.Load();
    private readonly ObservableCollection<CodeItem> _items = new();
    private readonly HashSet<string> _imported = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _apiCodes = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        L.Lang = _cfg.Language;
        CodeList.ItemsSource = _items;
        L.Changed += ApplyLanguage;
        ApplyLanguage();
        Loaded += async (_, _) =>
        {
            if (!_cfg.Accepted)
            {
                var r = MessageBox.Show(L.Get("warn_text"), L.Get("warn_title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) { Close(); return; }
                _cfg.Accepted = true;
                _cfg.Save();
            }
            Log(L.Get("log_start"));
            await RefreshCodesAsync();
        };
        Closing += (_, _) => _cts?.Cancel();
    }

    // ------------------------------------------------------------ giao dien

    private void ApplyLanguage()
    {
        SubText.Text = L.Get("app_sub");
        StartBtn.Content = L.Get("btn_start");
        StopBtn.Content = L.Get("btn_stop");
        RefreshBtn.Content = L.Get("btn_refresh");
        ImportBtn.Content = L.Get("btn_import");
        CalibBtn.Content = L.Get("btn_calib");
        ClearBtn.Content = L.Get("btn_clear");
        HdrCodes.Text = L.Get("hdr_codes");
        HdrLog.Text = L.Get("hdr_log");
        HintText.Text = L.Get("hint");
        LangBtn.Content = L.Lang == "vi" ? "VI | en" : "vi | EN";
        foreach (var it in _items) it.RefreshText();
        UpdateCount();
    }

    private void UpdateCount()
    {
        int pending = _items.Count(i => i.StatusKey == "st_pending" || i.StatusKey == "st_running");
        int done = _items.Count - pending;
        CountText.Text = L.F("count", _items.Count, pending, done);
    }

    public void Log(string msg)
    {
        void Do()
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }
        if (Dispatcher.CheckAccess()) Do(); else Dispatcher.Invoke(Do);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        StartBtn.IsEnabled = !busy;
        StopBtn.IsEnabled = busy;
        RefreshBtn.IsEnabled = !busy;
        ImportBtn.IsEnabled = !busy;
        CalibBtn.IsEnabled = !busy;
        ClearBtn.IsEnabled = !busy;
    }

    // ------------------------------------------------------------ danh sach ma

    private void RebuildItems()
    {
        _items.Clear();
        var all = _apiCodes.Concat(_imported.Where(c => !_apiCodes.Contains(c, StringComparer.OrdinalIgnoreCase)));
        foreach (var c in all)
            _items.Add(new CodeItem(c, _hist.Contains(c) ? "st_done" : "st_pending"));
        UpdateCount();
    }

    private async Task RefreshCodesAsync()
    {
        var res = await CodeService.FetchActiveAsync();
        if (res.Error == null)
            Log(L.F("log_fetch_ok", res.Codes.Count));
        else if (res.Codes.Count > 0)
            Log(L.F("log_fetch_cache", res.Error, res.Codes.Count));
        else
            Log(L.F("log_fetch_fail", res.Error));
        _apiCodes = res.Codes;
        RebuildItems();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshCodesAsync();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Text (*.txt)|*.txt|All|*.*", Multiselect = false };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var rx = new Regex("^[A-Za-z0-9]{4,40}$");
            int added = 0, skipped = 0;
            foreach (var raw in File.ReadAllText(dlg.FileName).Split(new[] { '\r', '\n', ',', ';', ' ', '\t' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var code = raw.Trim();
                bool exists = _items.Any(i => i.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                if (!rx.IsMatch(code) || exists) { skipped++; continue; }
                _imported.Add(code);
                added++;
            }
            RebuildItems();
            Log(L.F("log_import", added, skipped));
        }
        catch (Exception ex) { Log(L.F("log_error", ex.Message)); }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _hist.Clear();
        RebuildItems();
        Log(L.Get("log_cleared"));
    }

    private void Lang_Click(object sender, RoutedEventArgs e)
    {
        _cfg.Language = L.Lang == "vi" ? "en" : "vi";
        _cfg.Save();
        L.Set(_cfg.Language);
    }

    // ------------------------------------------------------------ chay macro

    private async Task<bool> SetClipboardAsync(string text)
    {
        for (int i = 0; i < 8; i++)
        {
            try { await Dispatcher.InvokeAsync(() => Clipboard.SetText(text)); return true; }
            catch { await Task.Delay(100); }
        }
        return false;
    }

    private CancellationTokenSource BeginRun()
    {
        SetBusy(true);
        var cts = new CancellationTokenSource();
        _cts = cts;
        // Theo doi phim F12 toan cuc de dung khan cap
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                if (Native.IsDown(Native.VK_F12)) { cts.Cancel(); break; }
                await Task.Delay(40);
            }
        });
        return cts;
    }

    private void EndRun()
    {
        _cts?.Cancel();
        _cts = null;
        SetBusy(false);
        UpdateCount();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var todo = _items.Where(i => i.StatusKey == "st_pending").Select(i => i.Code).ToList();
        if (todo.Count == 0) { Log(L.Get("log_no_codes")); return; }
        if (Native.FindGameWindow() == IntPtr.Zero) { Log(L.Get("log_no_game")); return; }

        Bar.Value = 0;
        var cts = BeginRun();
        var redeemer = new Redeemer(_cfg, Log, SetClipboardAsync);
        int sent = 0;
        try
        {
            sent = await redeemer.RunAsync(todo, OnStatus, (done, total) => Dispatcher.Invoke(() => Bar.Value = done / (double)total), cts.Token);
            Log(L.F("log_finished", sent));
        }
        catch (OperationCanceledException) { Log(L.Get("log_stopped")); }
        catch (Exception ex) { Log(L.F("log_error", ex.Message)); }
        finally
        {
            foreach (var it in _items.Where(i => i.StatusKey == "st_running")) it.StatusKey = "st_pending";
            EndRun();
        }
    }

    private void OnStatus(string code, string key)
    {
        Dispatcher.Invoke(() =>
        {
            var it = _items.FirstOrDefault(i => i.Code == code);
            if (it != null) it.StatusKey = key;
            if (key == "st_sent") _hist.Add(code);
            UpdateCount();
        });
    }

    private async void Calib_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var hwnd = Native.FindGameWindow();
        if (hwnd == IntPtr.Zero) { Log(L.Get("log_no_game")); return; }

        var cts = BeginRun();
        var redeemer = new Redeemer(_cfg, Log, SetClipboardAsync);
        try
        {
            Native.BringToFront(hwnd);
            await Task.Delay(600, cts.Token);
            await redeemer.CalibrateAsync(hwnd, cts.Token);
        }
        catch (OperationCanceledException) { Log(L.Get("log_calib_cancel")); }
        catch (Exception ex) { Log(L.F("log_error", ex.Message)); }
        finally { EndRun(); }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();
}
