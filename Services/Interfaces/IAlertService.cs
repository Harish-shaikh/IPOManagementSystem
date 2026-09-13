namespace IPOProcessingSystem.Services.Interfaces;

/// <summary>
/// Platform-agnostic dialog service for confirmation prompts and result notifications.
/// Keeps ViewModels testable while still showing native MAUI alerts.
/// </summary>
public interface IAlertService
{
    Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No");
    Task ShowAsync(string title, string message, string ok = "OK");
    Task ShowSuccessAsync(string message, string? filePath = null);
    Task ShowErrorAsync(string message);
    Task<string?> ShowActionSheetAsync(string title, string cancel, params string[] buttons);
}

