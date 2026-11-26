namespace WwmRedeemer;

/// <summary>Ghi log khoi dong/loi vao %AppData%\WwmRedeemer\app.log de de chan doan khi app khong mo duoc.</summary>
public static class Trace
{
    private static readonly object Lock = new();
    private static string FilePath => Path.Combine(AppConfig.DirPath, "app.log");

    public static void Write(string msg)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(AppConfig.DirPath);
                var f = new FileInfo(FilePath);
                if (f.Exists && f.Length > 256 * 1024) f.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {msg}{Environment.NewLine}");
            }
        }
        catch { }
    }

    public static void HookGlobalHandlers(System.Windows.Application app)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("UNHANDLED: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { Write("UNOBSERVED TASK: " + e.Exception); e.SetObserved(); };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Write("UI EXCEPTION: " + e.Exception);
            System.Windows.MessageBox.Show(e.Exception.Message + "\n\nChi tiet: " + FilePath, "WWM Code Redeemer");
            e.Handled = true;
        };
    }
}
