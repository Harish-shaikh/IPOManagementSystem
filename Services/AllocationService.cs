using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Runs the allotment/refund process:
///   1. Reads basis rows (post Exchange Approved).
///   2. Applies lucky draw results to decide which serial applicants get allotted.
///   3. Writes Allocation rows per category with Allot and DBal (refund).
/// </summary>
public class AllocationService : IAllocationService
{
    private readonly DatabaseService _dbService;

    public AllocationService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public async Task<List<Allocation>> RunAllotmentAsync(int ipoId)
    {
        var basisRows = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status != "REJECTED").ToListAsync();
        var luckyNos = await _dbService.Connection.Table<LuckyNumberAllocation>()
            .Where(l => l.IPO_ID == ipoId).ToListAsync();

        // Delete old allocations for re-run idempotency
        var old = await _dbService.Connection.Table<Allocation>()
            .Where(a => a.IPO_ID == ipoId).ToListAsync();
        await _dbService.Connection.RunInTransactionAsync(tran =>
        {
            foreach (var o in old) tran.Delete(o);
        });

        var results = new List<Allocation>();
        var categories = basisRows.GroupBy(b => b.Category);

        foreach (var catGroup in categories)
        {
            var catBids = bids.Where(b =>
                string.Equals(b.Category, catGroup.Key, StringComparison.OrdinalIgnoreCase)).ToList();

            decimal totalShares = catBids.Sum(b => b.Quantity);
            decimal totalAmount = catBids.Sum(b => b.Amount);

            // Shares to allot = sum of ShrAlto for this category
            decimal sharesToAllot = catGroup.Sum(r => r.ShrAlto);

            // DBal = total applied amount - allotted amount (proportion)
            decimal ratio = totalShares > 0 ? sharesToAllot / totalShares : 0;
            decimal allottedAmount = totalAmount * ratio;
            decimal dbal = totalAmount - allottedAmount;

            var alloc = new Allocation
            {
                IPO_ID = ipoId,
                Category = catGroup.Key,
                Debent = totalShares,
                Appl = catBids.Count,
                Allot = sharesToAllot,
                DBal = Math.Round(dbal, 2)
            };

            await _dbService.Connection.InsertAsync(alloc);
            results.Add(alloc);

            // Mark individual bid rows as ALLOTTED or mark refund
            foreach (var bid in catBids)
            {
                bid.Status = "ALLOTTED";
                await _dbService.Connection.UpdateAsync(bid);
            }
        }

        return results;
    }

    public async Task<List<Allocation>> RunRefundProcessAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var allocations = await _dbService.Connection.Table<Allocation>()
            .Where(a => a.IPO_ID == ipoId).ToListAsync();

        foreach (var alloc in allocations)
        {
            var catBids = bids.Where(b =>
                string.Equals(b.Category, alloc.Category, StringComparison.OrdinalIgnoreCase)).ToList();

            decimal allottedAmount = catBids
                .Where(b => b.Status == "ALLOTTED")
                .Sum(b => b.Amount);
            decimal totalAmount = catBids.Sum(b => b.Amount);

            alloc.DBal = Math.Round(totalAmount - allottedAmount, 2);
            await _dbService.Connection.UpdateAsync(alloc);
        }
        return allocations;
    }

    public async Task<List<Allocation>> GetAllocationsAsync(int ipoId)
        => await _dbService.Connection.Table<Allocation>()
               .Where(a => a.IPO_ID == ipoId).ToListAsync();

    public async Task ResetAllocationAsync(int ipoId)
    {
        var old = await _dbService.Connection.Table<Allocation>()
            .Where(a => a.IPO_ID == ipoId).ToListAsync();
        await _dbService.Connection.RunInTransactionAsync(tran =>
        {
            foreach (var o in old) tran.Delete(o);
        });
        // Revert bid statuses to PENDING
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status == "ALLOTTED").ToListAsync();
        foreach (var bid in bids)
        {
            bid.Status = "PENDING";
            await _dbService.Connection.UpdateAsync(bid);
        }
    }
}
