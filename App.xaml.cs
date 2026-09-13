namespace IPOProcessingSystem;

public partial class App : Application
{
    private static void Log(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IPOProcessingSystem",
                "startup.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    public App(AppShell shell)
    {
        Log("App.xaml.cs constructor start");
        try
        {
            InitializeComponent();
            Log("App.xaml.cs InitializeComponent finished");
            MainPage = shell;
            Log("App.xaml.cs MainPage set to shell");
        }
        catch (Exception ex)
        {
            Log($"[CRASH App.xaml.cs] {ex}");
            throw;
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Log("App.CreateWindow started");
        var window = base.CreateWindow(activationState);
        Log($"App.CreateWindow window created: {window}");
        window.Created += (s, e) => Log("Window.Created fired");
        window.Stopped += (s, e) => Log("Window.Stopped fired");
        window.Destroying += (s, e) => Log("Window.Destroying fired");
        return window;
    }
}


