using IPOProcessingSystem.Models;

namespace IPOProcessingSystem.Services.Interfaces;

public class ImportResult
{
    public int TotalRecords { get; set; }
    public decimal TotalQuantity { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public interface IBidImportService
{
    /// <summary>Parses a bid file and stages rows into memory for preview.</summary>
    Task<List<ApplnsxBid>> ParseBidFileAsync(string filePath, int ipoId, string asbaType, bool isBankFile, string exchange = "");

    /// <summary>Bulk-inserts staged rows into Applnsx inside a single SQLite transaction (fast batch).</summary>
    Task<ImportResult> BulkInsertAsync(List<ApplnsxBid> rows);

    /// <summary>Clears current day's staged Applnsx data for the IPO before a fresh re-upload.</summary>
    Task<int> ClearDataAsync(int ipoId, string? asbaType = null);

    /// <summary>Resolves dummy/missing IFSC codes to a BankCode using DummyIfscMapping.</summary>
    Task<int> MapDummyIfscAsync(int ipoId);
}
