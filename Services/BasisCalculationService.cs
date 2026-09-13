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
        var listing = await _dbService.Connection.Table<ListingData>()
            .Where(l => l.IPO_ID == ipoId).FirstOrDefaultAsync();
        if (listing == null)
        {
            listing = new ListingData { IPO_ID = ipoId };
            await _dbService.Connection.InsertAsync(listing);
        }
        return true;
        // TODO: export to BSE/NSE format files here (ClosedXML) per
        // manual's "EXPORT FOR BSE" / "EXPORT NETBAS NSE" buttons.
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
        // TODO: cross-reference lucky numbers against serials assigned during
        // GenerateLuckyDrawRangesAsync and flip ALLOT vs DBAL on the matching
        // ApplnsxBid rows. Left as an extension point since the exact serial
        // <-> application mapping depends on your lot-wise serial numbering.
        int updated = 0;
        foreach (var (category, luckyNo) in results)
        {
            var lucky = await _dbService.Connection.Table<LuckyNumberAllocation>()
                .Where(l => l.IPO_ID == ipoId && l.Category == category).FirstOrDefaultAsync();
            if (lucky != null)
            {
                lucky.AllocatedFlag = true;
                await _dbService.Connection.UpdateAsync(lucky);
                updated++;
            }
        }
        return updated;
    }
}
