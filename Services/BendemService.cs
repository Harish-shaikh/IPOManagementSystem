using ClosedXML.Excel;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Handles CDSL/NSDL BenDem file generation, verification, and merge (T and T+1 steps).
/// </summary>
public class BendemService : IBendemService
{
    private readonly DatabaseService _dbService;

    public BendemService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    public async Task<string> GenerateCdslBendemAsync(int ipoId, string sequenceNo, string exportPath)
    {
        Directory.CreateDirectory(exportPath);
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status != "REJECTED").ToListAsync();

        var fileName = Path.Combine(exportPath, $"{sequenceNo}.OUT");
        await using (var writer = new StreamWriter(fileName))
        {
            await writer.WriteLineAsync($"# CDSL BenDem - IPO {ipoId} - Seq {sequenceNo}");
            foreach (var bid in bids)
            {
                await writer.WriteLineAsync(
                    $"{bid.DPID}|{bid.CLID}|{bid.PAN}|{bid.Quantity}|{bid.Amount:F2}|{bid.Category}");
            }
        }

        var record = new BendemFile
        {
            IPO_ID = ipoId,
            Depository = "CDSL",
            SequenceNo = sequenceNo,
            GeneratedPath = fileName
        };
        await _dbService.Connection.InsertAsync(record);
        return fileName;
    }

    public async Task<string> GenerateNsdlBendemAsync(int ipoId, string exportPath)
    {
        Directory.CreateDirectory(exportPath);
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status != "REJECTED").ToListAsync();

        // Auto-generate sequence: find max existing NSDL sequence
        var existing = await _dbService.Connection.Table<BendemFile>()
            .Where(f => f.IPO_ID == ipoId && f.Depository == "NSDL").ToListAsync();
        int seqNo = existing.Count + 1;
        var seqStr = $"NSDL_{ipoId}_{seqNo:D3}";

        var fileName = Path.Combine(exportPath, $"{seqStr}.TXT");
        await using (var writer = new StreamWriter(fileName))
        {
            await writer.WriteLineAsync($"# NSDL BenDem - IPO {ipoId}");
            foreach (var bid in bids)
            {
                await writer.WriteLineAsync(
                    $"{bid.DPID}|{bid.CLID}|{bid.PAN}|{bid.Quantity}|{bid.Amount:F2}|{bid.Category}");
            }
            // NSDL footer lines (these are stripped before verification)
            await writer.WriteLineAsync("EOF");
            await writer.WriteLineAsync("END OF BENDEM DATA");
        }

        var record = new BendemFile
        {
            IPO_ID = ipoId,
            Depository = "NSDL",
            SequenceNo = seqStr,
            GeneratedPath = fileName
        };
        await _dbService.Connection.InsertAsync(record);
        return fileName;
    }

    public async Task<bool> VerifyCdslBendemAsync(int ipoId, string verifiedFilePath)
    {
        var record = await _dbService.Connection.Table<BendemFile>()
            .Where(f => f.IPO_ID == ipoId && f.Depository == "CDSL" && !f.VerifiedFlag)
            .FirstOrDefaultAsync();
        if (record == null) return false;

        record.VerifiedPath = verifiedFilePath;
        record.VerifiedFlag = true;
        record.VerifiedOn = DateTime.Now;
        await _dbService.Connection.UpdateAsync(record);
        return true;
    }

    public async Task<bool> VerifyNsdlBendemAsync(int ipoId, string verifiedFilePath)
    {
        // Auto-strip last 2 footer lines (EOF / END OF BENDEM DATA) per spec rule
        var lines = (await File.ReadAllLinesAsync(verifiedFilePath)).ToList();
        while (lines.Count > 0 &&
               (lines[^1].Trim().Equals("EOF", StringComparison.OrdinalIgnoreCase) ||
                lines[^1].Trim().StartsWith("END OF", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(lines[^1])))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var cleanPath = verifiedFilePath.EndsWith(".stripped.txt", StringComparison.OrdinalIgnoreCase)
            ? verifiedFilePath
            : verifiedFilePath + ".stripped.txt";
        await File.WriteAllLinesAsync(cleanPath, lines);

        var record = await _dbService.Connection.Table<BendemFile>()
            .Where(f => f.IPO_ID == ipoId && f.Depository == "NSDL" && !f.VerifiedFlag)
            .FirstOrDefaultAsync();
        if (record == null) return false;

        record.VerifiedPath = cleanPath;
        record.VerifiedFlag = true;
        record.VerifiedOn = DateTime.Now;
        await _dbService.Connection.UpdateAsync(record);
        return true;
    }

    public async Task<List<string>> MergeBendemAsync(int ipoId, string exportPath)
    {
        var company = await _dbService.Connection.Table<CompanyMaster>()
            .Where(c => c.IPO_ID == ipoId).FirstOrDefaultAsync();
        var clientName = company?.IssuerName ?? $"IPO_{ipoId}";
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == ipoId && b.Status != "REJECTED").ToListAsync();

        var produced = new List<string>();

        // 1. _IPO_Validation.xlsx in IPO_Validation directory
        var parentDir = Path.GetDirectoryName(exportPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? exportPath;
        var validationDir = Path.Combine(parentDir, "IPO_Validation");
        Directory.CreateDirectory(validationDir);
        var validationPath = Path.Combine(validationDir, $"{clientName}_IPO_Validation.xlsx");
        WriteValidationReport(validationPath, bids);
        produced.Add(validationPath);

        // 2. Updation of demographic data
        await _dbService.Connection.RunInTransactionAsync(tran =>
        {
            foreach (var bid in bids)
            {
                if (string.IsNullOrWhiteSpace(bid.MergeAcNo))
                {
                    bid.MergeAcNo = $"{bid.DPID}{bid.CLID}";
                    tran.Update(bid);
                }
            }
        });

        // 3. _BID_ERROR_DATA.xlsx in Merge_Bid_File directory
        Directory.CreateDirectory(exportPath);
        var errorBids = bids.Where(b => string.IsNullOrEmpty(b.PAN) || b.CLID.Length < 8).ToList();
        var errorPath = Path.Combine(exportPath, $"{clientName}_BID_ERROR_DATA.xlsx");
        WriteBidErrorReport(errorPath, errorBids);
        produced.Add(errorPath);

        // 4. _BSE_BOOK_DATA.xlsx / _NSE_BOOK_DATA.xlsx in Merge_Bid_File directory
        var exchangePrefix = company?.Exchange?.Contains("NSE", StringComparison.OrdinalIgnoreCase) == true &&
                             company?.Exchange?.Contains("BSE", StringComparison.OrdinalIgnoreCase) != true
                             ? "NSE" : "BSE";
        var bookPath = Path.Combine(exportPath, $"{clientName}_{exchangePrefix}_BOOK_DATA.xlsx");
        WriteBookDataReport(bookPath, bids);
        produced.Add(bookPath);

        return produced;
    }

    public async Task<List<BendemFile>> GetBendemFilesAsync(int ipoId)
        => await _dbService.Connection.Table<BendemFile>().Where(f => f.IPO_ID == ipoId).ToListAsync();

    public async Task DeleteVerificationAsync(int ipoId, string depository)
    {
        var record = await _dbService.Connection.Table<BendemFile>()
            .Where(f => f.IPO_ID == ipoId && f.Depository == depository && f.VerifiedFlag)
            .FirstOrDefaultAsync();
        if (record == null) return;
        record.VerifiedPath = null;
        record.VerifiedFlag = false;
        record.VerifiedOn = null;
        await _dbService.Connection.UpdateAsync(record);
    }

    // ── Excel helpers ──────────────────────────────────────────────────────────

    private static void WriteValidationReport(string path, List<ApplnsxBid> bids)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Validation");
        ws.Cell(1, 1).Value = "RowId"; ws.Cell(1, 2).Value = "PAN";
        ws.Cell(1, 3).Value = "CLID";  ws.Cell(1, 4).Value = "DPID";
        ws.Cell(1, 5).Value = "Qty";   ws.Cell(1, 6).Value = "Amount";
        ws.Cell(1, 7).Value = "Status";
        int r = 2;
        foreach (var b in bids)
        {
            ws.Cell(r, 1).Value = b.RowId; ws.Cell(r, 2).Value = b.PAN;
            ws.Cell(r, 3).Value = b.CLID;  ws.Cell(r, 4).Value = b.DPID;
            ws.Cell(r, 5).Value = b.Quantity; ws.Cell(r, 6).Value = b.Amount;
            ws.Cell(r, 7).Value = b.Status;
            r++;
        }
        wb.SaveAs(path);
    }

    private static void WriteBidErrorReport(string path, List<ApplnsxBid> bids)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("BidErrors");
        ws.Cell(1, 1).Value = "RowId"; ws.Cell(1, 2).Value = "PAN";
        ws.Cell(1, 3).Value = "CLID";  ws.Cell(1, 4).Value = "Error";
        int r = 2;
        foreach (var b in bids)
        {
            ws.Cell(r, 1).Value = b.RowId; ws.Cell(r, 2).Value = b.PAN;
            ws.Cell(r, 3).Value = b.CLID;
            ws.Cell(r, 4).Value = string.IsNullOrEmpty(b.PAN) ? "Missing PAN" : "Short CLID";
            r++;
        }
        wb.SaveAs(path);
    }

    private static void WriteBookDataReport(string path, List<ApplnsxBid> bids)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("BookData");
        ws.Cell(1, 1).Value = "OrderNo";  ws.Cell(1, 2).Value = "PAN";
        ws.Cell(1, 3).Value = "CLID";     ws.Cell(1, 4).Value = "DPID";
        ws.Cell(1, 5).Value = "Category"; ws.Cell(1, 6).Value = "Qty";
        ws.Cell(1, 7).Value = "Amount";   ws.Cell(1, 8).Value = "BankCode";
        ws.Cell(1, 9).Value = "Status";
        int r = 2;
        foreach (var b in bids)
        {
            ws.Cell(r, 1).Value = b.OrderNo;   ws.Cell(r, 2).Value = b.PAN;
            ws.Cell(r, 3).Value = b.CLID;      ws.Cell(r, 4).Value = b.DPID;
            ws.Cell(r, 5).Value = b.Category;  ws.Cell(r, 6).Value = b.Quantity;
            ws.Cell(r, 7).Value = b.Amount;    ws.Cell(r, 8).Value = b.BankCode;
            ws.Cell(r, 9).Value = b.Status;
            r++;
        }
        wb.SaveAs(path);
    }
}
