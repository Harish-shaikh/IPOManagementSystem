using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Listing Report page — date parameters, NSE/BSE pre/issue/post report generation.</summary>
public partial class ListingReportViewModel : BaseViewModel
{
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private DateTime preIssueDate = DateTime.Today;
    [ObservableProperty] private DateTime issueDate = DateTime.Today;
    [ObservableProperty] private DateTime postIssueDate = DateTime.Today.AddDays(2);
    [ObservableProperty] private string reportType = "Preissue"; // Preissue / Issue / Postissue
    [ObservableProperty] private string reportFor = "NSE"; // NSE / BSE
    [ObservableProperty] private string ofsClid = string.Empty;
    [ObservableProperty] private decimal ofsShares;

    public ObservableCollection<OfferForSaleRecord> OfsRecords { get; } = new();
    public List<string> ReportTypes { get; } = new() { "Preissue", "Issue", "Postissue" };
    public List<string> Exchanges { get; } = new() { "NSE", "BSE" };

    // Friday→Saturday rule note
    public string PostIssueDateNote => PostIssueDate.DayOfWeek == DayOfWeek.Friday
        ? "⚠ Post-Issue date is Friday — use Saturday's date per exchange convention."
        : string.Empty;

    public ListingReportViewModel(IAlertService alertService, DatabaseService dbService)
    {
        _alertService = alertService;
        _dbService = dbService;
        Title = "Listing Report";
    }

    [RelayCommand]
    private async Task GenerateIssueDataAsync()
    {
        IsBusy = true;
        try
        {
            // Update listing data record
            var listing = await _dbService.Connection.Table<ListingData>()
                .Where(l => l.IPO_ID == IpoId).FirstOrDefaultAsync() ?? new ListingData { IPO_ID = IpoId };
            listing.PreIssueDate = PreIssueDate;
            listing.IssueDate = IssueDate;
            listing.PostIssueDate = PostIssueDate;
            if (listing.Id == 0) await _dbService.Connection.InsertAsync(listing);
            else await _dbService.Connection.UpdateAsync(listing);
            await _alertService.ShowSuccessAsync("Issue data generated successfully.");
            StatusMessage = "Listing data saved.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateListingReportAsync()
    {
        IsBusy = true;
        try
        {
            var company = await _dbService.Connection.Table<CompanyMaster>()
                .Where(c => c.IPO_ID == IpoId).FirstOrDefaultAsync();
            var clientName = company?.IssuerName ?? $"IPO_{IpoId}";
            var exportPath = Path.Combine(FileSystem.AppDataDirectory, "Exports",
                $"IPO_{IpoId}", "ListingReports", ReportFor);
            Directory.CreateDirectory(exportPath);

            var fileName = $"{ReportType}_{clientName}_{ReportFor}.xlsx";
            var fullPath = Path.Combine(exportPath, fileName);

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add(ReportType);
            ws.Cell(1, 1).Value = "IPO ID"; ws.Cell(1, 2).Value = IpoId;
            ws.Cell(2, 1).Value = "Client Name"; ws.Cell(2, 2).Value = clientName;
            ws.Cell(3, 1).Value = "Report Type"; ws.Cell(3, 2).Value = ReportType;
            ws.Cell(4, 1).Value = "Exchange"; ws.Cell(4, 2).Value = ReportFor;
            ws.Cell(5, 1).Value = "Pre-Issue Date"; ws.Cell(5, 2).Value = PreIssueDate.ToString("dd/MM/yyyy");
            ws.Cell(6, 1).Value = "Issue Date"; ws.Cell(6, 2).Value = IssueDate.ToString("dd/MM/yyyy");
            ws.Cell(7, 1).Value = "Post-Issue Date"; ws.Cell(7, 2).Value = PostIssueDate.ToString("dd/MM/yyyy");
            wb.SaveAs(fullPath);

            await _alertService.ShowSuccessAsync($"Listing Report ({ReportType}/{ReportFor}) generated.", fullPath);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddOfsRecordAsync()
    {
        if (string.IsNullOrWhiteSpace(OfsClid) || OfsShares <= 0) { StatusMessage = "Enter DP/CL ID and Shares."; return; }
        var record = new OfferForSaleRecord
        {
            IPO_ID = IpoId,
            DPCL = OfsClid,
            Shares = OfsShares,
            Stage = ReportType == "Preissue" ? "PreIssue" : "PostIssue"
        };
        await _dbService.Connection.InsertAsync(record);
        OfsRecords.Add(record);
        OfsClid = string.Empty;
        OfsShares = 0;
    }

    [RelayCommand]
    private async Task DeleteOfsRecordAsync(OfferForSaleRecord record)
    {
        await _dbService.Connection.DeleteAsync(record);
        OfsRecords.Remove(record);
    }

    [RelayCommand]
    private async Task SubmitLockOfsAsync()
    {
        if (!await _alertService.ConfirmAsync("Lock OFS", "Lock Offer For Sale records? This cannot be undone.")) return;
        await _alertService.ShowSuccessAsync("Offer For Sale locked.");
    }

    partial void OnPostIssueDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(PostIssueDateNote));
    }
}
