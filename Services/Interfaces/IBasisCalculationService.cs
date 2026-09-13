using IPOProcessingSystem.Models;

namespace IPOProcessingSystem.Services.Interfaces;

public interface IBasisCalculationService
{
    /// <summary>Generates raw basis-of-allotment rows per category using BasisCategoryParameter values (PSIZE/LOT).</summary>
    Task<List<BasisOfAllotment>> ProcessBasisAsync(int ipoId);

    /// <summary>Recalculates PropAlloc / ShrAlto / Excess / percentages for a single basis row after a manual edit.</summary>
    BasisOfAllotment RecalculateRow(BasisOfAllotment row);

    /// <summary>Locks in the basis as Exchange Approved (final, ready for BSE/NSE export).</summary>
    Task<bool> ApproveBasisAsync(int ipoId);

    /// <summary>Generates the lottery (lucky draw) number ranges per category/search-type.</summary>
    Task<List<LuckyNumberAllocation>> GenerateLuckyDrawRangesAsync(int ipoId);

    /// <summary>Applies imported lucky-draw sequence numbers, flips AllocatedFlag, and finalizes ALLOT vs DBAL.</summary>
    Task<int> ApplyLuckyDrawResultsAsync(int ipoId, List<(string category, int luckyNo)> results);
}
