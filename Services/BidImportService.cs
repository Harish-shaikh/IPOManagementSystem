using System.Globalization;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Handles Module B: NSE/BSE Bid Upload & Day-Wise Staging.
/// Parses BSE ".DAT" (pipe/fixed-width per exchange spec) and NSE ".CSV" bid
/// files, and performs fast chunked batch-insert into Applnsx using
/// sqlite-net-pcl RunInTransactionAsync as required by the spec.
/// </summary>
public class BidImportService : IBidImportService
{
    private readonly DatabaseService _dbService;
    private const int BatchSize = 2000;

    public BidImportService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public async Task<List<ApplnsxBid>> ParseBidFileAsync(string filePath, int ipoId, string asbaType, bool isBankFile, string exchange = "")
    {
        var results = new List<ApplnsxBid>();
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Bid file not found", filePath);

        // Auto-detect delimiter: comma for CSV, pipe for DAT/fixed-width
        char delimiter = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ',' : '|';

        using var reader = new StreamReader(filePath);
        string? line;
        bool firstLine = true;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (firstLine && (line.StartsWith("Symbol", StringComparison.OrdinalIgnoreCase) ||
                               line.StartsWith("ClientId", StringComparison.OrdinalIgnoreCase)))
            {
                firstLine = false;
                continue; // skip header row if present
            }
            firstLine = false;

            var cols = line.Split(delimiter);
            if (cols.Length < 8) continue; // malformed row, skip

            // NOTE: column indices below are placeholders reflecting the manual's
            // grid (IPO ID, Client ID, Client Name, Order No, Book ID, Branch,
            // User ID, Symbol, Quantity ...). Map to your exact exchange spec.
            var bid = new ApplnsxBid
            {
                IPO_ID = ipoId,
                ClientId = SafeGet(cols, 0),
                ClientName = SafeGet(cols, 1),
                OrderNo = SafeGet(cols, 2),
                BookId = SafeGet(cols, 3),
                Branch = SafeGet(cols, 4),
                UserId = SafeGet(cols, 5),
                Symbol = SafeGet(cols, 6),
                Quantity = SafeParseInt(SafeGet(cols, 7)),
                Amount = SafeParseDecimal(SafeGet(cols, 8)),
                PAN = SafeGet(cols, 9),
                DPID = SafeGet(cols, 10),
                CLID = SafeGet(cols, 11),
                BankCode = SafeGet(cols, 12),
                Category = SafeGet(cols, 13, "RETAIL"),
                ASBA_Type = asbaType,
                IsBankFile = isBankFile,
                Status = "PENDING",
                EntryDate = DateTime.Now
            };
            results.Add(bid);
        }
        return results;
    }

    public async Task<ImportResult> BulkInsertAsync(List<ApplnsxBid> rows)
    {
        var result = new ImportResult();
        try
        {
            for (int i = 0; i < rows.Count; i += BatchSize)
            {
                var batch = rows.Skip(i).Take(BatchSize).ToList();
                await _dbService.Connection.RunInTransactionAsync(tran =>
                {
                    foreach (var row in batch)
                    {
                        tran.Insert(row);
                    }
                });
            }

            result.TotalRecords = rows.Count;
            result.TotalQuantity = rows.Sum(r => r.Quantity);
            result.Success = true;
            result.Message = $"{result.TotalRecords} records / {result.TotalQuantity} shares uploaded successfully.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Upload failed: {ex.Message}";
        }
        return result;
    }

    public async Task<int> ClearDataAsync(int ipoId, string? asbaType = null)
    {
        var query = _dbService.Connection.Table<ApplnsxBid>().Where(b => b.IPO_ID == ipoId);
        var toDelete = asbaType == null
            ? await query.ToListAsync()
            : (await query.ToListAsync()).Where(b => b.ASBA_Type == asbaType).ToList();

        int count = 0;
        await _dbService.Connection.RunInTransactionAsync(tran =>
        {
            foreach (var row in toDelete)
            {
                tran.Delete(row);
                count++;
            }
        });
        return count;
    }

    public async Task<int> MapDummyIfscAsync(int ipoId)
    {
        var mappings = await _dbService.Connection.Table<DummyIfscMapping>()
            .Where(m => m.IPO_ID == ipoId).ToListAsync();
        if (mappings.Count == 0) return 0;

        var missingIfscBids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && (b.BankCode == null || b.BankCode == string.Empty))
            .ToListAsync();

        int updated = 0;
        foreach (var bid in missingIfscBids)
        {
            var match = mappings.FirstOrDefault(m => m.IFSC_CODE == bid.DPID); // adapt key as per real IFSC field
            if (match != null)
            {
                bid.BankCode = match.BANK_CODE;
                await _dbService.Connection.UpdateAsync(bid);
                updated++;
            }
        }
        return updated;
    }

    private static string SafeGet(string[] arr, int idx, string fallback = "")
        => idx < arr.Length ? arr[idx].Trim() : fallback;

    private static int SafeParseInt(string val)
        => int.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : 0;

    private static decimal SafeParseDecimal(string val)
        => decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
