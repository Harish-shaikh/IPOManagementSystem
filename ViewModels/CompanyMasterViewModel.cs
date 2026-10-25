using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.ViewModels;

public partial class CompanyMasterViewModel : BaseViewModel
{
    private readonly DatabaseService _dbService;
    private readonly IAlertService _alertService;

    [ObservableProperty] private CompanyMaster company = new();
    [ObservableProperty] private bool isEditing;

    public string[] IssueTypes { get; } = { "SME" };
    public string[] Exchanges { get; } = { "ONLY NSE", "ONLY BSE", "BOTH" };

    public CompanyMasterViewModel(DatabaseService dbService, IAlertService alertService)
    {
        _dbService = dbService;
        _alertService = alertService;
        Title = "Company Master";
    }

    [RelayCommand]
    public async Task LoadAsync(int ipoId)
    {
        var existing = await _dbService.Connection.Table<CompanyMaster>()
            .Where(c => c.IPO_ID == ipoId).FirstOrDefaultAsync();
        if (existing != null)
        {
            existing.IssueType = "SME";
            Company = existing;
        }
    }

    [RelayCommand]
    private void New()
    {
        Company = new CompanyMaster { IssueType = "SME" };
        StatusMessage = "New company master record created.";
    }

    [RelayCommand]
    private void Edit()
    {
        IsEditing = true;
        StatusMessage = "Editing — make changes then click Save.";
    }

    [RelayCommand]
    private async Task BrowseClientAsync()
    {
        var clients = await _dbService.Connection.Table<ClientMaster>().ToListAsync();
        if (!clients.Any())
        {
            StatusMessage = "No clients found. Please seed client master.";
            return;
        }
        // Present client list as prompt options
        var names = clients.Select(c => $"{c.ClientId}: {c.ClientName}").ToArray();
        var picked = await _alertService.ShowActionSheetAsync("Select Client", "Cancel", names);
        if (picked == null || picked == "Cancel") return;
        var chosen = clients.FirstOrDefault(c => picked.StartsWith($"{c.ClientId}:"));
        if (chosen != null)
        {
            Company.ClientId = chosen.ClientId;
            Company.ClientName = chosen.ClientName;
            OnPropertyChanged(nameof(Company));
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            if (Company.IPO_ID == 0)
                await _dbService.Connection.InsertAsync(Company);
            else
                await _dbService.Connection.UpdateAsync(Company);

            IsEditing = false;
            StatusMessage = "Company master details saved.";
            await _alertService.ShowSuccessAsync("Company master details saved successfully.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Company.IPO_ID == 0) return;
        if (!await _alertService.ConfirmAsync("Delete Company", "Are you sure you want to delete this company record?")) return;
        await _dbService.Connection.DeleteAsync(Company);
        StatusMessage = "Company master record deleted.";
        await _alertService.ShowSuccessAsync("Company master record deleted.");
        Company = new CompanyMaster();
    }
}

