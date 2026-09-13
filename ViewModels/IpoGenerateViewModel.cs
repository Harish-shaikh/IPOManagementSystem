using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.ViewModels;

public partial class IpoGenerateViewModel : BaseViewModel
{
    private readonly DatabaseService _dbService;
    private readonly AppState _appState;
    private readonly IAlertService _alertService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private int clientId;
    [ObservableProperty] private string clientName = string.Empty;
    [ObservableProperty] private string issueType = "SME"; // SME / MAIN BOARD
    [ObservableProperty] private CompanyMaster? selectedCompany;
    [ObservableProperty] private string searchFilter = string.Empty;

    public ObservableCollection<CompanyMaster> Companies { get; } = new();
    public ObservableCollection<ClientMaster> Clients { get; } = new();
    [ObservableProperty] private ClientMaster? selectedClient;
    [ObservableProperty] private bool isBrowsingClients;

    public IpoGenerateViewModel(DatabaseService dbService, AppState appState, IAlertService alertService)
    {
        _dbService = dbService;
        _appState = appState;
        _alertService = alertService;
        Title = "IPO Generation";
    }

    [RelayCommand]
    private async Task BrowseClientsAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _dbService.Connection.Table<ClientMaster>().ToListAsync();
            Clients.Clear();
            foreach (var c in list.Where(c => string.IsNullOrWhiteSpace(SearchFilter) ||
                                              c.ClientName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)))
            {
                Clients.Add(c);
            }
            IsBrowsingClients = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void CloseClientBrowse() => IsBrowsingClients = false;

    partial void OnSelectedClientChanged(ClientMaster? value)
    {
        if (value == null) return;
        ClientId = value.ClientId;
        ClientName = value.ClientName;
        IsBrowsingClients = false;
    }

    [RelayCommand]
    private async Task LoadCompaniesAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _dbService.Connection.Table<CompanyMaster>().ToListAsync();
            Companies.Clear();
            foreach (var c in list
                .Where(c => string.IsNullOrEmpty(SearchFilter) ||
                            c.IssuerName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)))
                Companies.Add(c);
        }
        finally { IsBusy = false; }
    }

    partial void OnIssueTypeChanged(string value)
    {
        // Show SME confirmation when user selects SME
        if (value == "SME")
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page != null)
                {
                    bool ok = await page.DisplayAlert("Issue Type", "Are you sure on SME as Issue Type?", "Yes", "No");
                    if (!ok) IssueType = "MAIN BOARD";
                }
            });
        }
    }

    partial void OnSelectedCompanyChanged(CompanyMaster? value)
    {
        if (value == null) return;
        ClientId = value.ClientId;
        ClientName = value.IssuerName;
        IpoId = value.IPO_ID;
    }

    /// <summary>
    /// Matches manual behavior: type 0 (zero) in IPO ID box → new IPO;
    /// typing a known ID loads existing record.
    /// </summary>
    [RelayCommand]
    private async Task ProcessAsync()
    {
        if (IpoId == 0)
        {
            if (ClientId == 0) { StatusMessage = "Select a client first using Browse."; return; }
            var newCompany = new CompanyMaster
            {
                ClientId = ClientId,
                IssuerName = ClientName,
                IssueType = IssueType
            };
            await _dbService.Connection.InsertAsync(newCompany);
            SelectedCompany = newCompany;
            IpoId = newCompany.IPO_ID;
            _appState.ActiveIpoId = IpoId;
            _appState.ClientName = ClientName;
            StatusMessage = $"New IPO generated with ID {IpoId}.";
            await _alertService.ShowSuccessAsync($"New IPO {IpoId} created for {ClientName}.");
            await LoadCompaniesAsync();
        }
        else
        {
            var existing = await _dbService.Connection.Table<CompanyMaster>()
                .Where(c => c.IPO_ID == IpoId).FirstOrDefaultAsync();
            if (existing != null)
            {
                SelectedCompany = existing;
                ClientName = existing.IssuerName;
                ClientId = existing.ClientId;
                _appState.ActiveIpoId = IpoId;
                _appState.ClientName = existing.IssuerName;
                StatusMessage = $"Loaded existing IPO ID {IpoId} — {existing.IssuerName}.";
                await _alertService.ShowSuccessAsync($"Loaded IPO ID {IpoId} — {existing.IssuerName}.");
            }
            else
            {
                StatusMessage = $"IPO ID {IpoId} not found.";
                await _alertService.ShowErrorAsync($"IPO ID {IpoId} not found.");
            }
        }
    }
}
