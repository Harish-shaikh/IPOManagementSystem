using CommunityToolkit.Mvvm.ComponentModel;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Singleton shared state that broadcasts the currently active IPO ID
/// across all pages/viewmodels and footer bar without tight coupling.
/// </summary>
public partial class AppState : ObservableObject
{
    [ObservableProperty] private int    _activeIpoId;
    [ObservableProperty] private string? _clientName   = null;     // populated after IPO is selected
    [ObservableProperty] private string? _loggedInUser = null;     // populated after successful login
    [ObservableProperty] private string  _version      = "1.0.0";

    public string? ActiveClientName => ClientName;

    /// <summary>True once the user has successfully authenticated.</summary>
    public bool IsLoggedIn => !string.IsNullOrEmpty(LoggedInUser);
}

