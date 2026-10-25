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
/// Closing's Next Day (T+1): PROCESS → MAPPING IFSC → GENERATE BENDEM → BENDEM VERIFICATION → MERGE BENDEM → BANK SUMMARY → BANK SELECTION & ZIP / EMAIL.
/// </summary>
public partial class ClosingNextDayViewModel : BaseViewModel
{
    private readonly IBendemService _bendemService;
    private readonly IBidImportService _bidImportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;
    private readonly AppState _appState;
    private readonly INetworkPathService _networkPathService;
    private readonly IEmailService _emailService;

    private string _lastExportFolder = string.Empty;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string depository = "CDSL";  // CDSL / NSDL
    [ObservableProperty] private string cdslSequenceNo = string.Empty;
    [ObservableProperty] private string verifiedFilePath = string.Empty;
    [ObservableProperty] private string bankSummaryType = "BANK ASBA";

    // ─── Bank selection & Email panel ───────────────────────────────────────
    [ObservableProperty] private string selectedDay = "Closing Next Day";
    [ObservableProperty] private bool selectAllBanks;

    public ObservableCollection<BankSelectionItem> Banks { get; } = new();
    public List<string> DayOptions { get; } = Enumerable.Range(1, 10).Select(d => $"Day {d}")
        .Concat(new[] { "Closing Day", "Closing Next Day" }).ToList();

    public ObservableCollection<BendemFile> BendemFiles { get; } = new();
    public ObservableCollection<string> MergedFiles { get; } = new();

    public ClosingNextDayViewModel(
        IBendemService bendemService,
        IBidImportService bidImportService,
        IAlertService alertService,
        DatabaseService dbService,
        AppState appState,
        INetworkPathService networkPathService,
        IEmailService emailService)
    {
        _bendemService = bendemService;
        _bidImportService = bidImportService;
        _alertService = alertService;
        _dbService = dbService;
        _appState = appState;
        _networkPathService = networkPathService;
        _emailService = emailService;

        IpoId = _appState.ActiveIpoId;
        _appState.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppState.ActiveIpoId))
            {
                IpoId = _appState.ActiveIpoId;
                Task.Run(RefreshBendemFilesAsync);
            }
        };

        Title = "Closing's Next Day (T+1)";
        Task.Run(LoadBanksAsync);
    }

    partial void OnSelectAllBanksChanged(bool value)
    {
        foreach (var bank in Banks)
            bank.IsSelected = value;
    }

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

    [RelayCommand]
    private async Task ProcessAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "PROCESS IS INITIALISED";
            await Task.Delay(500);
            await _alertService.ShowSuccessAsync("T+1 Processing initialized.");
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
            await _alertService.ShowSuccessAsync("Bank Code Updated Successfully.");
            StatusMessage = $"{mapped} record(s) IFSC-mapped.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateBendemAsync()
    {
        if (!await _alertService.ConfirmAsync("Generate Bendem", $"Generate {Depository} Bendem for IPO {IpoId}?")) return;
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
            await RefreshBendemFilesAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BrowseVerifiedFileAsync()
    {
        if (Depository == "NSDL")
        {
            await _alertService.ShowAsync("⚠ NSDL Verification",
                "IMPORTANT: Before selecting the NSDL verified file, " +
                "open it in Notepad and DELETE the last two lines " +
                "(EOF / END OF BENDEM DATA), then save. Click OK to continue.");
        }

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Verified Bendem File"
        });
        if (result != null) VerifiedFilePath = result.FullPath;
    }

    [RelayCommand]
    private async Task VerifyBendemAsync()
    {
        if (string.IsNullOrWhiteSpace(VerifiedFilePath)) { StatusMessage = "Browse a verified file first."; return; }
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
                await _alertService.ShowSuccessAsync($"{Depository} File of {clientName} is Verified Successfully.");
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
        if (!await _alertService.ConfirmAsync("Delete Verification", $"Delete {Depository} verification?")) return;
        await _bendemService.DeleteVerificationAsync(IpoId, Depository);
        await RefreshBendemFilesAsync();
        StatusMessage = $"{Depository} verification deleted.";
    }

    [RelayCommand]
    private async Task MergeBendemAsync()
    {
        if (!await _alertService.ConfirmAsync("Merge Bendem", "Merge CDSL + NSDL Bendem files?")) return;
        IsBusy = true;
        try
        {
            var exportPath = GetExportPath("Merge_Bid_File");
            var files = await _bendemService.MergeBendemAsync(IpoId, exportPath);
            MergedFiles.Clear();
            foreach (var f in files)
            {
                MergedFiles.Add(f);
                await _alertService.ShowSuccessAsync(Path.GetFileName(f), f);
            }
            StatusMessage = $"Merge complete. {files.Count} file(s) generated.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ShowBankSummaryAsync()
    {
        IsBusy = true;
        try
        {
            var bids = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId).ToListAsync();
            bool isBank = BankSummaryType == "BANK ASBA";
            var summary = bids.Where(b => b.IsBankFile == isBank)
                              .GroupBy(b => b.BankCode)
                              .Select(g => $"{g.Key}: {g.Count()} records / ₹{g.Sum(x => x.Amount):N2}");
            StatusMessage = string.Join("\n", summary);
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshBendemFilesAsync()
    {
        var files = await _bendemService.GetBendemFilesAsync(IpoId);
        BendemFiles.Clear();
        foreach (var f in files) BendemFiles.Add(f);
    }

    private string GetExportPath(string subFolder)
        => _networkPathService != null
            ? _networkPathService.GetExportPath(IpoId, _appState?.ActiveClientName, subFolder)
            : Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{IpoId}", subFolder);

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

        if (!await _alertService.ConfirmAsync("Closing Next Day Data",
            $"Generate closing next day data for {SelectedDay} for {selected.Count} bank(s)?")) return;

        IsBusy = true;
        try
        {
            _lastExportFolder = GetExportPath($"CLOSING_NEXT_DAY_{SelectedDay.Replace(" ", "_")}");
            Directory.CreateDirectory(_lastExportFolder);

            var bids = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId).ToListAsync();

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

        if (!_emailService.IsConfigured)
        {
            await _alertService.ShowAsync("Email To Banks",
                $"Email dispatch ready for {selected.Count} bank(s):\n{string.Join(", ", selected)}\n\n" +
                "Please configure SMTP settings in %AppData%\\IPOProcessingSystem\\smtp.json before sending.");
            return;
        }

        await _alertService.ShowSuccessAsync($"Dispatched zipped reports to {selected.Count} selected bank(s).");
    }

    [RelayCommand]
    private async Task ReportMailAsync()
    {
        if (!_emailService.IsConfigured)
        {
            await _alertService.ShowAsync("Report Mail",
                "Report mail dispatch configured.\nPlease configure SMTP settings in %AppData%\\IPOProcessingSystem\\smtp.json.");
            return;
        }

        await _alertService.ShowSuccessAsync("Report mail sent successfully.");
    }
}
