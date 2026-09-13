using ClosedXML.Excel;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Handles Module G: Post-IPO report generation (All-in-One Report Generator).
/// Writes Excel workbooks via ClosedXML to a local export folder; point
/// ExportRootFolder at your org's shared drive / EDP path (as referenced
/// throughout the manual, e.g. \\192.168.0.253\IDRIVE\ISSUE\EDP\...).
/// </summary>
public class ReportService : IReportService
{
    private readonly DatabaseService _dbService;

    /// <summary>Change this to your network export path, e.g. @"\\192.168.0.253\IDRIVE\ISSUE\EDP".</summary>
    public string ExportRootFolder { get; set; } =
        Path.Combine(FileSystem.AppDataDirectory, "Exports");

    public ReportService(DatabaseService dbService)
    {
        _dbService = dbService;
        Directory.CreateDirectory(ExportRootFolder);
    }

    public async Task<string> GenerateBankSummaryAsync(int ipoId, bool bankAsba)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.IsBankFile == bankAsba).ToListAsync();

        var grouped = bids.GroupBy(b => b.BankCode)
            .Select(g => new { BankCode = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) });

        return WriteExcel($"BankSummary_{ipoId}_{(bankAsba ? "Bank" : "Member")}", ws =>
        {
            ws.Cell(1, 1).Value = "Bank Code";
            ws.Cell(1, 2).Value = "Records";
            ws.Cell(1, 3).Value = "Amount";
            int r = 2;
            foreach (var row in grouped)
            {
                ws.Cell(r, 1).Value = row.BankCode;
                ws.Cell(r, 2).Value = row.Count;
                ws.Cell(r, 3).Value = row.Amount;
                r++;
            }
        });
    }

    public async Task<string> GenerateRefundSummaryAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status == "REJECTED").ToListAsync();

        return WriteExcel($"RefundSummary_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Category";
            ws.Cell(1, 2).Value = "Count";
            ws.Cell(1, 3).Value = "Refund Amount";
            int r = 2;
            foreach (var g in bids.GroupBy(b => b.Category))
            {
                ws.Cell(r, 1).Value = g.Key;
                ws.Cell(r, 2).Value = g.Count();
                ws.Cell(r, 3).Value = g.Sum(x => x.Amount);
                r++;
            }
        });
    }

    public async Task<string> GenerateRefundDetailAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status == "REJECTED").ToListAsync();

        return WriteExcel($"RefundDetail_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "FormNo"; ws.Cell(1, 2).Value = "PAN";
            ws.Cell(1, 3).Value = "CLID"; ws.Cell(1, 4).Value = "Amount"; ws.Cell(1, 5).Value = "Reason";
            int r = 2;
            foreach (var b in bids)
            {
                ws.Cell(r, 1).Value = b.OrderNo;
                ws.Cell(r, 2).Value = b.PAN;
                ws.Cell(r, 3).Value = b.CLID;
                ws.Cell(r, 4).Value = b.Amount;
                ws.Cell(r, 5).Value = b.RejectionCode;
                r++;
            }
        });
    }

    public async Task<string> GenerateAllotteesReportAsync(int ipoId, bool top200Only)
    {
        var basis = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();
        var ordered = basis.OrderByDescending(b => b.ShrAlto).AsEnumerable();
        if (top200Only) ordered = ordered.Take(200);

        return WriteExcel($"Allottees_{ipoId}_{(top200Only ? "Top200" : "All")}", ws =>
        {
            ws.Cell(1, 1).Value = "Category"; ws.Cell(1, 2).Value = "Serial"; ws.Cell(1, 3).Value = "Shares Allotted";
            int r = 2;
            foreach (var row in ordered)
            {
                ws.Cell(r, 1).Value = row.Category;
                ws.Cell(r, 2).Value = row.Serial;
                ws.Cell(r, 3).Value = row.ShrAlto;
                r++;
            }
        });
    }

    public Task<string> GenerateCaSummaryAsync(int ipoId) =>
        Task.FromResult(WriteExcel($"CASummary_{ipoId}", ws => ws.Cell(1, 1).Value = "TODO: populate CA summary rows"));

    public Task<string> GenerateBrokerWiseReportAsync(int ipoId) =>
        Task.FromResult(WriteExcel($"BrokerWise_{ipoId}", ws => ws.Cell(1, 1).Value = "TODO: populate broker-wise rows"));

    public Task<string> GenerateTerminalWiseReportAsync(int ipoId) =>
        Task.FromResult(WriteExcel($"TerminalWise_{ipoId}", ws => ws.Cell(1, 1).Value = "TODO: populate terminal-wise rows"));

    public async Task<string> GenerateNsmCommissionReportAsync(int ipoId, decimal rate, decimal gstPercent)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status != "REJECTED").ToListAsync();
        decimal totalAmount = bids.Sum(b => b.Amount);
        decimal commission = totalAmount * rate / 100m;
        decimal gst = commission * gstPercent / 100m;

        return WriteExcel($"Commission_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Total Amount"; ws.Cell(1, 2).Value = totalAmount;
            ws.Cell(2, 1).Value = $"Commission @ {rate}%"; ws.Cell(2, 2).Value = commission;
            ws.Cell(3, 1).Value = $"GST @ {gstPercent}%"; ws.Cell(3, 2).Value = gst;
            ws.Cell(4, 1).Value = "Total Payable"; ws.Cell(4, 2).Value = commission + gst;
        });
    }

    public async Task<string> GenerateRocListingReportAsync(int ipoId, DateTime dateOfAllotment)
    {
        var basis = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        return WriteExcel($"ROCListing_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Date of Allotment"; ws.Cell(1, 2).Value = dateOfAllotment.ToString("dd/MM/yyyy");
            ws.Cell(2, 1).Value = "Category"; ws.Cell(2, 2).Value = "Shares Allotted";
            int r = 3;
            foreach (var row in basis.GroupBy(b => b.Category))
            {
                ws.Cell(r, 1).Value = row.Key;
                ws.Cell(r, 2).Value = row.Sum(x => x.ShrAlto);
                r++;
            }
        });
    }

    public async Task<string> GenerateTechnicalRejectionReportAsync(int ipoId)
    {
        var rejections = await _dbService.Connection.Table<TechnicalRejection>()
            .Where(r => r.IPO_ID == ipoId).ToListAsync();

        return WriteExcel($"TechnicalRejection_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "FormNo"; ws.Cell(1, 2).Value = "PAN";
            ws.Cell(1, 3).Value = "CLID"; ws.Cell(1, 4).Value = "Reason"; ws.Cell(1, 5).Value = "Remark";
            int r = 2;
            foreach (var rej in rejections)
            {
                ws.Cell(r, 1).Value = rej.FormNo;
                ws.Cell(r, 2).Value = rej.PAN;
                ws.Cell(r, 3).Value = rej.CLID;
                ws.Cell(r, 4).Value = rej.ReasonCode;
                ws.Cell(r, 5).Value = rej.Remark;
                r++;
            }
        });
    }

    private string WriteExcel(string fileNameNoExt, Action<IXLWorksheet> populate)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Report");
        populate(ws);
        var path = Path.Combine(ExportRootFolder, $"{fileNameNoExt}_{DateTime.Now:yyyyMMddHHmm}.xlsx");
        workbook.SaveAs(path);
        return path;
    }
}
