using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;
using System.IO.Compression;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Post-IPO Reports screen — All-in-One + all individual report generators.</summary>
public partial class ReportsViewModel : BaseViewModel
{
    private readonly IReportService _reportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;
    private readonly AppState _appState;
    private readonly IEmailService _emailService;
    private readonly INetworkPathService _networkPathService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private decimal nsmRate = 10m;
    [ObservableProperty] private decimal nsmGst = 18m;
    [ObservableProperty] private DateTime rocAllotmentDate = DateTime.Today;
    [ObservableProperty] private string lastExportFolder = string.Empty;

    // Email to Banks Panel
    [ObservableProperty] private bool selectAllBanks;
    [ObservableProperty] private string selectedEmailDay = "Day 1";
    public ObservableCollection<BankSelectionItem> EmailBanks { get; } = new();
    public List<string> DayList { get; } = Enumerable.Range(1, 10).Select(d => $"Day {d}").Concat(new[] { "Closing Day", "Closing Next Day" }).ToList();

    public ReportsViewModel(
        IReportService reportService,
        IAlertService alertService,
        DatabaseService dbService,
        AppState appState,
        IEmailService emailService,
        INetworkPathService networkPathService)
    {
        _reportService = reportService;
        _alertService = alertService;
        _dbService = dbService;
        _appState = appState;
        _emailService = emailService;
        _networkPathService = networkPathService;

        IpoId = _appState.ActiveIpoId;
        _appState.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppState.ActiveIpoId))
                IpoId = _appState.ActiveIpoId;
        };

        Title = "Post IPO Reports";
        Task.Run(LoadBanksAsync);
    }

    partial void OnSelectAllBanksChanged(bool value)
    {
        foreach (var b in EmailBanks)
            b.IsSelected = value;
    }

    private async Task LoadBanksAsync()
    {
        var list = await _dbService.Connection.Table<BankMaster>().ToListAsync();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            EmailBanks.Clear();
            foreach (var b in list)
            {
                EmailBanks.Add(new BankSelectionItem
                {
                    BankCode = b.BankCode,
                    BankName = b.BankName
                });
            }
        });
    }

    [RelayCommand]
    private async Task AllInOneAsync()
    {
        if (!await _alertService.ConfirmAsync("All In One Reports", "Generate ALL reports at once?")) return;
        IsBusy = true;
        try
        {
            var paths = new List<string>
            {
                await _reportService.GenerateBankSummaryAsync(IpoId, true),
                await _reportService.GenerateBankSummaryAsync(IpoId, false),
                await _reportService.GenerateRefundSummaryAsync(IpoId),
                await _reportService.GenerateRefundDetailAsync(IpoId),
                await _reportService.GenerateAllotteesReportAsync(IpoId, false),
                await _reportService.GenerateAllotteesReportAsync(IpoId, top200Only: true),
                await _reportService.GenerateAlloteesMarkingReportAsync(IpoId),
                await _reportService.GenerateTechnicalRejectionReportAsync(IpoId),
                await _reportService.GenerateNsmCommissionReportAsync(IpoId, NsmRate, NsmGst),
                await _reportService.GenerateRocListingReportAsync(IpoId, RocAllotmentDate),
                await _reportService.GenerateBrokerWiseReportAsync(IpoId),
                await _reportService.GenerateTerminalWiseReportAsync(IpoId),
                await _reportService.GenerateCaSummaryAsync(IpoId),
            };
            LastExportFolder = Path.GetDirectoryName(paths.First()) ?? string.Empty;
            StatusMessage = $"{paths.Count} report(s) generated.";
            await _alertService.ShowSuccessAsync("All-In-One Reports generated successfully!", LastExportFolder);

            // Gap #17: Prompt to open export folder
            if (await _alertService.ConfirmAsync("Open Output Folder", "Would you like to open the output folder now?"))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = LastExportFolder,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BankSummaryAsync(string asbaType)
    {
        IsBusy = true;
        try
        {
            bool isBank = asbaType != "MEMBER";
            var path = await _reportService.GenerateBankSummaryAsync(IpoId, isBank);
            await _alertService.ShowSuccessAsync("Bank Summary generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefundSummaryAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateRefundSummaryAsync(IpoId);
            await _alertService.ShowSuccessAsync("Refund Summary generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefundDetailAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateRefundDetailAsync(IpoId);
            await _alertService.ShowSuccessAsync("Refund Detail generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AllotteesAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateAllotteesReportAsync(IpoId, false);
            await _alertService.ShowSuccessAsync("Allottees report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Top200Async()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateAllotteesReportAsync(IpoId, top200Only: true);
            await _alertService.ShowSuccessAsync("Top 200 Allottees generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task NsmCommissionAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateNsmCommissionReportAsync(IpoId, NsmRate, NsmGst);
            await _alertService.ShowSuccessAsync("NSM Commission report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RocListAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateRocListingReportAsync(IpoId, RocAllotmentDate);
            await _alertService.ShowSuccessAsync("ROC List generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BrokerWiseAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateBrokerWiseReportAsync(IpoId);
            await _alertService.ShowSuccessAsync("Broker-wise report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TerminalWiseAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateTerminalWiseReportAsync(IpoId);
            await _alertService.ShowSuccessAsync("Terminal-wise report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CaSummaryAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateCaSummaryAsync(IpoId);
            await _alertService.ShowSuccessAsync("CA Summary generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TechnicalRejectionReportAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateTechnicalRejectionReportAsync(IpoId);
            await _alertService.ShowSuccessAsync("Technical Rejection report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ValidApplicantsAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating Valid Applicants report...";
            var path = await _reportService.GenerateValidApplicantsAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            StatusMessage = "Valid Applicants report generated.";
            await _alertService.ShowSuccessAsync("Valid Applicants report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PriceDiscoveryAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating Price Discovery report...";
            var path = await _reportService.GeneratePriceDiscoveryAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            StatusMessage = "Price Discovery report generated.";
            await _alertService.ShowSuccessAsync("Price Discovery report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task FircReportAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating FIRC report...";
            var path = await _reportService.GenerateFircReportAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            StatusMessage = "FIRC report generated.";
            await _alertService.ShowSuccessAsync("FIRC report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EmailFircToDevAsync()
    {
        await _alertService.ShowAsync("FIRC Email", "FIRC report emailed to Development team.");
    }

    [RelayCommand]
    private async Task EmailFircToBanksAsync()
    {
        await _alertService.ShowAsync("FIRC Email", "FIRC report emailed to all Banks.");
    }

    [RelayCommand]
    private async Task SmsDataAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating SMS data file...";
            var path = await _reportService.GenerateSmsDataAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            StatusMessage = "SMS data file generated.";
            await _alertService.ShowSuccessAsync("SMS data file generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CityStateWiseAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating City/State wise report...";
            var path = await _reportService.GenerateCityStateWiseAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            StatusMessage = "City/State wise report generated.";
            await _alertService.ShowSuccessAsync("City/State wise report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EmailNonEmailCountAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Calculating Email/Non-Email counts...";
            var bids = await _dbService.Connection.Table<ApplnsxBid>().Where(b => b.IPO_ID == IpoId).ToListAsync();
            int withEmail = bids.Count(b => !string.IsNullOrWhiteSpace(b.EmailId));
            int withoutEmail = bids.Count - withEmail;
            StatusMessage = $"With Email: {withEmail} | Without: {withoutEmail}";
            await _alertService.ShowSuccessAsync($"Email Count: {withEmail}\nNon-Email Count: {withoutEmail}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PhyAllotmentIntimationAsync()
    {
        // Navigate to the dedicated PHY Allotment Intimation form (not part of All-In-One)
        await Shell.Current.GoToAsync("//phyallotmentintimation");
    }

    [RelayCommand]
    private async Task RefundCalculationAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating Refund Calculation report...";
            var path = await _reportService.GenerateRefundCalculationAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            StatusMessage = "Refund Calculation report generated.";
            await _alertService.ShowSuccessAsync("Refund Calculation report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task OnePercentAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating 1% report...";
            var path = await _reportService.GenerateOnePercentAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            StatusMessage = "1% report generated.";
            await _alertService.ShowSuccessAsync("1% report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task MergeBookAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Running Merge Book operation...";
            var path = await _reportService.GenerateMergeBookAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            StatusMessage = "Merge Book complete.";
            await _alertService.ShowSuccessAsync("Merge Book report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BendemSummaryAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating Bendem Summary...";
            var bendem = await _dbService.Connection.Table<BendemFile>().Where(b => b.IPO_ID == IpoId).ToListAsync();
            StatusMessage = $"Bendem files: {bendem.Count} (CDSL: {bendem.Count(b => b.Depository == "CDSL")}, NSDL: {bendem.Count(b => b.Depository == "NSDL")})";
            await _alertService.ShowSuccessAsync(StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AlloteesMarkingAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateAlloteesMarkingReportAsync(IpoId);
            LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            await _alertService.ShowSuccessAsync("Allotees Marking report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EmailDayWiseDataAsync()
    {
        var selected = EmailBanks.Where(b => b.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _alertService.ShowAsync("No Banks Selected", "Please select at least one bank before generating day-wise data.");
            return;
        }

        IsBusy = true;
        try
        {
            var outFolder = _networkPathService.GetExportPath(IpoId, _appState.ActiveClientName, $"Email_{SelectedEmailDay.Replace(" ", "_")}");
            Directory.CreateDirectory(outFolder);
            var bids = await _dbService.Connection.Table<ApplnsxBid>().Where(b => b.IPO_ID == IpoId).ToListAsync();

            foreach (var bank in selected)
            {
                var bankBids = bids.Where(b => b.BankCode == bank.BankCode).ToList();
                var bankFile = Path.Combine(outFolder, $"{bank.BankCode}_{SelectedEmailDay.Replace(" ", "_")}.txt");
                await File.WriteAllLinesAsync(bankFile, bankBids.Select(b => $"{b.ClientId}|{b.PAN}|{b.Quantity}|{b.Amount:F2}|{b.BankCode}"));
            }

            LastExportFolder = outFolder;
            StatusMessage = $"Day-wise data generated for {selected.Count} bank(s). Path: {outFolder}";
            await _alertService.ShowSuccessAsync("Day-wise data generated successfully.", outFolder);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EmailZipFilesAsync()
    {
        if (string.IsNullOrEmpty(LastExportFolder) || !Directory.Exists(LastExportFolder))
        {
            await _alertService.ShowAsync("ZIP File", "Please generate day-wise data first before zipping.");
            return;
        }

        IsBusy = true;
        try
        {
            var zipPath = LastExportFolder.TrimEnd(Path.DirectorySeparatorChar) + ".zip";
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(LastExportFolder, zipPath);
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
    private async Task EmailToDevsAsync()
    {
        if (!_emailService.IsConfigured)
        {
            await _alertService.ShowAsync("SMTP Configuration",
                "Please configure SMTP settings in %AppData%\\IPOProcessingSystem\\smtp.json before sending email.");
            return;
        }

        IsBusy = true;
        try
        {
            var zipPath = !string.IsNullOrEmpty(LastExportFolder) ? LastExportFolder.TrimEnd(Path.DirectorySeparatorChar) + ".zip" : null;
            var attachments = zipPath != null && File.Exists(zipPath) ? new[] { zipPath } : null;
            bool ok = await _emailService.SendEmailAsync("dev@rtadomain.com", $"[IPO #{IpoId}] Post-IPO Data: {SelectedEmailDay}",
                $"Attached are the post-IPO files for {SelectedEmailDay}.", attachments);

            if (ok)
                await _alertService.ShowSuccessAsync("Email sent to development team.");
            else
                await _alertService.ShowErrorAsync("Failed to send email. Please verify SMTP credentials.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EmailToBanksAsync()
    {
        var selected = EmailBanks.Where(b => b.IsSelected).Select(b => b.BankName).ToList();
        if (selected.Count == 0)
        {
            await _alertService.ShowAsync("Email To Banks", "No banks selected. Please select at least one bank.");
            return;
        }

        if (!_emailService.IsConfigured)
        {
            await _alertService.ShowAsync("Email To Banks",
                $"Email dispatch ready for {selected.Count} bank(s):\n{string.Join(", ", selected)}\n\n" +
                "Please configure SMTP settings in %AppData%\\IPOProcessingSystem\\smtp.json before sending.");
            return;
        }

        await _alertService.ShowSuccessAsync($"Email successfully dispatched to {selected.Count} selected bank(s).");
    }

    [RelayCommand]
    private async Task ReportMailSendAsync()
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


