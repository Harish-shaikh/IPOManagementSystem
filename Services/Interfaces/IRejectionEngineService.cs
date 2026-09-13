using IPOProcessingSystem.Models;

namespace IPOProcessingSystem.Services.Interfaces;

public interface IRejectionEngineService
{
    Task<List<TechnicalRejection>> CheckPromoterPanAsync(int ipoId, IEnumerable<string> promoterPans);
    Task<List<TechnicalRejection>> CheckSebiDebarredAsync(int ipoId, IEnumerable<string> debarredPans);
    Task<List<TechnicalRejection>> CheckHniCutOffAsync(int ipoId, TimeSpan cutOffTime, DateTime closingDate);
    Task<List<TechnicalRejection>> CheckUsaApplicantAsync(int ipoId, IEnumerable<string> foreignKeywords);
    Task<List<TechnicalRejection>> CheckDuplicatePanAsync(int ipoId);
    Task<List<TechnicalRejection>> CheckMultipleApplicationsAsync(int ipoId, int maxAllowed = 1);
    Task<List<TechnicalRejection>> CheckMinorDetailAsync(int ipoId);
    Task<List<TechnicalRejection>> CheckPanMismatchAsync(int ipoId);
    Task<int> UpdateMarketMakerCategoryAsync(int ipoId, IEnumerable<string> orderNos);
    Task<int> ConfirmRejectionsAsync(int ipoId);
}
