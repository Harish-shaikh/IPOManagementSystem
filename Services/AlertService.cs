using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

/// <summary>
/// MAUI Shell-based alert/dialog service.
/// Dispatches all calls to the main thread for safe UI access.
/// </summary>
public class AlertService : IAlertService
{
    public async Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No")
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return false;
        return await MainThread.InvokeOnMainThreadAsync(
            () => page.DisplayAlert(title, message, accept, cancel));
    }

    public async Task ShowAsync(string title, string message, string ok = "OK")
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return;
        await MainThread.InvokeOnMainThreadAsync(
            () => page.DisplayAlert(title, message, ok));
    }

    public async Task ShowSuccessAsync(string message, string? filePath = null)
    {
        var full = filePath != null ? $"{message}\n\nFile: {filePath}" : message;
        await ShowAsync("Success", full);
    }

    public async Task ShowErrorAsync(string message)
    {
        await ShowAsync("Error", message, "OK");
    }

    public async Task<string?> ShowActionSheetAsync(string title, string cancel, params string[] buttons)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null) return null;
        return await MainThread.InvokeOnMainThreadAsync(
            () => page.DisplayActionSheet(title, cancel, null, buttons));
    }
}

