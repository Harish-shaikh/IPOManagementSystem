using ClosedXML.Excel;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Handles Module F: Basis of Allotment & Lucky Draw Engine.
/// Computes proportional share allocations per category (Retail/HNI/Market
/// Maker/Employee) using the PSIZE/LOT parameters configured per category
/// (BasisCategoryParameter, i.e. "IPO_TMAST" in the manual).
/// </summary>
public class BasisCalculationService : IBasisCalculationService
{
    private readonly DatabaseService _dbService;

    public BasisCalculationService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public async Task<List<BasisOfAllotment>> ProcessBasisAsync(int ipoId)
    {
        var parameters = await _dbService.Connection.Table<BasisCategoryParameter>()
            .Where(p => p.IPO_ID == ipoId).ToListAsync();
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status != "REJECTED").ToListAsync();

        var results = new List<BasisOfAllotment>();

        foreach (var param in parameters)
        {
            var categoryBids = bids.Where(b =>
                    string.Equals(b.Category, param.Category, StringComparison.OrdinalIgnoreCase))
                .GroupBy(b => b.Quantity)
                .OrderBy(g => g.Key)
                .ToList();

            int serial = 0;
            decimal totalAllocatedShares = 0;
            int totalApplications = categoryBids.Sum(g => g.Count());

            foreach (var group in categoryBids)
            {
                serial++;
                int applns = group.Count();
                decimal shrApld = group.Sum(g => g.Quantity);

                // Proportionate allocation: (category shares reserved / total shares applied in category) * shares applied by this bucket
                decimal totalCategoryShrApld = categoryBids.Sum(g => g.Sum(x => x.Quantity));
                decimal propAlloc = totalCategoryShrApld == 0
                    ? 0
                    : Math.Round(param.PSize1 * shrApld / totalCategoryShrApld, 0);

                int mlot = param.Lot > 0 ? param.Lot : 1;
                int noAlto = mlot > 0 ? (int)(propAlloc / mlot) : 0;
                decimal shralto = noAlto * mlot;
                decimal excess = propAlloc - shralto;

                var row = new BasisOfAllotment
                {
                    IPO_ID = ipoId,
                    Category = param.Category,
                    Serial = serial,
                    Applns = applns,
                    ShrApld = shrApld,
                    PropAlloc = propAlloc,
                    Proper = propAlloc, // pre-rounding value; refine as needed
                    MLot = mlot,
                    NoAlto = noAlto,
                    ShrAlto = shralto,
                    Numerator = 1,
                    Denomin = 1,
                    Excess = excess,
                    AppPercent = totalApplications == 0 ? 0 : Math.Round(100m * applns / totalApplications, 4),
                    ShrPercent = totalCategoryShrApld == 0 ? 0 : Math.Round(100m * shrApld / totalCategoryShrApld, 4)
                };

                totalAllocatedShares += shralto;
                results.Add(row);
                await _dbService.Connection.InsertAsync(row);
            }
        }

        return results;
    }

    public BasisOfAllotment RecalculateRow(BasisOfAllotment row)
    {
        int mlot = row.MLot > 0 ? row.MLot : 1;
        row.NoAlto = (int)(row.PropAlloc / mlot);
        row.ShrAlto = row.NoAlto * mlot;
        row.Excess = row.PropAlloc - row.ShrAlto;
        return row;
    }

    public async Task<bool> ApproveBasisAsync(int ipoId)
    {
        var rows = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        await _dbService.Connection.RunInTransactionAsync(tran =>
        {
            foreach (var r in rows)
            {
                r.IsExchangeApproved = true;
                tran.Update(r);
            }
        });

        var listing = await _dbService.Connection.Table<ListingData>()
            .Where(l => l.IPO_ID == ipoId).FirstOrDefaultAsync();
        if (listing == null)
        {
            listing = new ListingData { IPO_ID = ipoId };
            await _dbService.Connection.InsertAsync(listing);
        }

        await ExportBseFormatAsync(ipoId);
        await ExportNetBasNseAsync(ipoId);

        return true;
    }

    public async Task<string> ExportBseFormatAsync(int ipoId)
    {
        var rows = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var company = await _dbService.Connection.Table<CompanyMaster>()
            .Where(c => c.IPO_ID == ipoId).FirstOrDefaultAsync();
        string clientName = !string.IsNullOrWhiteSpace(company?.IssuerName) ? company.IssuerName : $"IPO_{ipoId}";

        var exportPath = Path.Combine(@"\\192.168.0.253\IDRIVE\ISSUE\EDP", clientName, "Basis");
        if (!Directory.Exists(@"\\192.168.0.253"))
            exportPath = Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{ipoId}", "Basis");
        Directory.CreateDirectory(exportPath);

        var fileName = Path.Combine(exportPath, $"{clientName}_BASIS_BSE.xlsx");
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Basis");

        var headers = new[] { "Category","Serial","CAT","APPLNS","SHRAPLD","PROPALLOC",
                               "PROPER","MLOT","NOALTD","SHRALTD","NUMERATOR","DENOMIN",
                               "EXCESS","SR","RDD","APPERCE","SHRPERCE" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.Category;
            ws.Cell(r, 2).Value = row.Serial;
            ws.Cell(r, 3).Value = row.ShrApld;
            ws.Cell(r, 4).Value = row.Applns;
            ws.Cell(r, 5).Value = row.ShrApld;
            ws.Cell(r, 6).Value = row.PropAlloc;
            ws.Cell(r, 7).Value = row.Proper;
            ws.Cell(r, 8).Value = row.MLot;
            ws.Cell(r, 9).Value = row.NoAlto;
            ws.Cell(r, 10).Value = row.ShrAlto;
            ws.Cell(r, 11).Value = row.Numerator;
            ws.Cell(r, 12).Value = row.Denomin;
            ws.Cell(r, 13).Value = row.Excess;
            ws.Cell(r, 14).Value = row.SR;
            ws.Cell(r, 15).Value = row.RDD;
            ws.Cell(r, 16).Value = row.AppPercent;
            ws.Cell(r, 17).Value = row.ShrPercent;
            r++;
        }
        wb.SaveAs(fileName);
        return fileName;
    }

    public async Task<string> ExportNetBasNseAsync(int ipoId)
    {
        var rows = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var company = await _dbService.Connection.Table<CompanyMaster>()
            .Where(c => c.IPO_ID == ipoId).FirstOrDefaultAsync();
        string clientName = !string.IsNullOrWhiteSpace(company?.IssuerName) ? company.IssuerName : $"IPO_{ipoId}";

        var exportPath = Path.Combine(@"\\192.168.0.253\IDRIVE\ISSUE\EDP", clientName, "Basis");
        if (!Directory.Exists(@"\\192.168.0.253"))
            exportPath = Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{ipoId}", "Basis");
        Directory.CreateDirectory(exportPath);

        var fileName = Path.Combine(exportPath, $"{clientName}_NETBAS_NSE.txt");
        var lines = new List<string> { "CATEGORY|APPLNS|SHRAPLD|ALLOTTED|RATIO|MLOT" };
        foreach (var g in rows.GroupBy(r => r.Category))
        {
            decimal totalApld = g.Sum(r => r.ShrApld);
            decimal totalAlto = g.Sum(r => r.ShrAlto);
            decimal ratio = totalApld > 0 ? totalAlto / totalApld : 0;
            int mlot = g.FirstOrDefault()?.MLot ?? 1;
            lines.Add($"{g.Key}|{g.Sum(r => r.Applns)}|{totalApld}|{totalAlto}|{ratio:F6}|{mlot}");
        }
        await File.WriteAllLinesAsync(fileName, lines);
        return fileName;
    }

    public async Task<List<LuckyNumberAllocation>> GenerateLuckyDrawRangesAsync(int ipoId)
    {
        var basisRows = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var ranges = new List<LuckyNumberAllocation>();
        var byCategory = basisRows.GroupBy(b => b.Category);
        foreach (var group in byCategory)
        {
            int from = 1;
            int to = (int)group.Sum(g => g.Applns);
            var lucky = new LuckyNumberAllocation
            {
                IPO_ID = ipoId,
                Category = group.Key,
                Cat = group.Sum(g => g.ShrApld),
                SearchType = "Type 1",
                SerialFrom = from,
                SerialTo = to,
                AllocatedFlag = false
            };
            await _dbService.Connection.InsertAsync(lucky);
            ranges.Add(lucky);
        }
        return ranges;
    }

    public async Task<int> ApplyLuckyDrawResultsAsync(int ipoId, List<(string category, int luckyNo)> results)
    {
        int updated = 0;
        foreach (var (category, luckyNo) in results)
        {
            // Get all bids for this category, ordered by entry order
            var catBids = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == ipoId &&
                            b.Category == category &&
                            b.Status != "REJECTED")
                .ToListAsync();

            if (catBids.Count == 0) continue;

            // Get basis rows to know how many get allotted
            var basisRows = await _dbService.Connection.Table<BasisOfAllotment>()
                .Where(b => b.IPO_ID == ipoId && b.Category == category)
                .ToListAsync();
            int noAllotted = basisRows.Sum(r => r.NoAlto);

            // Lucky number = starting serial for allotment; wrap around if needed
            var ordered = catBids.Select((bid, idx) => (bid, serial: idx + 1)).ToList();
            var allottedSerials = new HashSet<int>();
            for (int i = 0; i < noAllotted && i < ordered.Count; i++)
            {
                int serial = ((luckyNo - 1 + i) % ordered.Count) + 1;
                allottedSerials.Add(serial);
            }

            await _dbService.Connection.RunInTransactionAsync(tran =>
            {
                foreach (var (bid, serial) in ordered)
                {
                    if (allottedSerials.Contains(serial))
                    {
                        bid.Status = "ALLOTTED";
                        bid.AllottedQuantity = bid.Quantity;
                    }
                    else
                    {
                        bid.Status = "REFUND";
                        bid.AllottedQuantity = 0;
                    }
                    tran.Update(bid);
                    updated++;
                }
            });

            // Update the LuckyNumberAllocation record
            var lucky = await _dbService.Connection.Table<LuckyNumberAllocation>()
                .Where(l => l.IPO_ID == ipoId && l.Category == category).FirstOrDefaultAsync();
            if (lucky != null)
            {
                lucky.AllocatedFlag = true;
                await _dbService.Connection.UpdateAsync(lucky);
            }
        }
        return updated;
    }
}
