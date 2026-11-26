using System.Drawing;
using System.Text;
using System.Windows;

namespace WwmRedeemer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Trace.HookGlobalHandlers(this);
        Trace.Write($"Start v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} admin={new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)} os={Environment.OSVersion}");

        // Che do tu kiem tra (khong mo giao dien): --selftest [--img duong_dan_anh]...
        if (e.Args.Contains("--selftest"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            await SelfTestAsync(e.Args);
            Shutdown();
            return;
        }

        try
        {
            var w = new MainWindow();
            Trace.Write("MainWindow created");
            w.Show();
            Trace.Write("MainWindow shown");
        }
        catch (Exception ex)
        {
            Trace.Write("STARTUP FAILED: " + ex);
            MessageBox.Show(ex.Message + "\n\n" + Path.Combine(AppConfig.DirPath, "app.log"), "WWM Code Redeemer");
            Shutdown(1);
        }
    }

    private static async Task SelfTestAsync(string[] args)
    {
        var sb = new StringBuilder();
        try
        {
            var res = await CodeService.FetchActiveAsync();
            sb.AppendLine($"API: {res.Codes.Count} active codes, error={res.Error ?? "none"}, cache={res.FromCache}");
            sb.AppendLine("first5: " + string.Join(", ", res.Codes.Take(5)));

            sb.AppendLine($"OCR available: {OcrService.Available}");
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "--img") continue;
                using var bmp = new Bitmap(args[i + 1]);
                var lines = await OcrService.ReadAsync(bmp);
                sb.AppendLine($"--- {args[i + 1]} ({bmp.Width}x{bmp.Height}), {lines.Count} lines");
                foreach (var l in lines) sb.AppendLine($"  [{l.X:0},{l.Y:0}] {l.Text}");
                var btn = Locator.FindExchangeButton(lines, bmp.Width, bmp.Height);
                var (field, confirm) = Locator.FindDialog(lines, bmp.Width, bmp.Height);
                sb.AppendLine($"  button={Fmt(btn, bmp)} field={Fmt(field, bmp)} confirm={Fmt(confirm, bmp)} dialogOpen={Locator.DialogOpen(lines)}");
                var (outcome, msg) = Redeemer.Classify(lines, bmp.Width, bmp.Height);
                sb.AppendLine($"  classify={outcome} msg='{msg}'");

                double lum = DialogDetector.ProbeLum(bmp);
                sb.AppendLine($"  probeLum={lum:0.0} open={DialogDetector.IsOpen(bmp)}");
                var ctl = DialogDetector.FindControls(bmp);
                sb.AppendLine(ctl == null ? "  visual controls=null" : $"  visual field={Fmt(ctl.Value.field, bmp)} confirm={Fmt(ctl.Value.confirm, bmp)}");
            }
        }
        catch (Exception ex) { sb.AppendLine("ERROR: " + ex); }

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "selftest.txt"), sb.ToString());
    }

    private static string Fmt(Pt? p, Bitmap b) =>
        p == null ? "null" : $"({p.X:0.000},{p.Y:0.000}) px=({p.X * b.Width:0},{p.Y * b.Height:0})";
}
