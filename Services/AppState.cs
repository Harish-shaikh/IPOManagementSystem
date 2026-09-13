using CommunityToolkit.Mvvm.ComponentModel;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Singleton shared state that broadcasts the currently active IPO ID
/// across all pages/viewmodels and footer bar without tight coupling.
/// </summary>
public partial class AppState : ObservableObject
{
    [ObservableProperty] private int _activeIpoId;
    [ObservableProperty] private string? _clientName = "None";
    [ObservableProperty] private string? _loggedInUser = "50216";
    [ObservableProperty] private string _version = "1.0.0";
}

