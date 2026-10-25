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
/// Closing Day (T) — Full workflow:
///   Upload Bank ASBA + Member ASBA → Process → Map IFSC
///   → Generate Bendem (CDSL/NSDL) → Bendem Verification → Merge Bendem
///   → Select All Banks → Day Wise Data → ZIP FILE → Email To Banks
/// </summary>
public partial class ClosingDayViewModel : BaseViewModel
{
    private readonly IBidImportService _bidImportService;
    private readonly IBendemService _bendemService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;
    private readonly AppState _appState;

    // Tracks the last generated day-wise export folder for ZIP
    private string _lastExportFolder = string.Empty;

    // ─── Upload panel ──────────────────────────────────────────────────────
    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string exchange = "BSE";
    [ObservableProperty] private string asbaType = "BANK ASBA";
    [ObservableProperty] private string filePath = string.Empty;
    [ObservableProperty] private int totalRecords;
    [ObservableProperty] private decimal totalQuantity;

    // ─── Bendem section ────────────────────────────────────────────────────
    [ObservableProperty] private string depository = "CDSL";  // CDSL / NSDL
    [ObservableProperty] private string cdslSequenceNo = string.Empty;
    [ObservableProperty] private string verifiedFilePath = string.Empty;

    // ─── Bank Summary panel ───────────────────────────────────────────────
    [ObservableProperty] private bool showBankSummary;
    [ObservableProperty] private string bankSummaryType = "BANK ASBA";
    public ObservableCollection<BankSummaryRow> BankSummaryList { get; } = new();

    // ─── Email-to-Banks panel ──────────────────────────────────────────────
    [ObservableProperty] private string selectedDay = "Day 1";
    [ObservableProperty] private bool selectAllBanks;

    public ObservableCollection<BankSelectionItem> Banks { get; } = new();
    public ObservableCollection<BendemFile> BendemFiles { get; } = new();
    public ObservableCollection<ApplnsxBid> PreviewRows { get; } = new();
    public List<string> DayOptions { get; } = Enumerable.Range(1, 10).Select(d => $"Day {d}").ToList();

    public ClosingDayViewModel(IBidImportService bidImportService, IBendemService bendemService,
        IAlertService alertService, DatabaseService dbService, AppState appState)
    {
        _bidImportService = bidImportService;
        _bendemService = bendemService;
        _alertService = alertService;
        _dbService = dbService;
        _appState = appState;

        IpoId = _appState.ActiveIpoId;
        _appState.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppState.ActiveIpoId))
            {
                IpoId = _appState.ActiveIpoId;
                Task.Run(RefreshBendemFilesAsync);
            }
        };

        Title = "Closing Day (T)";
        Task.Run(LoadBanksAsync);
        Task.Run(RefreshBendemFilesAsync);
    }

    /// <summary>Propagates Select All toggle to every bank checklist item.</summary>
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

    private async Task RefreshBendemFilesAsync()
    {
        if (IpoId <= 0) return;
        var files = await _bendemService.GetBendemFilesAsync(IpoId);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BendemFiles.Clear();
            foreach (var f in files) BendemFiles.Add(f);
        });
    }

    private string GetExportPath(string subFolder)
        => Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{IpoId}", subFolder);

    // ─── Upload Commands ───────────────────────────────────────────────────────

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
        if (string.IsNullOrWhiteSpace(FilePath)) { StatusMessage = "Select a bid file first."; return; }
        IsBusy = true;
        try
        {
            bool isBank = AsbaType.Contains("BANK", StringComparison.OrdinalIgnoreCase)
                          && !AsbaType.Contains("MEMBER");
            var rows = await _bidImportService.ParseBidFileAsync(FilePath, IpoId, AsbaType, isBank, Exchange);
            var result = await _bidImportService.BulkInsertAsync(rows);
            TotalRecords = result.TotalRecords;
            TotalQuantity = result.TotalQuantity;
            StatusMessage = result.Message;
            PreviewRows.Clear();
            foreach (var r in rows.Take(100)) PreviewRows.Add(r);

            await _alertService.ShowSuccessAsync(
                $"{Exchange} - {AsbaType} UPLOADED SUCCESSFULLY",
                $"Total Records: {result.TotalRecords:N0}\nTotal Quantity: {result.TotalQuantity:N0}");
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

    // ─── Process + IFSC Commands ───────────────────────────────────────────────

    [RelayCommand]
    private async Task ProcessAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "CLOSING DAY PROCESS IS INITIALISED";
            await Task.Delay(400);
            await _alertService.ShowSuccessAsync($"Closing Day processing initialized for IPO {IpoId}.");
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

    // ─── Bendem Commands ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task GenerateBendemAsync()
    {
        if (!await _alertService.ConfirmAsync("Generate Bendem",
            $"Generate {Depository} Bendem for IPO {IpoId} (Closing Day)?")) return;

        IsBusy = true;
        try
        {
            var exportPath = GetExportPath("IPO_Bendem");
            string path;
            if (Depository == "CDSL")
            {
                if (string.IsNullOrWhiteSpace(CdslSequenceNo))
                {
                    StatusMessage = "Enter CDSL sequence file name (e.g. 128307.001)";
                    return;
                }
                path = await _bendemService.GenerateCdslBendemAsync(IpoId, CdslSequenceNo, exportPath);
                await _alertService.ShowSuccessAsync("CDSL Bendem Generated Successfully.", path);
            }
            else
            {
                path = await _bendemService.GenerateNsdlBendemAsync(IpoId, exportPath);
                await _alertService.ShowSuccessAsync("NSDL Bendem Generated Successfully.", path);
            }
            StatusMessage = $"Bendem generated: {Path.GetFileName(path)}";
            await RefreshBendemFilesAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BrowseVerifiedFileAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = $"Select Verified {Depository} Bendem File"
        });
        if (result != null) VerifiedFilePath = result.FullPath;
    }

    [RelayCommand]
    private async Task VerifyBendemAsync()
    {
        if (string.IsNullOrWhiteSpace(VerifiedFilePath))
        {
            StatusMessage = "Browse a verified bendem file first.";
            return;
        }
        if (!await _alertService.ConfirmAsync("Verify Bendem", "Do You Want To Upload Bendem?")) return;

        IsBusy = true;
        try
        {
            bool ok = Depository == "CDSL"
                ? await _bendemService.VerifyCdslBendemAsync(IpoId, VerifiedFilePath)
                : await _bendemService.VerifyNsdlBendemAsync(IpoId, VerifiedFilePath);

            if (ok)
            {
                var company = await _dbService.Connection.Table<CompanyMaster>()
                    .Where(c => c.IPO_ID == IpoId).FirstOrDefaultAsync();
                var clientName = company?.IssuerName ?? "Client";
                await _alertService.ShowSuccessAsync(
                    $"{Depository} File of {clientName} is Verified Successfully.");
                VerifiedFilePath = string.Empty;
                await RefreshBendemFilesAsync();
            }
            else
            {
                await _alertService.ShowErrorAsync("No pending Bendem file found to verify.");
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteVerificationAsync()
    {
        if (!await _alertService.ConfirmAsync("Delete Verification",
            $"Delete {Depository} verification for IPO {IpoId}?")) return;
        await _bendemService.DeleteVerificationAsync(IpoId, Depository);
        await RefreshBendemFilesAsync();
        StatusMessage = $"{Depository} verification deleted.";
    }

    [RelayCommand]
    private async Task MergeBendemAsync()
    {
        if (!await _alertService.ConfirmAsync("Merge Bendem", "Merge CDSL + NSDL Bendem files for Closing Day?")) return;
        IsBusy = true;
        try
        {
            var exportPath = GetExportPath("Merge_Bid_File");
            var files = await _bendemService.MergeBendemAsync(IpoId, exportPath);
            foreach (var f in files)
                await _alertService.ShowSuccessAsync(Path.GetFileName(f), f);

            StatusMessage = $"Merge complete. {files.Count} file(s) generated.";
        }
        finally { IsBusy = false; }
    }

    // ─── Email-to-Banks Commands ───────────────────────────────────────────────

    [RelayCommand]
    private async Task GenerateDayWiseDataAsync()
    {
        var selected = Banks.Where(b => b.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _alertService.ShowAsync("No Banks Selected",
                "Please select at least one bank before generating day-wise data.");
            return;
        }
        if (!await _alertService.ConfirmAsync("Closing Day Wise Data",
            $"Generate closing day data for {selected.Count} bank(s)?")) return;

        IsBusy = true;
        try
        {
            _lastExportFolder = GetExportPath("CLOSING_DAY_PROCESS_(T)");
            Directory.CreateDirectory(_lastExportFolder);

            var bids = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId).ToListAsync();

            foreach (var bank in selected)
            {
                var bankBids = bids.Where(b => b.BankCode == bank.BankCode).ToList();
                var bankFile = Path.Combine(_lastExportFolder, $"{bank.BankCode}_ClosingDay.txt");
                await File.WriteAllLinesAsync(bankFile,
                    bankBids.Select(b => $"{b.ClientId}|{b.PAN}|{b.Quantity}|{b.Amount:F2}|{b.BankCode}"));
            }

            StatusMessage = $"Closing Day data generated for {selected.Count} bank(s). Path: {_lastExportFolder}";
            await _alertService.ShowSuccessAsync("Closing Day data generated successfully.", _lastExportFolder);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ZipFileAsync()
    {
        if (string.IsNullOrEmpty(_lastExportFolder) || !Directory.Exists(_lastExportFolder))
        {
            await _alertService.ShowAsync("ZIP File",
                "Please generate Day Wise Data first before creating ZIP.");
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
            await _alertService.ShowAsync("Email To Banks",
                "No banks selected. Please select at least one bank.");
            return;
        }
        await _alertService.ShowAsync("Email To Banks",
            $"Email dispatch configured for {selected.Count} bank(s):\n{string.Join(", ", selected)}\n\n" +
            "Please verify network access and SMTP configuration before sending.");
    }

    [RelayCommand]
    private void ToggleBankSummary()
    {
        ShowBankSummary = !ShowBankSummary;
        if (ShowBankSummary)
        {
            Task.Run(ShowBankSummaryAsync);
        }
    }

    [RelayCommand]
    private void HideBankSummary() => ShowBankSummary = false;

    [RelayCommand]
    private async Task ShowBankSummaryAsync()
    {
        IsBusy = true;
        try
        {
            var bids = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId).ToListAsync();
            bool isBank = BankSummaryType == "BANK ASBA";
            var grouped = bids.Where(b => b.IsBankFile == isBank)
                              .GroupBy(b => b.BankCode)
                              .Select(g => new BankSummaryRow
                              {
                                  BankCode = string.IsNullOrEmpty(g.Key) ? "UNKNOWN" : g.Key,
                                  Count = g.Count(),
                                  TotalQuantity = g.Sum(x => x.Quantity),
                                  TotalAmount = g.Sum(x => x.Amount)
                              }).OrderBy(r => r.BankCode).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                BankSummaryList.Clear();
                foreach (var r in grouped) BankSummaryList.Add(r);
                ShowBankSummary = true;
                StatusMessage = $"Bank summary loaded for {BankSummaryType}: {grouped.Count} bank(s).";
            });
        }
        finally { IsBusy = false; }
    }
}

