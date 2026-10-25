namespace IPOProcessingSystem.Services.Interfaces;

public interface IReportService
{
    string ExportRootFolder { get; set; }
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
    Task<string> GenerateAlloteesMarkingReportAsync(int ipoId);
    Task<string> GeneratePriceDiscoveryAsync(int ipoId);
    Task<string> GenerateFircReportAsync(int ipoId);
    Task<string> GenerateSmsDataAsync(int ipoId);
    Task<string> GenerateCityStateWiseAsync(int ipoId);
    Task<string> GenerateOnePercentAsync(int ipoId);
    Task<string> GenerateMergeBookAsync(int ipoId);
    Task<string> GenerateRefundCalculationAsync(int ipoId);
    Task<string> GenerateValidApplicantsAsync(int ipoId);
}

