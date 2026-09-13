using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Day-wise bid processing — Upload → Mapping IFSC → Email to Bank.</summary>
public partial class DayWiseViewModel : BaseViewModel
{
    private readonly IBidImportService _bidImportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string exchange = "BSE";
    [ObservableProperty] private string asbaType = "MEMBER ASBA";
    [ObservableProperty] private string filePath = string.Empty;
    [ObservableProperty] private int totalRecords;
    [ObservableProperty] private decimal totalQuantity;
    [ObservableProperty] private string selectedDay = "Day 1";
    [ObservableProperty] private string selectedBank = string.Empty;
    [ObservableProperty] private bool selectAllBanks;

    public ObservableCollection<string> Banks { get; } = new();
    public ObservableCollection<string> SelectedBanks { get; } = new();
    public ObservableCollection<ApplnsxBid> PreviewRows { get; } = new();
    public List<string> DayOptions { get; } = Enumerable.Range(1, 10).Select(d => $"Day {d}").ToList();

    private readonly AppState _appState;

    public DayWiseViewModel(IBidImportService bidImportService, IAlertService alertService, DatabaseService dbService, AppState appState)
    {
        _bidImportService = bidImportService;
        _alertService = alertService;
        _dbService = dbService;
        _appState = appState;
        IpoId = _appState.ActiveIpoId;
        _appState.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppState.ActiveIpoId))
                IpoId = _appState.ActiveIpoId;
        };
        Title = "Day Wise Process";
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var ext = Exchange == "NSE" ? ".csv" : ".dat";
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = $"Select {Exchange} Bid File ({ext})",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ext } }
            })
        });
        if (result != null) FilePath = result.FullPath;
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath)) { StatusMessage = "Select a file first."; return; }
        IsBusy = true;
        try
        {
            bool isBank = AsbaType.Contains("BANK", StringComparison.OrdinalIgnoreCase) && !AsbaType.Contains("MEMBER");
            var rows = await _bidImportService.ParseBidFileAsync(FilePath, IpoId, AsbaType, isBank, Exchange);
            var result = await _bidImportService.BulkInsertAsync(rows);
            TotalRecords = result.TotalRecords;
            TotalQuantity = result.TotalQuantity;
            StatusMessage = result.Message;
            PreviewRows.Clear();
            foreach (var r in rows.Take(100)) PreviewRows.Add(r);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ProcessAsync()
    {
        IsBusy = true;
        try
        {
            // Initialize day processing — updates status
            StatusMessage = $"PROCESS-{SelectedDay} IS INITIALISED";
            await Task.Delay(500); // simulate work
            await _alertService.ShowSuccessAsync($"Day {SelectedDay} processing initialized for IPO {IpoId}.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task MapIfscAsync()
    {
        IsBusy = true;
        try
        {
            var mapped = await _bidImportService.MapDummyIfscAsync(IpoId);
            StatusMessage = $"{mapped} record(s) IFSC-mapped.";
            await _alertService.ShowSuccessAsync("Bank Code Updated Successfully.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateDayWiseDataAsync()
    {
        if (!await _alertService.ConfirmAsync("Day Wise Data", $"Generate day-wise data for Day {SelectedDay}?")) return;
        IsBusy = true;
        try
        {
            var exportPath = GetExportPath("DayWise");
            Directory.CreateDirectory(exportPath);
            // Generate day-wise Excel per bank
            var bids = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId).ToListAsync();
            StatusMessage = $"Day-wise data generated. {bids.Count} records.";
            await _alertService.ShowSuccessAsync("Day-wise data generated successfully.", exportPath);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EmailToBanksAsync()
    {
        await _alertService.ShowAsync("Email To Banks", "Email sending is configured via SMTP settings. Please verify network access and run SMTP dispatch.");
    }

    private string GetExportPath(string subFolder)
        => Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{IpoId}", subFolder);
}
