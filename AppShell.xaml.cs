using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IPOProcessingSystem.Services;

namespace IPOProcessingSystem;

/// <summary>Message sent by LoginViewModel after a successful login.</summary>
public class LoginSuccessMessage : ValueChangedMessage<string>
{
    public LoginSuccessMessage(string username) : base(username) { }
}

/// <summary>Message sent to trigger logout flow from any ViewModel.</summary>
public class LogoutMessage : ValueChangedMessage<bool>
{
    public LogoutMessage() : base(true) { }
}

public partial class AppShell : Shell
{
    private readonly AppState _appState;

    public AppShell(AppState appState)
    {
        _appState = appState;
        InitializeComponent();

        // Listen for successful login to navigate to initial screen
        WeakReferenceMessenger.Default.Register<LoginSuccessMessage>(this, (_, msg) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FlyoutBehavior = FlyoutBehavior.Disabled;
                GoToAsync("//ipogenerate");
            });
        });

        // Listen for logout message to clear state and return to login
        WeakReferenceMessenger.Default.Register<LogoutMessage>(this, (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Clear the active IPO context
                _appState.ActiveIpoId = 0;
                _appState.ClientName = "None";
                _appState.LoggedInUser = string.Empty;

                FlyoutBehavior = FlyoutBehavior.Disabled;
                GoToAsync("//login");
            });
        });
    }

    // ── Desktop MenuBar handlers ──────────────────────────────────────────

    /// <summary>Navigate to a named Shell route from the menu bar.</summary>
    private async void OnMenuNavigate(object? sender, EventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.CommandParameter is string route)
        {
            if (string.IsNullOrEmpty(_appState.LoggedInUser) && route != "//login")
            {
                await DisplayAlert("Access Restricted", "Please login first to access the system.", "OK");
                return;
            }

            try { await GoToAsync(route); }
            catch { /* ignore navigation errors */ }
        }
    }

    /// <summary>Logout menu item — broadcasts LogoutMessage.</summary>
    private void OnLogoutClicked(object? sender, EventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new LogoutMessage());
    }

    /// <summary>Exit menu item — cleanly terminates the application.</summary>
    private void OnExitClicked(object? sender, EventArgs e)
    {
        Application.Current?.Quit();
    }

    /// <summary>Help → About dialog.</summary>
    private async void OnHelpClicked(object? sender, EventArgs e)
    {
        await DisplayAlert(
            "IPO Process Management System",
            "IPO Registrar & Transfer Agent (RTA) System\n\n" +
            "Version: 1.0.0\n\n" +
            "Process Flow:\n" +
            "1. PRE IPO: IPO Generate → Company Master\n" +
            "2. IPO: Bid Upload → Day Wise → Closing Day → T+1 → Recon (T+2) → Technical Rejection → Basis → Lucky No → Allocation\n" +
            "3. POST IPO: Reports → Listing Report → Data Transfer → FC Email Sender\n\n" +
            "Network Export Root: \\\\192.168.0.253\\IDRIVE\\ISSUE\\EDP\\<Client>\\",
            "OK");
    }

    /// <summary>Help → Workflow Guide.</summary>
    private async void OnWorkflowGuideClicked(object? sender, EventArgs e)
    {
        await DisplayAlert(
            "IPO Workflow Guide",
            "KEY BUSINESS RULES:\n\n" +
            "• BSE files: .dat | NSE files: .csv\n" +
            "• Bank ASBA = source of truth during Recon\n" +
            "• NSDL Bendem: delete last 2 footer lines before upload\n" +
            "• FC Email: Fund Transfer ≠ Letter Gen date during generate phase\n" +
            "• Listing Report: if Post-Issue date is Friday → use Saturday\n" +
            "• Bigshare Online: <20,000 records → Transfer Records; ≥20,000 → BackUp\n" +
            "• NSM Commission defaults: Rate=10, GST=18\n" +
            "• Rejected records move to Rejection Data — never deleted\n" +
            "• Set Basis Category (PSIZE/LOT) for ALL categories before running Basis",
            "OK");
    }
}
