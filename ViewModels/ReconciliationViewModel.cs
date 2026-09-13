using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>
/// Full Reconciliation ViewModel covering all 10 tabs:
/// VIEW EXCEL | TRANSFER TO SQL | CLEAN DATA SUMMARY | APPLNSX UNMATCH |
/// APPLNSX MATCH | SCHEDULE UNMATCH | REJECTION DATA | NIL BANK | SEARCH+UPDATE
/// </summary>
public partial class ReconciliationViewModel : BaseViewModel
{
    private readonly IReconciliationService _reconService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    // ── Common ───────────────────────────────────────────────────────────────
    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string selectedBankCode = string.Empty;
    [ObservableProperty] private string reconType = "NON ASBA"; // NON ASBA / ASBA
    [ObservableProperty] private string currentTab = "VIEW EXCEL";

    // ── VIEW EXCEL tab ───────────────────────────────────────────────────────
    [ObservableProperty] private string filePath = string.Empty;
    [ObservableProperty] private string fileType = "Excel"; // Excel / CSV
    [ObservableProperty] private bool hasHeader = true;
    [ObservableProperty] private int fileRecordCount;
    private List<Dictionary<string, object>> _stagedRows = new();

    // ── TRANSFER TO SQL tab ──────────────────────────────────────────────────
    // Left = internal SQL fields, Right = excel columns from file
    public ObservableCollection<string> SqlFields { get; } = new()
    {
        "FormNo","NAME","DEBENT","AMOUNT","PAN","DPID","CLID","Ref_No","ACNO","REMARK","OrderNo","Amount_Blocked"
    };
    public ObservableCollection<string> ExcelColumns { get; } = new();
    public ObservableCollection<ColumnMappingRow> ColumnMappingPairs { get; } = new();

    // ── CLEAN DATA SUMMARY tab ───────────────────────────────────────────────
    [ObservableProperty] private int totalRecords;
    [ObservableProperty] private decimal totalAmount;
    [ObservableProperty] private int matchedRecords;
    [ObservableProperty] private decimal matchedAmount;
    [ObservableProperty] private int unmatchedRecords;
    [ObservableProperty] private decimal unmatchedAmount;
    [ObservableProperty] private int excelTotalRecords;
    [ObservableProperty] private decimal excelTotalAmount;

    // ── UNMATCH tabs ─────────────────────────────────────────────────────────
    public ObservableCollection<UnmatchRecord> ApplnsxUnmatch { get; } = new();
    public ObservableCollection<UnmatchRecord> ScheduleUnmatch { get; } = new();
    public ObservableCollection<UnmatchRecord> AmountUnmatch { get; } = new();

    // ── REJECTION DATA tab ───────────────────────────────────────────────────
    public ObservableCollection<TechnicalRejection> RejectionData { get; } = new();
    [ObservableProperty] private TechnicalRejection? selectedRejection;

    // ── SEARCH AND UPDATE ────────────────────────────────────────────────────
    [ObservableProperty] private string searchFormNo = string.Empty;
    [ObservableProperty] private ApplnsxBid? selectedBid;
    [ObservableProperty] private string updateBankCode = string.Empty;
    [ObservableProperty] private string updateBankName = string.Empty;
    public ObservableCollection<ApplnsxBid> SearchResults { get; } = new();

    // ── NIL BANK tab ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool isMemberAsba;

    // ── Bank lists ───────────────────────────────────────────────────────────
    public ObservableCollection<string> PendingBanks { get; } = new();
    public ObservableCollection<string> CompletedBanks { get; } = new();

    public ReconciliationViewModel(IReconciliationService reconService, IAlertService alertService, DatabaseService dbService)
    {
        _reconService = reconService;
        _alertService = alertService;
        _dbService = dbService;
        Title = "Bank Reconciliation (T+2)";
    }

    [RelayCommand]
    private async Task LoadBanksAsync()
    {
        var banks = await _dbService.Connection.Table<BankMaster>()
            .Where(b => !b.IsNilForCurrentIpo).ToListAsync();
        var completed = await _dbService.Connection.Table<ReconciliationSummary>()
            .Where(r => r.IPO_ID == IpoId && r.MatchStatus == "MATCHED").ToListAsync();
        var completedCodes = completed.Select(c => c.BankCode).ToHashSet();

        PendingBanks.Clear();
        CompletedBanks.Clear();
        foreach (var b in banks)
        {
            if (completedCodes.Contains(b.BankCode)) CompletedBanks.Add(b.BankCode);
            else PendingBanks.Add(b.BankCode);
        }
    }

    // ── VIEW EXCEL ────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task BrowseAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Bank Schedule (Excel/CSV)"
        });
        if (result != null) FilePath = result.FullPath;
    }

    [RelayCommand]
    private async Task DisplayContentAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath)) { StatusMessage = "Select file first."; return; }
        IsBusy = true;
        try
        {
            // Build basic column map for display (all columns mapped to themselves)
            var basicMap = new Dictionary<string, string>();
            foreach (var field in SqlFields) basicMap[field] = field;

            _stagedRows = await _reconService.ImportBankScheduleAsync(FilePath, basicMap);
            FileRecordCount = _stagedRows.Count;

            // Populate Excel column headers for column-mapping tab
            ExcelColumns.Clear();
            if (_stagedRows.Any())
                foreach (var col in _stagedRows.First().Keys) ExcelColumns.Add(col);

            StatusMessage = $"{FileRecordCount} record(s) loaded.";
        }
        catch (Exception ex)
        {
            await _alertService.ShowErrorAsync($"Error reading file: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteReconAsync()
    {
        if (!await _alertService.ConfirmAsync("Delete Recon", $"Delete all recon data for bank {SelectedBankCode}?")) return;
        var old = await _dbService.Connection.Table<ReconciliationSummary>()
            .Where(r => r.IPO_ID == IpoId && r.BankCode == SelectedBankCode).ToListAsync();
        foreach (var o in old) await _dbService.Connection.DeleteAsync(o);
        StatusMessage = "Recon deleted.";
    }

    // ── TRANSFER TO SQL ───────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LoadMappingAsync()
    {
        var saved = await _reconService.GetColumnMappingAsync(SelectedBankCode);
        ColumnMappingPairs.Clear();
        foreach (var m in saved)
            ColumnMappingPairs.Add(new ColumnMappingRow { SqlField = m.TargetColumn, ExcelColumn = m.SourceColumn });
    }

    [RelayCommand]
    private async Task SaveMappingAsync()
    {
        var pairs = ColumnMappingPairs.Select(p => (p.ExcelColumn, p.SqlField)).ToList();
        await _reconService.SaveColumnMappingAsync(SelectedBankCode, pairs);
        await _alertService.ShowSuccessAsync("Column mapping saved.");
    }

    [RelayCommand]
    private void AddMappingPair()
    {
        ColumnMappingPairs.Add(new ColumnMappingRow());
    }

    [RelayCommand]
    private async Task FinishTransferAsync()
    {
        if (!_stagedRows.Any()) { StatusMessage = "Load file content first."; return; }
        if (!await _alertService.ConfirmAsync("Finish Transfer", "Transfer bank schedule data to SQL?")) return;
        IsBusy = true;
        try
        {
            var columnMap = ColumnMappingPairs.ToDictionary(p => p.SqlField, p => p.ExcelColumn);
            if (!columnMap.Any())
            {
                // Use display headers as-is
                foreach (var field in SqlFields) columnMap[field] = field;
            }
            var bankRows = await _reconService.ImportBankScheduleAsync(FilePath, columnMap);
            var summary = await _reconService.CompareAsync(IpoId, SelectedBankCode, bankRows);

            TotalRecords = summary.TotalRecords;
            TotalAmount = summary.TotalAmount;
            MatchedRecords = summary.MatchedRecords;
            MatchedAmount = summary.MatchedAmount;
            UnmatchedRecords = summary.UnmatchedRecords;
            UnmatchedAmount = summary.UnmatchedAmount;
            ExcelTotalRecords = summary.ExcelTotalRecords;
            ExcelTotalAmount = summary.ExcelTotalAmount;

            await _alertService.ShowSuccessAsync("ASBA File Uploaded Successfully.");
            await LoadUnmatchDataAsync();
            CurrentTab = "CLEAN DATA SUMMARY";
        }
        finally { IsBusy = false; }
    }

    // ── CLEAN DATA SUMMARY ────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RunReconciliationAsync()
    {
        await FinishTransferAsync();
        if (UnmatchedRecords == 0)
        {
            PendingBanks.Remove(SelectedBankCode);
            CompletedBanks.Add(SelectedBankCode);
            StatusMessage = "Reconciliation matched. Proceed to next bank.";
        }
        else
        {
            StatusMessage = $"{UnmatchedRecords} unmatched — review Unmatch tabs.";
        }
    }

    // ── UNMATCH ───────────────────────────────────────────────────────────────
    private async Task LoadUnmatchDataAsync()
    {
        var applnsxOnly = await _reconService.GetUnmatchRecordsAsync(IpoId, SelectedBankCode, "AVAILABLE_IN_APPLNSX_NOT_SCHEDULE");
        ApplnsxUnmatch.Clear();
        foreach (var u in applnsxOnly) ApplnsxUnmatch.Add(u);

        var scheduleOnly = await _reconService.GetUnmatchRecordsAsync(IpoId, SelectedBankCode, "AVAILABLE_IN_SCHEDULE_NOT_APPLNSX");
        ScheduleUnmatch.Clear();
        foreach (var u in scheduleOnly) ScheduleUnmatch.Add(u);

        var amountMismatch = await _reconService.GetUnmatchRecordsAsync(IpoId, SelectedBankCode, "AMOUNT_UNMATCH");
        AmountUnmatch.Clear();
        foreach (var u in amountMismatch) AmountUnmatch.Add(u);
    }

    [RelayCommand]
    private async Task RejectUnmatchRecordAsync(UnmatchRecord record)
    {
        if (!await _alertService.ConfirmAsync("Reject Record", $"Reject FormNo {record.FormNo} from {SelectedBankCode}?")) return;
        // Find the ApplnsxBid row
        var bid = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == IpoId && b.OrderNo == record.FormNo).FirstOrDefaultAsync();
        if (bid != null) await _reconService.RejectRecordAsync(bid.RowId, "RECON_UNMATCH");
        record.Resolved = true;
        await _dbService.Connection.UpdateAsync(record);
        ApplnsxUnmatch.Remove(record);
        await _alertService.ShowSuccessAsync("Record rejected and moved to Rejection Data.");
    }

    // ── REJECTION DATA ────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LoadRejectionDataAsync()
    {
        var list = await _dbService.Connection.Table<TechnicalRejection>()
            .Where(r => r.IPO_ID == IpoId).ToListAsync();
        RejectionData.Clear();
        foreach (var r in list) RejectionData.Add(r);
    }

    [RelayCommand]
    private async Task InsertIntoApplnsxAsync(TechnicalRejection rejection)
    {
        if (!await _alertService.ConfirmAsync("Insert Into Applnsx", "Re-add this rejected record to Applnsx?")) return;
        await _reconService.RestoreRejectedRecordAsync(rejection.RejectionId);
        await _dbService.Connection.DeleteAsync(rejection);
        RejectionData.Remove(rejection);
        await _alertService.ShowSuccessAsync("Record re-inserted into Applnsx.");
    }

    // ── SEARCH AND UPDATE ─────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchFormNo)) return;
        var results = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == IpoId && b.OrderNo == SearchFormNo).ToListAsync();
        SearchResults.Clear();
        foreach (var b in results) SearchResults.Add(b);
    }

    [RelayCommand]
    private async Task UpdateBankCodeAsync()
    {
        if (SelectedBid == null || string.IsNullOrWhiteSpace(UpdateBankCode)) return;
        await _reconService.UpdateBankCodeAsync(SelectedBid.RowId, UpdateBankCode, UpdateBankName);
        await _alertService.ShowSuccessAsync("UPDATE SUCCESSFULL");
        SelectedBid.BankCode = UpdateBankCode;
        SelectedBid.BankName = UpdateBankName;
    }

    // ── NIL BANK ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task MarkBankNilAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedBankCode)) return;
        if (!await _alertService.ConfirmAsync("Nil Bank", $"Mark {SelectedBankCode} as NIL for this IPO?")) return;
        await _reconService.MarkBankAsNilAsync(IpoId, SelectedBankCode, IsMemberAsba);
        PendingBanks.Remove(SelectedBankCode);
        await _alertService.ShowSuccessAsync($"{SelectedBankCode} marked as NIL and removed from active list.");
    }
}

/// <summary>Helper class for the column mapping grid rows.</summary>
public partial class ColumnMappingRow : ObservableObject
{
    [ObservableProperty] private string sqlField = string.Empty;
    [ObservableProperty] private string excelColumn = string.Empty;
}
