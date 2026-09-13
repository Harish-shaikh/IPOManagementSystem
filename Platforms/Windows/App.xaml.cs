using Microsoft.UI.Xaml;

namespace IPOProcessingSystem.WinUI;

public partial class App : MauiWinUIApplication
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IPOProcessingSystem",
        "startup.log");

    private static void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    public App()
    {
        Log("App constructor started");
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log($"[CRASH AppDomain] {e.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log($"[CRASH TaskScheduler] {e.Exception}");
        };
        this.UnhandledException += (s, e) =>
        {
            Log($"[CRASH WinUI] {e.Message} | {e.Exception}");
            e.Handled = false;
        };
        try
        {
            InitializeComponent();
            Log("InitializeComponent finished");
        }
        catch (Exception ex)
        {
            Log($"[CRASH InitializeComponent] {ex}");
            throw;
        }
    }

    protected override MauiApp CreateMauiApp()
    {
        Log("CreateMauiApp started");
        try
        {
            var app = MauiProgram.CreateMauiApp();
            Log("CreateMauiApp succeeded");
            return app;
        }
        catch (Exception ex)
        {
            Log($"[CRASH CreateMauiApp] {ex}");
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log("OnLaunched started");
        try
        {
            base.OnLaunched(args);
            Log("base.OnLaunched finished");
        }
        catch (Exception ex)
        {
            Log($"[CRASH OnLaunched] {ex}");
            throw;
        }
    }
}

