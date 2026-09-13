using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Globalization;

namespace IPOProcessingSystem.Services;

/// <summary>
/// Handles Module D: Bank Reconciliation (T+2).
/// Bank file is always treated as the source of truth, per the manual:
/// "IF SUMMARY DOES NOT MATCH THEN ALWAYS CONSIDER BANK FILE IS MAIN BASE FILE".
/// </summary>
public class ReconciliationService : IReconciliationService
{
    private readonly DatabaseService _dbService;

    public ReconciliationService(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    /// <summary>
    /// Reads an Excel (.xlsx/.xls) or CSV bank schedule file and returns normalized rows.
    /// The columnMap parameter maps internal field names to the Excel column headers.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> ImportBankScheduleAsync(
        string filePath, Dictionary<string, string> columnMap)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".csv")
            return await Task.Run(() => ReadCsvFile(filePath, columnMap));
        return await Task.Run(() => ReadExcelFile(filePath, columnMap));
    }

    private List<Dictionary<string, object>> ReadExcelFile(
        string filePath, Dictionary<string, string> columnMap)
    {
        var rows = new List<Dictionary<string, object>>();
        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheets.First();
        var headerRow = ws.Row(1);

        // Build excel-column-index → internal-field-name lookup
        var colIndexToField = new Dictionary<int, string>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetValue<string>().Trim();
            // columnMap: internal field → excel header  →  reverse lookup
            var match = columnMap.FirstOrDefault(kv =>
                string.Equals(kv.Value, header, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null)
                colIndexToField[cell.Address.ColumnNumber] = match.Key;
        }

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, object>();
            foreach (var (colIdx, fieldName) in colIndexToField)
            {
                var cell = row.Cell(colIdx);
                dict[fieldName] = cell.IsEmpty() ? "" : cell.GetValue<string>().Trim();
            }
            if (dict.Count > 0) rows.Add(dict);
        }
        return rows;
    }

    private List<Dictionary<string, object>> ReadCsvFile(
        string filePath, Dictionary<string, string> columnMap)
    {
        var rows = new List<Dictionary<string, object>>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
        };
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        while (csv.Read())
        {
            var dict = new Dictionary<string, object>();
            foreach (var kv in columnMap)
            {
                var excelHeader = kv.Value;
                var idx = Array.FindIndex(headers, h =>
                    string.Equals(h, excelHeader, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                    dict[kv.Key] = csv.GetField(idx) ?? "";
            }
            if (dict.Count > 0) rows.Add(dict);
        }
        return rows;
    }

    public async Task<ReconSummaryResult> CompareAsync(int ipoId, string bankCode,
        List<Dictionary<string, object>> bankRows)
    {
        var applnsxRows = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(a => a.IPO_ID == ipoId && a.BankCode == bankCode)
            .ToListAsync();

        var result = new ReconSummaryResult
        {
            TotalRecords = applnsxRows.Count,
            TotalAmount = applnsxRows.Sum(a => a.Amount)
        };

        var applnsxByForm = applnsxRows.ToDictionary(a => a.OrderNo, a => a);
        var matched = new List<ApplnsxBid>();

        foreach (var bankRow in bankRows)
        {
            var formNo = bankRow.TryGetValue("FormNo", out var f) ? f?.ToString() ?? "" : "";
            var amount = bankRow.TryGetValue("Amount", out var a) &&
                         decimal.TryParse(a?.ToString(), out var amt) ? amt : 0m;

            if (applnsxByForm.TryGetValue(formNo, out var applnRow))
            {
                if (Math.Abs(applnRow.Amount - amount) < 0.01m)
                {
                    matched.Add(applnRow);
                }
                else
                {
                    await _dbService.Connection.InsertAsync(new UnmatchRecord
                    {
                        IPO_ID = ipoId,
                        BankCode = bankCode,
                        FormNo = formNo,
                        UnmatchType = "AMOUNT_UNMATCH",
                        ExcelAmount = amount,
                        ApplnAmount = applnRow.Amount
                    });
                }
            }
            else
            {
                await _dbService.Connection.InsertAsync(new UnmatchRecord
                {
                    IPO_ID = ipoId,
                    BankCode = bankCode,
                    FormNo = formNo,
                    UnmatchType = "AVAILABLE_IN_SCHEDULE_NOT_APPLNSX",
                    ExcelAmount = amount,
                    ApplnAmount = 0
                });
            }
        }

        var matchedFormNos = matched.Select(m => m.OrderNo).ToHashSet();
        foreach (var extra in applnsxRows.Where(a => !matchedFormNos.Contains(a.OrderNo)))
        {
            await _dbService.Connection.InsertAsync(new UnmatchRecord
            {
                IPO_ID = ipoId,
                BankCode = bankCode,
                FormNo = extra.OrderNo,
                UnmatchType = "AVAILABLE_IN_APPLNSX_NOT_SCHEDULE",
                ExcelAmount = 0,
                ApplnAmount = extra.Amount
            });
        }

        result.MatchedRecords = matched.Count;
        result.MatchedAmount = matched.Sum(m => m.Amount);
        result.UnmatchedRecords = bankRows.Count - matched.Count;
        result.ExcelTotalRecords = bankRows.Count;
        result.ExcelTotalAmount = bankRows.Sum(r =>
            r.TryGetValue("Amount", out var v) &&
            decimal.TryParse(v?.ToString(), out var d) ? d : 0m);
        result.UnmatchedAmount = result.TotalAmount - result.MatchedAmount;

        await _dbService.Connection.InsertAsync(new ReconciliationSummary
        {
            IPO_ID = ipoId,
            BankCode = bankCode,
            ExcelAmount = result.ExcelTotalAmount,
            ApplnAmount = result.TotalAmount,
            MatchStatus = result.UnmatchedRecords == 0 ? "MATCHED" : "UNMATCHED"
        });

        return result;
    }

    public async Task RejectRecordAsync(long applnsxRowId, string reason)
    {
        var row = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(a => a.RowId == applnsxRowId).FirstOrDefaultAsync();
        if (row == null) return;
        row.Status = "REJECTED";
        row.RejectionCode = reason;
        await _dbService.Connection.UpdateAsync(row);
    }

    public async Task RestoreRejectedRecordAsync(int rejectionId)
    {
        var rejection = await _dbService.Connection.Table<TechnicalRejection>()
            .Where(r => r.RejectionId == rejectionId).FirstOrDefaultAsync();
        if (rejection == null) return;
        var applnRow = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(a => a.OrderNo == rejection.FormNo).FirstOrDefaultAsync();
        if (applnRow != null)
        {
            applnRow.Status = "PENDING";
            applnRow.RejectionCode = null;
            await _dbService.Connection.UpdateAsync(applnRow);
        }
    }

    public async Task UpdateBankCodeAsync(long applnsxRowId, string newBankCode, string newBankName)
    {
        var row = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(a => a.RowId == applnsxRowId).FirstOrDefaultAsync();
        if (row == null) return;
        row.BankCode = newBankCode;
        row.BankName = newBankName;
        await _dbService.Connection.UpdateAsync(row);
    }

    public async Task MarkBankAsNilAsync(int ipoId, string bankCode, bool isMemberAsba)
    {
        var bank = await _dbService.Connection.Table<BankMaster>()
            .Where(b => b.BankCode == bankCode).FirstOrDefaultAsync();
        if (bank != null)
        {
            bank.IsNilForCurrentIpo = true;
            await _dbService.Connection.UpdateAsync(bank);
        }
    }

    public async Task<List<UnmatchRecord>> GetUnmatchRecordsAsync(int ipoId, string bankCode, string unmatchType)
    {
        return await _dbService.Connection.Table<UnmatchRecord>()
            .Where(u => u.IPO_ID == ipoId && u.BankCode == bankCode && u.UnmatchType == unmatchType)
            .ToListAsync();
    }

    public async Task<ReconSummaryResult> GetSummaryAsync(int ipoId, string bankCode)
    {
        var applnRows = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(a => a.IPO_ID == ipoId && a.BankCode == bankCode)
            .ToListAsync();
        var recon = await _dbService.Connection.Table<ReconciliationSummary>()
            .Where(r => r.IPO_ID == ipoId && r.BankCode == bankCode)
            .FirstOrDefaultAsync();

        return new ReconSummaryResult
        {
            TotalRecords = applnRows.Count,
            TotalAmount = applnRows.Sum(a => a.Amount),
            MatchedRecords = applnRows.Count(a => a.Status == "MATCHED"),
            ExcelTotalAmount = recon?.ExcelAmount ?? 0,
            ExcelTotalRecords = 0
        };
    }

    public async Task<List<ColumnMapping>> GetColumnMappingAsync(string bankCode)
        => await _dbService.Connection.Table<ColumnMapping>()
               .Where(m => m.BankCode == bankCode).ToListAsync();

    public async Task SaveColumnMappingAsync(string bankCode, List<(string source, string target)> pairs)
    {
        // Delete old mapping for this bank and re-insert
        var old = await _dbService.Connection.Table<ColumnMapping>()
            .Where(m => m.BankCode == bankCode).ToListAsync();
        foreach (var o in old) await _dbService.Connection.DeleteAsync(o);
        foreach (var (src, tgt) in pairs)
        {
            await _dbService.Connection.InsertAsync(new ColumnMapping
            {
                BankCode = bankCode,
                SourceColumn = src,
                TargetColumn = tgt
            });
        }
    }
}
