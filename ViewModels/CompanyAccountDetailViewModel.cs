using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Company Account Detail — bank account master per IPO for fund transfers.</summary>
public partial class CompanyAccountDetailViewModel : BaseViewModel
{
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private CompanyAccountDetail account = new();
    [ObservableProperty] private bool isEditing;

    public CompanyAccountDetailViewModel(IAlertService alertService, DatabaseService dbService)
    {
        _alertService = alertService;
        _dbService = dbService;
        Title = "Company Account Detail";
    }

    [RelayCommand]
    private async Task LoadAsync(int ipoId)
    {
        var existing = await _dbService.Connection.Table<CompanyAccountDetail>()
            .Where(a => a.IPO_ID == ipoId).FirstOrDefaultAsync();
        Account = existing ?? new CompanyAccountDetail { IPO_ID = ipoId };
    }

    [RelayCommand]
    private void EditAsync()
    {
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            if (Account.Id == 0)
                await _dbService.Connection.InsertAsync(Account);
            else
                await _dbService.Connection.UpdateAsync(Account);
            IsEditing = false;
            await _alertService.ShowSuccessAsync("Company Account Details saved.");
            StatusMessage = "Saved.";
        }
        finally { IsBusy = false; }
    }
}
