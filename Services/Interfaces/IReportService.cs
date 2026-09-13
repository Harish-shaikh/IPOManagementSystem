namespace IPOProcessingSystem.Services.Interfaces;

public interface IReportService
{
    Task<string> GenerateBankSummaryAsync(int ipoId, bool bankAsba);
    Task<string> GenerateRefundSummaryAsync(int ipoId);
    Task<string> GenerateRefundDetailAsync(int ipoId);
    Task<string> GenerateAllotteesReportAsync(int ipoId, bool top200Only);
    Task<string> GenerateCaSummaryAsync(int ipoId);
    Task<string> GenerateBrokerWiseReportAsync(int ipoId);
    Task<string> GenerateTerminalWiseReportAsync(int ipoId);
    Task<string> GenerateNsmCommissionReportAsync(int ipoId, decimal rate, decimal gstPercent);
    Task<string> GenerateRocListingReportAsync(int ipoId, DateTime dateOfAllotment);
    Task<string> GenerateTechnicalRejectionReportAsync(int ipoId);
}
