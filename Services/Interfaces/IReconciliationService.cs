using IPOProcessingSystem.Models;

namespace IPOProcessingSystem.Services.Interfaces;

public class ReconSummaryResult
{
    public int TotalRecords { get; set; }
    public decimal TotalAmount { get; set; }
    public int MatchedRecords { get; set; }
    public decimal MatchedAmount { get; set; }
    public int UnmatchedRecords { get; set; }
    public decimal UnmatchedAmount { get; set; }
    public int ExcelTotalRecords { get; set; }
    public decimal ExcelTotalAmount { get; set; }
}

public interface IReconciliationService
{
    Task<List<Dictionary<string, object>>> ImportBankScheduleAsync(string filePath, Dictionary<string, string> columnMap);
    Task<ReconSummaryResult> CompareAsync(int ipoId, string bankCode, List<Dictionary<string, object>> bankRows);
    Task RejectRecordAsync(long applnsxRowId, string reason);
    Task RestoreRejectedRecordAsync(int rejectionId);
    Task UpdateBankCodeAsync(long applnsxRowId, string newBankCode, string newBankName);
    Task MarkBankAsNilAsync(int ipoId, string bankCode, bool isMemberAsba);
    Task<List<UnmatchRecord>> GetUnmatchRecordsAsync(int ipoId, string bankCode, string unmatchType);
    Task<ReconSummaryResult> GetSummaryAsync(int ipoId, string bankCode);
    Task<List<ColumnMapping>> GetColumnMappingAsync(string bankCode);
    Task SaveColumnMappingAsync(string bankCode, List<(string source, string target)> pairs);
}
