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

        // Listen for successful login to unlock the full navigation
        WeakReferenceMessenger.Default.Register<LoginSuccessMessage>(this, (_, msg) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Show all flyout items and enable flyout navigation
                PreIpoFlyoutItem.IsVisible = true;
                IpoProcessingFlyoutItem.IsVisible = true;
                PostIpoFlyoutItem.IsVisible = true;
                SettingsFlyoutItem.IsVisible = true;
                SystemFlyoutItem.IsVisible = true;
                FlyoutBehavior = FlyoutBehavior.Flyout;

                // Navigate to IPO Generate page as the initial landing screen after login
                GoToAsync("//ipogenerate");
            });
        });

        // Listen for logout message to collapse navigation and clear state
        WeakReferenceMessenger.Default.Register<LogoutMessage>(this, (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Clear the active IPO context
                _appState.ActiveIpoId = 0;
                _appState.ClientName = "None";
                _appState.LoggedInUser = string.Empty;

                // Collapse all flyout sections — user must log in again
                PreIpoFlyoutItem.IsVisible = false;
                IpoProcessingFlyoutItem.IsVisible = false;
                PostIpoFlyoutItem.IsVisible = false;
                SettingsFlyoutItem.IsVisible = false;
                SystemFlyoutItem.IsVisible = false;
                FlyoutBehavior = FlyoutBehavior.Disabled;

                GoToAsync("//login");
            });
        });
    }
}


