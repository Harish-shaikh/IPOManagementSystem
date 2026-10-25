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
    private readonly INetworkPathService _networkPathService;
    private readonly AppState _appState;

    public string ExportRootFolder { get; set; } =
        Path.Combine(FileSystem.AppDataDirectory, "Exports");

    public ReportService(DatabaseService dbService, INetworkPathService networkPathService, AppState appState)
    {
        _dbService = dbService;
        _networkPathService = networkPathService;
        _appState = appState;
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

    public async Task<string> GenerateAlloteesMarkingReportAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.AllottedShares > 0).ToListAsync();

        return WriteExcel($"AlloteesMarking_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Application No";
            ws.Cell(1, 2).Value = "PAN";
            ws.Cell(1, 3).Value = "Client ID / DP ID";
            ws.Cell(1, 4).Value = "Depository";
            ws.Cell(1, 5).Value = "Category";
            ws.Cell(1, 6).Value = "Shares Applied";
            ws.Cell(1, 7).Value = "Shares Allotted";
            ws.Cell(1, 8).Value = "Marking Status";

            int r = 2;
            foreach (var b in bids)
            {
                ws.Cell(r, 1).Value = b.FormNo;
                ws.Cell(r, 2).Value = b.PAN;
                ws.Cell(r, 3).Value = b.ClientId;
                ws.Cell(r, 4).Value = !string.IsNullOrEmpty(b.DPID) && b.DPID.StartsWith("IN") ? "NSDL" : "CDSL";
                ws.Cell(r, 5).Value = b.Category;
                ws.Cell(r, 6).Value = b.Quantity;
                ws.Cell(r, 7).Value = b.AllottedShares;
                ws.Cell(r, 8).Value = "MARKED";
                r++;
            }
        });
    }

    public async Task<string> GeneratePriceDiscoveryAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status != "REJECTED").ToListAsync();

        return WriteExcel($"PriceDiscovery_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Price";
            ws.Cell(1, 2).Value = "Applications";
            ws.Cell(1, 3).Value = "Shares Applied";
            ws.Cell(1, 4).Value = "Amount (INR)";

            int r = 2;
            var groups = bids.GroupBy(b => b.Price).OrderByDescending(g => g.Key);
            foreach (var g in groups)
            {
                ws.Cell(r, 1).Value = g.Key;
                ws.Cell(r, 2).Value = g.Count();
                ws.Cell(r, 3).Value = g.Sum(x => x.Quantity);
                ws.Cell(r, 4).Value = g.Sum(x => x.Amount);
                r++;
            }
        }, "PriceDiscovery");
    }

    public async Task<string> GenerateFircReportAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && (b.Country != "INDIA" && b.Country != "" || b.Category == "NRI" || b.Category == "FII")).ToListAsync();

        return WriteExcel($"FIRC_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Form No";
            ws.Cell(1, 2).Value = "PAN";
            ws.Cell(1, 3).Value = "CLID";
            ws.Cell(1, 4).Value = "Category";
            ws.Cell(1, 5).Value = "Country";
            ws.Cell(1, 6).Value = "Shares";
            ws.Cell(1, 7).Value = "Amount (INR)";

            int r = 2;
            foreach (var b in bids)
            {
                ws.Cell(r, 1).Value = b.FormNo;
                ws.Cell(r, 2).Value = b.PAN;
                ws.Cell(r, 3).Value = b.CLID;
                ws.Cell(r, 4).Value = b.Category;
                ws.Cell(r, 5).Value = string.IsNullOrWhiteSpace(b.Country) ? "NON-RESIDENT" : b.Country;
                ws.Cell(r, 6).Value = b.Quantity;
                ws.Cell(r, 7).Value = b.Amount;
                r++;
            }
        }, "FircReport");
    }

    public async Task<string> GenerateSmsDataAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        return WriteExcel($"SMS_Data_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Form No";
            ws.Cell(1, 2).Value = "PAN";
            ws.Cell(1, 3).Value = "Mobile";
            ws.Cell(1, 4).Value = "Status";
            ws.Cell(1, 5).Value = "Allotted Qty";
            ws.Cell(1, 6).Value = "Refund Amount";

            int r = 2;
            foreach (var b in bids)
            {
                decimal refund = b.Status == "ALLOTTED"
                    ? Math.Max(0, b.Amount - (b.AllottedQuantity ?? 0) * b.Price)
                    : b.Amount;

                ws.Cell(r, 1).Value = b.FormNo;
                ws.Cell(r, 2).Value = b.PAN;
                ws.Cell(r, 3).Value = b.MobileNo;
                ws.Cell(r, 4).Value = b.Status;
                ws.Cell(r, 5).Value = b.AllottedQuantity ?? 0;
                ws.Cell(r, 6).Value = refund;
                r++;
            }
        }, "SmsData");
    }

    public async Task<string> GenerateCityStateWiseAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        return WriteExcel($"CityStateWise_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "State";
            ws.Cell(1, 2).Value = "City";
            ws.Cell(1, 3).Value = "Applications";
            ws.Cell(1, 4).Value = "Shares Applied";
            ws.Cell(1, 5).Value = "Amount (INR)";
            ws.Cell(1, 6).Value = "Allotted Apps";
            ws.Cell(1, 7).Value = "Allotted Shares";

            int r = 2;
            var groups = bids.GroupBy(b => new { State = string.IsNullOrWhiteSpace(b.State) ? "OTHER" : b.State, City = string.IsNullOrWhiteSpace(b.City) ? "OTHER" : b.City });
            foreach (var g in groups.OrderBy(x => x.Key.State).ThenBy(x => x.Key.City))
            {
                ws.Cell(r, 1).Value = g.Key.State;
                ws.Cell(r, 2).Value = g.Key.City;
                ws.Cell(r, 3).Value = g.Count();
                ws.Cell(r, 4).Value = g.Sum(x => x.Quantity);
                ws.Cell(r, 5).Value = g.Sum(x => x.Amount);
                ws.Cell(r, 6).Value = g.Count(x => x.Status == "ALLOTTED");
                ws.Cell(r, 7).Value = g.Sum(x => x.AllottedQuantity ?? 0);
                r++;
            }
        }, "CityStateWise");
    }

    public async Task<string> GenerateOnePercentAsync(int ipoId)
    {
        var company = await _dbService.Connection.Table<CompanyMaster>()
            .Where(c => c.IPO_ID == ipoId).FirstOrDefaultAsync();
        decimal issueSize = company?.IssueSize ?? 0;
        decimal onePercent = Math.Round(issueSize * 0.01m, 2);

        return WriteExcel($"1Percent_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Parameter";
            ws.Cell(1, 2).Value = "Value";

            ws.Cell(2, 1).Value = "IPO ID";
            ws.Cell(2, 2).Value = ipoId;

            ws.Cell(3, 1).Value = "Issuer Name";
            ws.Cell(3, 2).Value = company?.IssuerName ?? $"IPO_{ipoId}";

            ws.Cell(4, 1).Value = "Issue Size (INR)";
            ws.Cell(4, 2).Value = issueSize;

            ws.Cell(5, 1).Value = "1% Security Deposit (INR)";
            ws.Cell(5, 2).Value = onePercent;
        }, "1PercentReport");
    }

    public async Task<string> GenerateMergeBookAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        return WriteExcel($"MergeBook_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Order No";
            ws.Cell(1, 2).Value = "Form No";
            ws.Cell(1, 3).Value = "PAN";
            ws.Cell(1, 4).Value = "CLID";
            ws.Cell(1, 5).Value = "DPID";
            ws.Cell(1, 6).Value = "Category";
            ws.Cell(1, 7).Value = "Quantity";
            ws.Cell(1, 8).Value = "Price";
            ws.Cell(1, 9).Value = "Amount";
            ws.Cell(1, 10).Value = "Bank Code";
            ws.Cell(1, 11).Value = "Status";

            int r = 2;
            foreach (var b in bids)
            {
                ws.Cell(r, 1).Value = b.OrderNo;
                ws.Cell(r, 2).Value = b.FormNo;
                ws.Cell(r, 3).Value = b.PAN;
                ws.Cell(r, 4).Value = b.CLID;
                ws.Cell(r, 5).Value = b.DPID;
                ws.Cell(r, 6).Value = b.Category;
                ws.Cell(r, 7).Value = b.Quantity;
                ws.Cell(r, 8).Value = b.Price;
                ws.Cell(r, 9).Value = b.Amount;
                ws.Cell(r, 10).Value = b.BankCode;
                ws.Cell(r, 11).Value = b.Status;
                r++;
            }
        }, "MergeBook");
    }

    public async Task<string> GenerateRefundCalculationAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        return WriteExcel($"RefundCalculation_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Form No";
            ws.Cell(1, 2).Value = "PAN";
            ws.Cell(1, 3).Value = "CLID";
            ws.Cell(1, 4).Value = "Category";
            ws.Cell(1, 5).Value = "Applied Qty";
            ws.Cell(1, 6).Value = "Allotted Qty";
            ws.Cell(1, 7).Value = "Applied Amount";
            ws.Cell(1, 8).Value = "Allotted Amount";
            ws.Cell(1, 9).Value = "Refund Amount";

            int r = 2;
            foreach (var b in bids)
            {
                int alltdQty = b.AllottedQuantity ?? 0;
                decimal alltdAmt = alltdQty * b.Price;
                decimal refundAmt = Math.Max(0, b.Amount - alltdAmt);

                ws.Cell(r, 1).Value = b.FormNo;
                ws.Cell(r, 2).Value = b.PAN;
                ws.Cell(r, 3).Value = b.CLID;
                ws.Cell(r, 4).Value = b.Category;
                ws.Cell(r, 5).Value = b.Quantity;
                ws.Cell(r, 6).Value = alltdQty;
                ws.Cell(r, 7).Value = b.Amount;
                ws.Cell(r, 8).Value = alltdAmt;
                ws.Cell(r, 9).Value = refundAmt;
                r++;
            }
        }, "RefundCalculation");
    }

    public async Task<string> GenerateValidApplicantsAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status != "REJECTED").ToListAsync();

        return WriteExcel($"ValidApplicants_{ipoId}", ws =>
        {
            ws.Cell(1, 1).Value = "Form No";
            ws.Cell(1, 2).Value = "Order No";
            ws.Cell(1, 3).Value = "PAN";
            ws.Cell(1, 4).Value = "CLID";
            ws.Cell(1, 5).Value = "Category";
            ws.Cell(1, 6).Value = "Quantity";
            ws.Cell(1, 7).Value = "Amount";
            ws.Cell(1, 8).Value = "Application Date";

            int r = 2;
            foreach (var b in bids)
            {
                ws.Cell(r, 1).Value = b.FormNo;
                ws.Cell(r, 2).Value = b.OrderNo;
                ws.Cell(r, 3).Value = b.PAN;
                ws.Cell(r, 4).Value = b.CLID;
                ws.Cell(r, 5).Value = b.Category;
                ws.Cell(r, 6).Value = b.Quantity;
                ws.Cell(r, 7).Value = b.Amount;
                ws.Cell(r, 8).Value = b.ApplicationDate?.ToString("dd/MM/yyyy HH:mm:ss") ?? "";
                r++;
            }
        }, "ValidApplicants");
    }

    private string WriteExcel(string fileNameNoExt, Action<IXLWorksheet> populate, string subfolder = "Reports")
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Report");
        populate(ws);

        string folder = _networkPathService != null
            ? _networkPathService.GetExportPath(_appState?.ActiveIpoId ?? 0, _appState?.ActiveClientName, subfolder)
            : Path.Combine(ExportRootFolder, subfolder);

        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{fileNameNoExt}_{DateTime.Now:yyyyMMddHHmm}.xlsx");
        workbook.SaveAs(path);
        return path;
    }

}
