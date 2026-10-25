using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.ViewModels;

/// <summary>
/// PHY Allotment Intimation — generates a formatted .txt allotment letter for
/// physical applicants and saves it to:
/// \\192.168.0.253\idrive\issue\EDP\{ClientName}\AllotmentIntimation\{ClientName}_AllotmentIntimation.txt
/// This is the ONLY Post-IPO report NOT included in "All In One Reports".
/// </summary>
public partial class PhyAllotmentIntimationViewModel : BaseViewModel
{
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;
    private readonly IReportService _reportService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string address = string.Empty;
    [ObservableProperty] private string tel = string.Empty;
    [ObservableProperty] private string fax = string.Empty;
    [ObservableProperty] private string website = string.Empty;
    [ObservableProperty] private string emailId = string.Empty;
    [ObservableProperty] private string exchangeType = "NSE";   // NSE / BSE
    [ObservableProperty] private string ipoDetails = string.Empty;   // free-text block
    [ObservableProperty] private string issuePrice = string.Empty;
    [ObservableProperty] private string modeRemittance = "ASBA UNBLOCK";
    [ObservableProperty] private string contactPerson = string.Empty;
    [ObservableProperty] private string designation = string.Empty;

    public List<string> ExchangeTypes { get; } = new() { "NSE", "BSE" };

    public PhyAllotmentIntimationViewModel(IAlertService alertService,
        DatabaseService dbService, IReportService reportService)
    {
        _alertService = alertService;
        _dbService = dbService;
        _reportService = reportService;
        Title = "PHY Allotment Intimation";
    }

    /// <summary>
    /// Generate the allotment intimation .txt letter and save to network EDP path.
    /// </summary>
    [RelayCommand]
    private async Task GenerateLetterAsync()
    {
        if (string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(ContactPerson))
        {
            await _alertService.ShowErrorAsync("Please fill in Address and Contact Person before generating.");
            return;
        }

        if (!await _alertService.ConfirmAsync("Generate Allotment Intimation",
            "Generate Physical Allotment Intimation letter?")) return;

        IsBusy = true;
        try
        {
            // Resolve client name from company master
            var company = await _dbService.Connection.Table<CompanyMaster>()
                .Where(c => c.IPO_ID == IpoId).FirstOrDefaultAsync();
            // IssuerName is the correct company name field in CompanyMaster
            var clientName = !string.IsNullOrWhiteSpace(company?.IssuerName)
                ? company!.IssuerName
                : $"IPO_{IpoId}";

            // Build the output path
            var edpRoot = _reportService.ExportRootFolder;
            var outputFolder = Path.Combine(edpRoot, clientName, "AllotmentIntimation");
            Directory.CreateDirectory(outputFolder);
            var outputFile = Path.Combine(outputFolder, $"{clientName}_AllotmentIntimation.txt");

            // Build letter content
            var letterText = BuildLetterText(clientName, company);
            await File.WriteAllTextAsync(outputFile, letterText);

            StatusMessage = $"Letter saved: {outputFile}";
            await _alertService.ShowSuccessAsync(
                "Allotment Intimation letter generated successfully.",
                outputFile);
        }
        catch (Exception ex)
        {
            await _alertService.ShowErrorAsync($"Error generating letter: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private string BuildLetterText(string clientName, CompanyMaster? company)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ALLOTMENT INTIMATION LETTER");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();
        sb.AppendLine($"Date: {DateTime.Now:dd/MM/yyyy}");
        sb.AppendLine();
        sb.AppendLine("To,");
        sb.AppendLine(Address);
        sb.AppendLine();
        sb.AppendLine($"Tel: {Tel}");
        sb.AppendLine($"Fax: {Fax}");
        sb.AppendLine($"Website: {Website}");
        sb.AppendLine($"Email: {EmailId}");
        sb.AppendLine();
        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"IPO Details: {clientName} — {ExchangeType}");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();
        sb.AppendLine(IpoDetails);
        sb.AppendLine();
        sb.AppendLine($"Issue Price: {IssuePrice}");
        sb.AppendLine($"Mode of Remittance: {ModeRemittance}");
        sb.AppendLine();
        if (company != null)
        {
            sb.AppendLine($"Face Value: {company.FaceValue:N2}");
            sb.AppendLine($"Issue Size: {company.IssueSize:N0}");
            sb.AppendLine($"Opening Date: {company.OpeningDate:dd/MM/yyyy}");
            sb.AppendLine($"Closing Date: {company.ClosingDate:dd/MM/yyyy}");
        }
        sb.AppendLine();
        sb.AppendLine(new string('-', 60));
        sb.AppendLine("Yours faithfully,");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(ContactPerson);
        sb.AppendLine(Designation);
        sb.AppendLine();
        sb.AppendLine($"Generated by IPO Process System on {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        return sb.ToString();
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
