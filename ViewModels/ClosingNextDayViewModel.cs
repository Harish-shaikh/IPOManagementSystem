using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>
/// Closing's Next Day (T+1): PROCESS → MAPPING IFSC → GENERATE BENDEM → BENDEM VERIFICATION → MERGE BENDEM → BANK SUMMARY.
/// </summary>
public partial class ClosingNextDayViewModel : BaseViewModel
{
    private readonly IBendemService _bendemService;
    private readonly IBidImportService _bidImportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string depository = "CDSL";  // CDSL / NSDL
    [ObservableProperty] private string cdslSequenceNo = string.Empty;
    [ObservableProperty] private string verifiedFilePath = string.Empty;
    [ObservableProperty] private string bankSummaryType = "BANK ASBA";

    public ObservableCollection<BendemFile> BendemFiles { get; } = new();
    public ObservableCollection<string> MergedFiles { get; } = new();

    public ClosingNextDayViewModel(IBendemService bendemService, IBidImportService bidImportService,
        IAlertService alertService, DatabaseService dbService)
    {
        _bendemService = bendemService;
        _bidImportService = bidImportService;
        _alertService = alertService;
        _dbService = dbService;
        Title = "Closing's Next Day (T+1)";
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
        => Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{IpoId}", subFolder);
}
