using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using IPOProcessingSystem.Services;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly AppState _appState;

    [ObservableProperty] private string username = string.Empty;
    [ObservableProperty] private string password = string.Empty;

    public LoginViewModel(IAuthService authService, AppState appState)
    {
        _authService = authService;
        _appState = appState;
        Title = "IPO Login";
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        // Per manual note: ensure system date format is dd/MM/yyyy before use.
        if (System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern != "dd/MM/yyyy")
        {
            StatusMessage = "Warning: system date format is not dd/MM/yyyy - please verify Windows regional settings.";
        }

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Please enter User ID and Password.";
            return;
        }

        IsBusy = true;
        try
        {
            var success = await _authService.LoginAsync(Username, Password);
            if (success)
            {
                // Update the global AppState so footer bar shows correct user
                _appState.LoggedInUser = Username;
                StatusMessage = "Login successful.";
                // Notify AppShell to unlock full navigation
                WeakReferenceMessenger.Default.Send(new LoginSuccessMessage(Username));

                if (Shell.Current != null)
                    await Shell.Current.GoToAsync("//ipogenerate");
            }
            else
            {
                StatusMessage = "Invalid User ID or Password.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
