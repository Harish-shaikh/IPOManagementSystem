using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;
using System.IO.Compression;

namespace IPOProcessingSystem.ViewModels;

/// <summary>
/// Day Wise Process — Upload → Process → Mapping IFSC → Email to Bank panel.
/// Supports Select All Banks checkbox, ZIP FILE generation, and Email To Banks.
/// </summary>
public partial class DayWiseViewModel : BaseViewModel
{
    private readonly IBidImportService _bidImportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;
    private readonly AppState _appState;

    // Last export folder path (set by GenerateDayWiseData)
    private string _lastExportFolder = string.Empty;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string asbaType = "MEMBER ASBA";
    [ObservableProperty] private string filePath = string.Empty;
    [ObservableProperty] private int totalRecords;
    [ObservableProperty] private decimal totalQuantity;
    [ObservableProperty] private string selectedDay = "Day 1";

    /// <summary>
    /// When toggled, checks or unchecks all banks in the checklist.
    /// </summary>
    [ObservableProperty] private bool selectAllBanks;

    public ObservableCollection<BankSelectionItem> Banks { get; } = new();
    public ObservableCollection<ApplnsxBid> PreviewRows { get; } = new();
    public List<string> DayOptions { get; } = Enumerable.Range(1, 10).Select(d => $"Day {d}").ToList();

    public DayWiseViewModel(IBidImportService bidImportService, IAlertService alertService,
        DatabaseService dbService, AppState appState)
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

        // Load banks from master on construct (fire-and-forget in ctor safe in MAUI)
        Task.Run(LoadBanksAsync);
    }

    /// <summary>Propagates Select All toggle to every bank item.</summary>
    partial void OnSelectAllBanksChanged(bool value)
    {
        foreach (var bank in Banks)
            bank.IsSelected = value;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task LoadBanksAsync()
    {
        var bankMasters = await _dbService.Connection.Table<BankMaster>().ToListAsync();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Banks.Clear();
            foreach (var b in bankMasters)
            {
                Banks.Add(new BankSelectionItem
                {
                    BankCode = b.BankCode,
                    BankName = b.BankName
                });
            }
        });
    }

    private string GetExportPath(string subFolder)
        => Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{IpoId}", subFolder);

    // ─── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Bid File",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".dat", ".csv", ".txt" } },
                { DevicePlatform.macOS, new[] { "dat", "csv", "txt" } }
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
            bool isBank = AsbaType.Contains("BANK", StringComparison.OrdinalIgnoreCase)
                          && !AsbaType.Contains("MEMBER");
            var rows = await _bidImportService.ParseBidFileAsync(FilePath, IpoId, AsbaType, isBank);
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
    private async Task ClearDataAsync()
    {
        bool confirm = await _alertService.ConfirmAsync("Clear Uploaded Data",
            "Are you sure about Reupload? Existing data will get deleted!");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            var count = await _bidImportService.ClearDataAsync(IpoId, AsbaType);
            PreviewRows.Clear();
            TotalRecords = 0;
            TotalQuantity = 0;
            StatusMessage = $"Data cleared successfully ({count} records removed). Ready for fresh upload.";
            await _alertService.ShowAsync("Data Cleared",
                "Data cleared successfully.\n\nPlease Close and Open New window for Fresh Uploading Process.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ProcessAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = $"PROCESS-{SelectedDay} IS INITIALISED";
            await Task.Delay(400);
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
        var selected = Banks.Where(b => b.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _alertService.ShowAsync("No Banks Selected", "Please select at least one bank before generating day-wise data.");
            return;
        }

        if (!await _alertService.ConfirmAsync("Day Wise Data", $"Generate day-wise data for {SelectedDay} for {selected.Count} bank(s)?")) return;

        IsBusy = true;
        try
        {
            _lastExportFolder = GetExportPath($"DayWise_{SelectedDay.Replace(" ", "_")}");
            Directory.CreateDirectory(_lastExportFolder);

            var bids = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId).ToListAsync();

            // Generate per-bank files for selected banks
            foreach (var bank in selected)
            {
                var bankBids = bids.Where(b => b.BankCode == bank.BankCode).ToList();
                var bankFile = Path.Combine(_lastExportFolder, $"{bank.BankCode}_{SelectedDay.Replace(" ", "_")}.txt");
                await File.WriteAllLinesAsync(bankFile,
                    bankBids.Select(b => $"{b.ClientId}|{b.PAN}|{b.Quantity}|{b.Amount:F2}|{b.BankCode}"));
            }

            StatusMessage = $"Day-wise data generated for {selected.Count} bank(s). Path: {_lastExportFolder}";
            await _alertService.ShowSuccessAsync("Day-wise data generated successfully.", _lastExportFolder);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ZipFileAsync()
    {
        if (string.IsNullOrEmpty(_lastExportFolder) || !Directory.Exists(_lastExportFolder))
        {
            await _alertService.ShowAsync("ZIP File", "Please generate Day Wise Data first before zipping.");
            return;
        }

        IsBusy = true;
        try
        {
            var zipPath = _lastExportFolder.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(_lastExportFolder, zipPath);
            StatusMessage = $"ZIP created: {zipPath}";
            await _alertService.ShowSuccessAsync("ZIP File Created Successfully.", zipPath);
        }
        catch (Exception ex)
        {
            await _alertService.ShowErrorAsync($"ZIP creation failed: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EmailToBanksAsync()
    {
        var selected = Banks.Where(b => b.IsSelected).Select(b => b.BankName).ToList();
        if (selected.Count == 0)
        {
            await _alertService.ShowAsync("Email To Banks", "No banks selected. Please select at least one bank.");
            return;
        }
        await _alertService.ShowAsync("Email To Banks",
            $"Email dispatch configured for {selected.Count} bank(s):\n{string.Join(", ", selected)}\n\n" +
            "Please verify SMTP settings and network access before sending.");
    }

    [RelayCommand]
    private async Task ReportMailAsync()
    {
        await _alertService.ShowAsync("Report Mail",
            "Report mail will be sent after verifying the day-wise data.\n\nPlease configure SMTP settings in Settings.");
    }
}
