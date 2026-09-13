using IPOProcessingSystem.Models;

namespace IPOProcessingSystem.Services.Interfaces;

public interface IAllocationService
{
    /// <summary>Run allotment calculation using basis + lucky draw results.</summary>
    Task<List<Allocation>> RunAllotmentAsync(int ipoId);
    /// <summary>Get existing allocation results.</summary>
    Task<List<Allocation>> GetAllocationsAsync(int ipoId);
    /// <summary>Reset allocation for re-processing.</summary>
    Task ResetAllocationAsync(int ipoId);
}
