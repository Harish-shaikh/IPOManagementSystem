using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Handles Module E: Technical Rejection Engine.
/// Rule-based automated filtering per the manual's checklist
/// (Promoter PAN, SEBI Debarred, HNI Cut-off, USA Applicant, Duplicate PAN,
/// Minor Details, Market Maker, More-than-one application).
/// </summary>
public class RejectionEngineService : IRejectionEngineService
{
    private readonly DatabaseService _dbService;

    public RejectionEngineService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public async Task<List<TechnicalRejection>> CheckPromoterPanAsync(int ipoId, IEnumerable<string> promoterPans)
    {
        var pans = promoterPans.Select(p => p.Trim().ToUpperInvariant()).ToHashSet();
        var matches = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var rejections = new List<TechnicalRejection>();
        foreach (var m in matches.Where(b => pans.Contains(b.PAN.ToUpperInvariant())))
        {
            rejections.Add(await InsertRejection(ipoId, m, "PromoterPAN", "Matched Promoter PAN list"));
        }
        return rejections;
    }

    public async Task<List<TechnicalRejection>> CheckSebiDebarredAsync(int ipoId, IEnumerable<string> debarredPans)
    {
        var dbPans = (await _dbService.Connection.Table<SebiDebarredPan>().ToListAsync())
                      .Select(p => p.PAN.ToUpperInvariant()).ToHashSet();
        foreach (var p in debarredPans.Select(p => p.Trim().ToUpperInvariant()))
            dbPans.Add(p);

        var matches = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var rejections = new List<TechnicalRejection>();
        foreach (var m in matches.Where(b => dbPans.Contains(b.PAN.ToUpperInvariant())))
        {
            rejections.Add(await InsertRejection(ipoId, m, "SEBIDebarred", "Matched SEBI Debarred list"));
        }
        return rejections;
    }

    public async Task<List<TechnicalRejection>> CheckHniCutOffAsync(int ipoId, TimeSpan cutOffTime, DateTime closingDate)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Category == "HNI").ToListAsync();

        var rejections = new List<TechnicalRejection>();
        foreach (var bid in bids.Where(b =>
            b.ApplicationDate.HasValue &&
            b.ApplicationDate.Value.Date == closingDate.Date &&
            b.ApplicationDate.Value.TimeOfDay > cutOffTime))
        {
            rejections.Add(await InsertRejection(ipoId, bid, "HNICutOff", $"Applied after {cutOffTime} on closing day"));
        }
        return rejections;
    }

    public async Task<List<TechnicalRejection>> CheckUsaApplicantAsync(int ipoId, IEnumerable<string> foreignKeywords)
    {
        var keywords = foreignKeywords.Select(k => k.ToUpperInvariant()).ToList();
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var rejections = new List<TechnicalRejection>();
        foreach (var bid in bids)
        {
            var addressBlob = $"{bid.Address1} {bid.Address2} {bid.Address3} {bid.City} {bid.Country}"
                              .ToUpperInvariant();
            if (keywords.Any(k => addressBlob.Contains(k)))
            {
                rejections.Add(await InsertRejection(ipoId, bid, "USAApplicant",
                    $"US address detected: {bid.City}, {bid.Country}"));
            }
        }
        return rejections;
    }

    public async Task<List<TechnicalRejection>> CheckDuplicatePanAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var rejections = new List<TechnicalRejection>();
        var duplicateGroups = bids.GroupBy(b => b.PAN).Where(g => g.Count() > 1);
        foreach (var group in duplicateGroups)
        {
            // Keep the first, reject the rest (customize per your business rule)
            foreach (var dup in group.Skip(1))
            {
                rejections.Add(await InsertRejection(ipoId, dup, "DuplicatePAN", $"Duplicate of PAN {group.Key}"));
            }
        }
        return rejections;
    }

    public async Task<List<TechnicalRejection>> CheckMultiplePanAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var rejections = new List<TechnicalRejection>();
        // Group by CLID; if same CLID has more than 1 distinct PAN -> flag all except first
        var groups = bids.GroupBy(b => b.CLID)
                         .Where(g => g.Select(x => x.PAN).Distinct().Count() > 1);
        foreach (var group in groups)
            foreach (var bid in group.Skip(1))
                rejections.Add(await InsertRejection(ipoId, bid, "MultiplePAN",
                    $"CLID {group.Key} has multiple PANs"));
        return rejections;
    }

    public async Task<List<TechnicalRejection>> CheckMultipleApplicationsAsync(int ipoId, int maxAllowed = 1)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var rejections = new List<TechnicalRejection>();
        // Group by MergeAcNo (if available), otherwise fall back to CLID
        var groups = bids
            .GroupBy(b => string.IsNullOrWhiteSpace(b.MergeAcNo) ? b.CLID : b.MergeAcNo)
            .Where(g => g.Count() > maxAllowed);
        foreach (var group in groups)
            foreach (var extra in group.Skip(maxAllowed))
                rejections.Add(await InsertRejection(ipoId, extra, "MoreThanOne",
                    $"More than {maxAllowed} application(s) for account {group.Key}"));
        return rejections;
    }

    public async Task<List<TechnicalRejection>> CheckMinorDetailAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var rejections = new List<TechnicalRejection>();
        foreach (var bid in bids)
        {
            // OK1 = '$' means this belongs to a DIFFERENT rejection type - skip
            // OK1 has flag indicating genuine minor applicant
            if (!string.IsNullOrEmpty(bid.OK1) && bid.OK1 != "$")
            {
                rejections.Add(await InsertRejection(ipoId, bid, "MinorDetail",
                    $"Minor applicant flag: {bid.OK1}"));
            }
        }
        return rejections;
    }

    public async Task<List<TechnicalRejection>> CheckPanMismatchAsync(int ipoId)
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        var rejections = new List<TechnicalRejection>();
        foreach (var bid in bids)
        {
            // Standard Indian PAN format: 5 letters, 4 digits, 1 letter (10 characters total)
            var pan = bid.PAN?.Trim().ToUpperInvariant() ?? string.Empty;
            bool isValid = pan.Length == 10 &&
                           pan.Take(5).All(char.IsLetter) &&
                           pan.Skip(5).Take(4).All(char.IsDigit) &&
                           char.IsLetter(pan[9]);
            if (!isValid)
            {
                rejections.Add(await InsertRejection(ipoId, bid, "PANMismatch", $"Invalid PAN format: {bid.PAN}"));
            }
        }
        return rejections;
    }

    public async Task<int> UpdateMarketMakerCategoryAsync(int ipoId, IEnumerable<string> orderNos)
    {
        var set = orderNos.ToHashSet();
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId).ToListAsync();

        int updated = 0;
        await _dbService.Connection.RunInTransactionAsync(tran =>
        {
            foreach (var bid in bids.Where(b => set.Contains(b.OrderNo)))
            {
                bid.Category = "MARKET MAKER";
                tran.Update(bid);
                updated++;
            }
        });
        return updated;
    }

    public async Task<int> ConfirmRejectionsAsync(int ipoId)
    {
        var rejections = await _dbService.Connection.Table<TechnicalRejection>()
            .Where(r => r.IPO_ID == ipoId).ToListAsync();

        int confirmed = 0;
        await _dbService.Connection.RunInTransactionAsync(tran =>
        {
            foreach (var rej in rejections)
            {
                var appln = tran.Table<ApplnsxBid>()
                    .Where(a => a.IPO_ID == ipoId && a.CLID == rej.CLID && a.PAN == rej.PAN)
                    .FirstOrDefault();
                if (appln != null && appln.Status != "REJECTED")
                {
                    appln.Status = "REJECTED";
                    appln.RejectionCode = rej.ReasonCode;
                    tran.Update(appln);
                    confirmed++;
                }
            }
        });
        return confirmed;
    }

    private async Task<TechnicalRejection> InsertRejection(int ipoId, ApplnsxBid bid, string reasonCode, string remark)
    {
        var rejection = new TechnicalRejection
        {
            IPO_ID = ipoId,
            FormNo = bid.OrderNo,
            PAN = bid.PAN,
            CLID = bid.CLID,
            ReasonCode = reasonCode,
            Remark = remark
        };
        await _dbService.Connection.InsertAsync(rejection);
        return rejection;
    }
}
