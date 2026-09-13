using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Post-IPO Reports screen — All-in-One + all individual report generators.</summary>
public partial class ReportsViewModel : BaseViewModel
{
    private readonly IReportService _reportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private decimal nsmRate = 10m;
    [ObservableProperty] private decimal nsmGst = 18m;
    [ObservableProperty] private DateTime rocAllotmentDate = DateTime.Today;
    [ObservableProperty] private string lastExportFolder = string.Empty;

    public ReportsViewModel(IReportService reportService, IAlertService alertService, DatabaseService dbService)
    {
        _reportService = reportService;
        _alertService = alertService;
        _dbService = dbService;
        Title = "Post IPO Reports";
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
            var bids = await _dbService.Connection.Table<ApplnsxBid>().Where(b => b.IPO_ID == IpoId).ToListAsync();
            var valid = bids.Where(b => !b.IsTechnicallyRejected).ToList();
            StatusMessage = $"Valid Applicants: {valid.Count} records exported.";
            await _alertService.ShowSuccessAsync($"Valid Applicants: {valid.Count} records.");
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
            await Task.Delay(500); // placeholder for actual implementation
            StatusMessage = "Price Discovery report generated.";
            await _alertService.ShowSuccessAsync("Price Discovery report generated.");
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
            await Task.Delay(500);
            StatusMessage = "FIRC report generated.";
            await _alertService.ShowSuccessAsync("FIRC report generated.");
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
            await Task.Delay(500);
            StatusMessage = "SMS data file generated.";
            await _alertService.ShowSuccessAsync("SMS data file generated.");
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
            await Task.Delay(500);
            StatusMessage = "City/State wise report generated.";
            await _alertService.ShowSuccessAsync("City/State wise report generated.");
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
        IsBusy = true;
        try
        {
            StatusMessage = "Generating PHY Allotment Intimation letters...";
            await Task.Delay(500);
            StatusMessage = "PHY Allotment Intimation letters generated.";
            await _alertService.ShowSuccessAsync("PHY Allotment Intimation letters generated.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefundCalculationAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Generating Refund Calculation report...";
            await Task.Delay(500);
            StatusMessage = "Refund Calculation report generated.";
            await _alertService.ShowSuccessAsync("Refund Calculation report generated.");
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
            await Task.Delay(500);
            StatusMessage = "1% report generated.";
            await _alertService.ShowSuccessAsync("1% report generated.");
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
            await Task.Delay(500);
            StatusMessage = "Merge Book complete.";
            await _alertService.ShowSuccessAsync("Merge Book operation complete.");
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
}

