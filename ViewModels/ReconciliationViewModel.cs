using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>
/// Full Reconciliation ViewModel — 10 tabs per spec §2.5.1:
/// VIEW EXCEL | TRANSFER TO SQL | CLEAN DATA SUMMARY | APPLNSX UNMATCH CASE |
/// APPLNSX MATCH CASE | SCHEDULE UNMATCH | SCHEDULE UNMATCH I | SCHEDULE UNMATCH II |
/// REJECTION DATA | NIL BANK
/// Business rule: Bank file is ALWAYS the source of truth during reconciliation.
/// </summary>
public partial class ReconciliationViewModel : BaseViewModel
{
    private readonly IReconciliationService _reconService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    // ── Common ────────────────────────────────────────────────────────────────
    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string reconType = "NON-ASBA"; // NON-ASBA / ASBA
    [ObservableProperty] private string activeTab = "ViewExcel";

    // Tab visibility properties
    public bool IsTabViewExcel      => ActiveTab == "ViewExcel";
    public bool IsTabTransferToSql  => ActiveTab == "TransferToSql";
    public bool IsTabCleanData      => ActiveTab == "CleanData";
    public bool IsTabApplnsxUnmatch => ActiveTab == "ApplnsxUnmatch";
    public bool IsTabApplnsxMatch   => ActiveTab == "ApplnsxMatch";
    public bool IsTabSchedUnmatch   => ActiveTab == "SchedUnmatch";
    public bool IsTabSchedUnmatchI  => ActiveTab == "SchedUnmatchI";
    public bool IsTabSchedUnmatchII => ActiveTab == "SchedUnmatchII";
    public bool IsTabRejectionData  => ActiveTab == "RejectionData";
    public bool IsTabNilBank        => ActiveTab == "NilBank";

    // ── VIEW EXCEL tab ────────────────────────────────────────────────────────
    [ObservableProperty] private string filePath = string.Empty;
    [ObservableProperty] private string selectedFileType = "Excel"; // Excel / CSV
    [ObservableProperty] private bool hasHeader = true;
    [ObservableProperty] private string selectedBankCode = string.Empty;
    [ObservableProperty] private int fileRecordCount;
    [ObservableProperty] private bool hasMultipleSheets;
    [ObservableProperty] private string selectedSheet = string.Empty;
    public ObservableCollection<string> FileTypes { get; } = new() { "Excel", "CSV" };
    public ObservableCollection<string> SheetNames { get; } = new();
    public ObservableCollection<string> BankNames { get; } = new();
    public ObservableCollection<string> PendingBanks { get; } = new();
    public ObservableCollection<string> CompletedBanks { get; } = new();
    public ObservableCollection<string> PreviewRows { get; } = new();  // raw string rows for VIEW EXCEL

    // ── TRANSFER TO SQL tab ───────────────────────────────────────────────────
    private readonly Dictionary<string, string> _columnMap = new();
    public ObservableCollection<string> SystemColumns { get; } = new()
    {
        "FormNo","NAME","DEBENT","AMOUNT","PAN","DPID","CLID","Ref_No","ACNO","REMARK","OrderNo","Amount_Blocked"
    };
    public ObservableCollection<string> ExcelColumns { get; } = new();
    [ObservableProperty] private string? selectedSystemColumn;
    [ObservableProperty] private string? selectedExcelColumn;
    [ObservableProperty] private string mappingStatus = string.Empty;

    // ── CLEAN DATA SUMMARY tab ────────────────────────────────────────────────
    [ObservableProperty] private int applnsxTotalRecords;
    [ObservableProperty] private decimal applnsxTotalAmount;
    [ObservableProperty] private int applnsxMatchedRecords;
    [ObservableProperty] private decimal applnsxMatchedAmount;
    [ObservableProperty] private int applnsxUnmatchedRecords;
    [ObservableProperty] private decimal applnsxUnmatchedAmount;
    [ObservableProperty] private int excelTotalRecords;
    [ObservableProperty] private decimal excelTotalAmount;
    [ObservableProperty] private int excelMatchedRecords;
    [ObservableProperty] private decimal excelMatchedAmount;
    [ObservableProperty] private int excelUnmatchedRecords;
    [ObservableProperty] private decimal excelUnmatchedAmount;

    // Search and Update panel (Clean Data Summary)
    [ObservableProperty] private bool showSearchUpdatePanel;
    [ObservableProperty] private string reconSearchQuery = string.Empty;
    public ObservableCollection<ApplnsxBid> ReconSearchResults { get; } = new();
    [ObservableProperty] private ApplnsxBid? selectedReconBid;
    [ObservableProperty] private string newBankCode = string.Empty;

    public string ReconVerdictText => ApplnsxTotalRecords == ExcelTotalRecords && ApplnsxTotalAmount == ExcelTotalAmount
        ? $"✅ RECONCILED — Both totals match ({ApplnsxTotalRecords} records, ₹{ApplnsxTotalAmount:N0})"
        : $"❌ MISMATCH — APPLNSX: {ApplnsxTotalRecords} recs / ₹{ApplnsxTotalAmount:N0} | Bank: {ExcelTotalRecords} recs / ₹{ExcelTotalAmount:N0}";
    public Color ReconMatchColor => ApplnsxTotalRecords == ExcelTotalRecords && ApplnsxTotalAmount == ExcelTotalAmount
        ? Colors.ForestGreen : Colors.Crimson;

    // ── APPLNSX UNMATCH CASE tab ──────────────────────────────────────────────
    public ObservableCollection<UnmatchRecord> ApplnsxUnmatch { get; } = new();
    [ObservableProperty] private UnmatchRecord? selectedUnmatchRecord;
    [ObservableProperty] private string searchFormNo = string.Empty;
    [ObservableProperty] private string updateBankCode = string.Empty;

    // ── APPLNSX MATCH CASE tab ────────────────────────────────────────────────
    public ObservableCollection<ApplnsxBid> ApplnsxMatch { get; } = new();

    // ── SCHEDULE UNMATCH tabs ─────────────────────────────────────────────────
    public ObservableCollection<ApplnsxBid> ScheduleUnmatch { get; } = new();
    public ObservableCollection<ApplnsxBid> ScheduleUnmatchI { get; } = new();
    public ObservableCollection<ApplnsxBid> ScheduleUnmatchII { get; } = new();

    // ── REJECTION DATA tab ────────────────────────────────────────────────────
    public ObservableCollection<RejectionRecord> RejectedRecords { get; } = new();
    [ObservableProperty] private string rejSubTab = "Update"; // Update / Insert
    [ObservableProperty] private string rejSearchFormNo = string.Empty;
    [ObservableProperty] private string rejFilterBankCode = string.Empty;
    public bool IsRejSubTabInsert => RejSubTab == "Insert";

    // ── NIL BANK tab ──────────────────────────────────────────────────────────
    [ObservableProperty] private string nilBankCode = string.Empty;
    [ObservableProperty] private string nilAsbaType = "BANK"; // BANK / MEMBER
    [ObservableProperty] private int nilBankRecordCount;
    public ObservableCollection<ApplnsxBid> NilBankRows { get; } = new();

    public ReconciliationViewModel(IReconciliationService reconService, IAlertService alertService, DatabaseService dbService)
    {
        _reconService = reconService;
        _alertService = alertService;
        _dbService = dbService;
        Title = "Bank Reconciliation (T+2)";
        Task.Run(LoadBanksAsync);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tab switching
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private void SwitchTab(string tab)
    {
        ActiveTab = tab;
        OnPropertyChanged(nameof(IsTabViewExcel));
        OnPropertyChanged(nameof(IsTabTransferToSql));
        OnPropertyChanged(nameof(IsTabCleanData));
        OnPropertyChanged(nameof(IsTabApplnsxUnmatch));
        OnPropertyChanged(nameof(IsTabApplnsxMatch));
        OnPropertyChanged(nameof(IsTabSchedUnmatch));
        OnPropertyChanged(nameof(IsTabSchedUnmatchI));
        OnPropertyChanged(nameof(IsTabSchedUnmatchII));
        OnPropertyChanged(nameof(IsTabRejectionData));
        OnPropertyChanged(nameof(IsTabNilBank));
    }

    [RelayCommand]
    private void SwitchRejSubTab(string subTab)
    {
        RejSubTab = subTab;
        OnPropertyChanged(nameof(IsRejSubTabInsert));
    }

    // ────────────────────────────────────────────────────────────────────────
    // VIEW EXCEL tab commands
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task BrowseAsync()
    {
        var ext = SelectedFileType == "CSV" ? ".csv" : ".xlsx";
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = $"Select Bank Schedule File ({ext})"
        });
        if (result != null) FilePath = result.FullPath;
    }

    [RelayCommand]
    private async Task DisplayContentAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath)) { StatusMessage = "Browse a file first."; return; }
        IsBusy = true;
        try
        {
            // Load the bank file using ReconciliationService
            var rows = await _reconService.ImportBankScheduleAsync(FilePath, _columnMap);
            FileRecordCount = rows.Count;
            PreviewRows.Clear();
            foreach (var row in rows.Take(200))
                PreviewRows.Add(string.Join(" | ", row.Values.Select(v => v?.ToString() ?? "")));

            // Populate excel columns for mapping
            if (rows.Any())
            {
                ExcelColumns.Clear();
                foreach (var key in rows[0].Keys) ExcelColumns.Add(key);
            }
            StatusMessage = $"Loaded {FileRecordCount} records from {Path.GetFileName(FilePath)}.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ClearFile()
    {
        FilePath = string.Empty;
        PreviewRows.Clear();
        FileRecordCount = 0;
        StatusMessage = "File cleared.";
    }

    [RelayCommand]
    private async Task DeleteReconAsync()
    {
        if (!await _alertService.ConfirmAsync("Delete Recon", $"Delete all recon data for bank '{SelectedBankCode}'? This cannot be undone.")) return;
        // Delete ReconSession entries for this bank
        var sessions = await _dbService.Connection.Table<ReconSession>().Where(s => s.IPO_Id == IpoId && s.BankCode == SelectedBankCode).ToListAsync();
        foreach (var s in sessions) await _dbService.Connection.DeleteAsync(s);
        StatusMessage = $"Recon data deleted for {SelectedBankCode}.";
        await LoadBanksAsync();
    }

    [RelayCommand]
    private void LoadSheet() => StatusMessage = $"Sheet '{SelectedSheet}' loaded.";

    [RelayCommand]
    public async Task LoadBanksAsync()
    {
        var allBanks = await _dbService.Connection.Table<BankMaster>().ToListAsync();
        var nilSessions = await _dbService.Connection.Table<ReconSession>()
            .Where(s => s.IPO_Id == IpoId && s.Status == "Nil").ToListAsync();
        var nilCodes = nilSessions.Select(s => s.BankCode).ToHashSet();

        var completedSessions = await _dbService.Connection.Table<ReconSession>()
            .Where(s => s.IPO_Id == IpoId && s.Status == "Completed").ToListAsync();
        var completedCodes = completedSessions.Select(s => s.BankCode).ToHashSet();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BankNames.Clear();
            PendingBanks.Clear();
            CompletedBanks.Clear();

            foreach (var b in allBanks)
            {
                BankNames.Add(b.BankCode);
                if (nilCodes.Contains(b.BankCode)) continue;

                if (completedCodes.Contains(b.BankCode))
                    CompletedBanks.Add(b.BankCode);
                else
                    PendingBanks.Add(b.BankCode);
            }
            if (BankNames.Any() && string.IsNullOrEmpty(SelectedBankCode))
                SelectedBankCode = BankNames.First();
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // TRANSFER TO SQL tab commands
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private void MapColumnPair()
    {
        if (SelectedSystemColumn == null || SelectedExcelColumn == null) return;
        _columnMap[SelectedSystemColumn] = SelectedExcelColumn;
        MappingStatus = $"Mapped: {SelectedSystemColumn} ↔ {SelectedExcelColumn} ({_columnMap.Count} pairs total)";
    }

    [RelayCommand]
    private async Task LoadMappingAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedBankCode)) { StatusMessage = "Select a bank first."; return; }
        var mappings = await _dbService.Connection.Table<ColumnMapping>().Where(m => m.BankCode == SelectedBankCode).ToListAsync();
        _columnMap.Clear();
        foreach (var m in mappings) _columnMap[m.TargetColumn] = m.SourceColumn;
        MappingStatus = $"Loaded {_columnMap.Count} saved mappings for {SelectedBankCode}.";
    }

    [RelayCommand]
    private async Task SaveMappingAsync()
    {
        if (!_columnMap.Any()) { MappingStatus = "No pairs to save. Map at least one column pair."; return; }
        foreach (var kv in _columnMap)
        {
            var existing = await _dbService.Connection.Table<ColumnMapping>()
                .Where(m => m.BankCode == SelectedBankCode && m.TargetColumn == kv.Key).FirstOrDefaultAsync();
            if (existing != null) { existing.SourceColumn = kv.Value; await _dbService.Connection.UpdateAsync(existing); }
            else await _dbService.Connection.InsertAsync(new ColumnMapping { BankCode = SelectedBankCode, TargetColumn = kv.Key, SourceColumn = kv.Value });
        }
        MappingStatus = $"Mapping saved for {SelectedBankCode} ({_columnMap.Count} pairs).";
    }

    [RelayCommand]
    private void CleanMapping()
    {
        _columnMap.Clear();
        MappingStatus = "Column mapping cleared. Remap and save.";
    }

    [RelayCommand]
    private async Task FinishImportAsync()
    {
        if (!_columnMap.Any()) { await _alertService.ShowErrorAsync("No column mapping defined. Map columns first, then use FINISH."); return; }
        if (!await _alertService.ConfirmAsync("Commit to SQL", $"Commit {FileRecordCount} records from {SelectedBankCode} into APPLNSX?")) return;
        IsBusy = true;
        try
        {
            var rows = await _reconService.ImportBankScheduleAsync(FilePath, _columnMap);
            int inserted = 0;
            foreach (var row in rows)
            {
                var bid = new ApplnsxBid
                {
                    IPO_ID = IpoId,
                    BankCode = SelectedBankCode,
                    FormNo = row.TryGetValue("FormNo", out var fn) ? fn?.ToString() ?? "" : "",
                    Name = row.TryGetValue("NAME", out var nm) ? nm?.ToString() ?? "" : "",
                    Quantity = row.TryGetValue("DEBENT", out var dbt) ? int.TryParse(dbt?.ToString(), out int q) ? q : 0 : 0,
                    Amount = row.TryGetValue("AMOUNT", out var amt) ? decimal.TryParse(amt?.ToString(), out decimal a) ? a : 0 : 0,
                    PAN = row.TryGetValue("PAN", out var pan) ? pan?.ToString() ?? "" : "",
                    DPID = row.TryGetValue("DPID", out var dp) ? dp?.ToString() ?? "" : "",
                    CLID = row.TryGetValue("CLID", out var cl) ? cl?.ToString() ?? "" : "",
                    RefNo = row.TryGetValue("Ref_No", out var rn) ? rn?.ToString() ?? "" : "",
                    AccountNo = row.TryGetValue("ACNO", out var ac) ? ac?.ToString() ?? "" : "",
                    Remark = row.TryGetValue("REMARK", out var rk) ? rk?.ToString() ?? "" : "",
                    OrderNo = row.TryGetValue("OrderNo", out var on) ? on?.ToString() ?? "" : "",
                    IsBankFile = true,
                    Status = "PENDING"
                };
                await _dbService.Connection.InsertAsync(bid);
                inserted++;
            }
            // Mark session complete
            await _dbService.Connection.InsertAsync(new ReconSession { IPO_Id = IpoId, BankCode = SelectedBankCode, Status = "Completed" });
            PendingBanks.Remove(SelectedBankCode);
            if (!CompletedBanks.Contains(SelectedBankCode)) CompletedBanks.Add(SelectedBankCode);
            StatusMessage = $"{inserted} records committed for {SelectedBankCode}.";
            await _alertService.ShowSuccessAsync($"ASBA File Uploaded Successfully for {SelectedBankCode}.", $"{inserted} records inserted.");
        }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────────────────────
    // CLEAN DATA SUMMARY tab commands
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RefreshSummaryAsync()
    {
        IsBusy = true;
        try
        {
            var applnsx = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.BankCode == SelectedBankCode).ToListAsync();
            ApplnsxTotalRecords = applnsx.Count;
            ApplnsxTotalAmount = applnsx.Sum(b => b.Amount);
            var bankFile = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.BankCode == SelectedBankCode && b.IsBankFile).ToListAsync();
            ExcelTotalRecords = bankFile.Count;
            ExcelTotalAmount = bankFile.Sum(b => b.Amount);

            OnPropertyChanged(nameof(ReconVerdictText));
            OnPropertyChanged(nameof(ReconMatchColor));
            StatusMessage = $"Summary refreshed for {SelectedBankCode}.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ShowAmountUnmatch() => StatusMessage = "Amount unmatch cases — use SEARCH AND UPDATE to investigate.";

    partial void OnSelectedReconBidChanged(ApplnsxBid? value)
    {
        if (value != null)
        {
            NewBankCode = value.BankCode;
        }
    }

    [RelayCommand]
    private void SearchAndUpdate()
    {
        ShowSearchUpdatePanel = !ShowSearchUpdatePanel;
    }

    [RelayCommand]
    private void HideSearchUpdatePanel()
    {
        ShowSearchUpdatePanel = false;
    }

    [RelayCommand]
    private async Task ExecuteReconSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(ReconSearchQuery))
        {
            StatusMessage = "Please enter Form No, Order No, PAN or Client ID to search.";
            return;
        }

        IsBusy = true;
        try
        {
            string q = ReconSearchQuery.Trim();
            var results = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && (b.FormNo.Contains(q) || b.OrderNo.Contains(q) || b.PAN.Contains(q) || b.ClientId.Contains(q)))
                .Take(50)
                .ToListAsync();

            ReconSearchResults.Clear();
            foreach (var r in results) ReconSearchResults.Add(r);

            StatusMessage = $"Found {results.Count} record(s).";
            if (results.Count > 0)
            {
                SelectedReconBid = results.First();
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveReconBidUpdateAsync()
    {
        if (SelectedReconBid == null)
        {
            await _alertService.ShowAsync("Select Record", "Please select a record from the search results.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewBankCode))
        {
            await _alertService.ShowAsync("Bank Code Required", "Please enter or select a new Bank Code.");
            return;
        }

        SelectedReconBid.BankCode = NewBankCode.Trim().ToUpperInvariant();
        await _dbService.Connection.UpdateAsync(SelectedReconBid);

        await _alertService.ShowSuccessAsync($"Updated Form {SelectedReconBid.FormNo} Bank Code to '{NewBankCode}'.");
        StatusMessage = $"Updated Form {SelectedReconBid.FormNo} to bank {NewBankCode}.";
        await RefreshSummaryAsync();
    }

    // ────────────────────────────────────────────────────────────────────────
    // APPLNSX UNMATCH tab commands
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LoadApplnsxUnmatchAsync()
    {
        IsBusy = true;
        try
        {
            var bids = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.BankCode == SelectedBankCode && !b.IsBankFile).ToListAsync();
            ApplnsxUnmatch.Clear();
            foreach (var bid in bids)
                ApplnsxUnmatch.Add(new UnmatchRecord { FormNo = bid.FormNo, BankCode = bid.BankCode, ExcelAmount = bid.Amount, UnmatchType = "APPLNSX only" });
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RejectUnmatchRecordAsync(UnmatchRecord record)
    {
        if (!await _alertService.ConfirmAsync("Reject Record", $"Reject application {record.FormNo}? This will move it to Rejection Data.")) return;
        // Move to RejectionRecord table
        await _dbService.Connection.InsertAsync(new RejectionRecord
        {
            IPO_Id = IpoId,
            FormNo = record.FormNo,
            BankCode = record.BankCode,
            Status = "Active",
            Remark = "APPLNSX Unmatch Rejection (Recon)"
        });
        ApplnsxUnmatch.Remove(record);
        await _alertService.ShowSuccessAsync($"Application {record.FormNo} rejected and moved to Rejection Data.");
        await RefreshSummaryAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchFormNo)) return;
        var results = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == IpoId && (b.FormNo == SearchFormNo || b.OrderNo == SearchFormNo)).ToListAsync();
        if (!results.Any()) { StatusMessage = $"No record found for '{SearchFormNo}'."; return; }
        var r = results.First();
        UpdateBankCode = r.BankCode;
        StatusMessage = $"Found: {r.FormNo} | Bank: {r.BankCode} | Amount: ₹{r.Amount:N0}";
    }

    [RelayCommand]
    private async Task UpdateBankCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchFormNo) || string.IsNullOrWhiteSpace(UpdateBankCode)) return;
        var bid = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == IpoId && b.FormNo == SearchFormNo).FirstOrDefaultAsync();
        if (bid == null) { StatusMessage = "Record not found."; return; }
        bid.BankCode = UpdateBankCode;
        await _dbService.Connection.UpdateAsync(bid);
        StatusMessage = $"UPDATE SUCCESSFULL — {bid.FormNo} bank code updated to {UpdateBankCode}.";
    }

    // ────────────────────────────────────────────────────────────────────────
    // APPLNSX MATCH tab commands
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LoadMatchedRecordsAsync()
    {
        IsBusy = true;
        try
        {
            var matched = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.BankCode == SelectedBankCode && b.Status == "MATCHED").ToListAsync();
            ApplnsxMatch.Clear();
            foreach (var m in matched) ApplnsxMatch.Add(m);
            StatusMessage = $"Loaded {matched.Count} matched records.";
        }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────────────────────
    // SCHEDULE UNMATCH tab commands
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LoadSchedUnmatchAsync()
    {
        IsBusy = true;
        try
        {
            // Records in APPLNSX with a different bank code than currently selected
            var records = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.BankCode != SelectedBankCode && !string.IsNullOrEmpty(b.FormNo)).ToListAsync();
            ScheduleUnmatch.Clear();
            foreach (var r in records.Take(500)) ScheduleUnmatch.Add(r);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadSchedUnmatchIAsync()
    {
        IsBusy = true;
        try
        {
            var records = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.BankCode != SelectedBankCode).ToListAsync();
            ScheduleUnmatchI.Clear();
            foreach (var r in records.Take(500)) ScheduleUnmatchI.Add(r);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadSchedUnmatchIIAsync()
    {
        IsBusy = true;
        try
        {
            var records = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.BankCode != SelectedBankCode).ToListAsync();
            ScheduleUnmatchII.Clear();
            foreach (var r in records.Take(500)) ScheduleUnmatchII.Add(r);
        }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────────────────────
    // REJECTION DATA tab commands
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LoadRejectionDataAsync()
    {
        IsBusy = true;
        try
        {
            var query = _dbService.Connection.Table<RejectionRecord>().Where(r => r.IPO_Id == IpoId);
            if (!string.IsNullOrWhiteSpace(RejFilterBankCode))
                query = query.Where(r => r.BankCode == RejFilterBankCode);
            if (!string.IsNullOrWhiteSpace(RejSearchFormNo))
                query = query.Where(r => r.FormNo.Contains(RejSearchFormNo));
            var records = await query.ToListAsync();
            RejectedRecords.Clear();
            foreach (var r in records) RejectedRecords.Add(r);
            StatusMessage = $"Loaded {records.Count} rejected record(s).";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task InsertIntoApplnsxAsync()
    {
        var selected = RejectedRecords.Where(r => r.IsSelected).ToList();
        if (!selected.Any()) { await _alertService.ShowErrorAsync("Select at least one record to reinstate."); return; }
        if (!await _alertService.ConfirmAsync("Reinstate Records", $"Reinstate {selected.Count} record(s) into APPLNSX?")) return;
        IsBusy = true;
        try
        {
            foreach (var rej in selected)
            {
                rej.Status = "Reverted";
                await _dbService.Connection.UpdateAsync(rej);
                // Re-insert into APPLNSX with pending status
                await _dbService.Connection.InsertAsync(new ApplnsxBid
                {
                    IPO_ID = IpoId, FormNo = rej.FormNo, BankCode = rej.BankCode,
                    Name = rej.Name, Amount = rej.Amount, Quantity = (int)rej.Debent,
                    PAN = rej.PAN, CLID = rej.CLID, AccountNo = rej.ACNO, Remark = rej.Remark,
                    Status = "PENDING"
                });
                RejectedRecords.Remove(rej);
            }
            await _alertService.ShowSuccessAsync($"{selected.Count} record(s) reinstated into APPLNSX.");
        }
        finally { IsBusy = false; }
    }

    // ────────────────────────────────────────────────────────────────────────
    // NIL BANK tab commands
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ShowNilBankDetailsAsync()
    {
        IsBusy = true;
        try
        {
            var rows = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.BankCode == NilBankCode).ToListAsync();
            NilBankRows.Clear();
            foreach (var r in rows) NilBankRows.Add(r);
            NilBankRecordCount = rows.Count;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task MarkBankNilAsync()
    {
        if (string.IsNullOrWhiteSpace(NilBankCode)) { StatusMessage = "Select a bank first."; return; }
        if (NilBankRecordCount > 0)
        {
            await _alertService.ShowErrorAsync($"{NilBankCode} has {NilBankRecordCount} records. Cannot mark as NIL until all records are resolved.");
            return;
        }
        if (!await _alertService.ConfirmAsync("Mark NIL Bank", $"Mark '{NilBankCode}' as NIL (removes it from active recon list)?")) return;
        await _dbService.Connection.InsertAsync(new ReconSession { IPO_Id = IpoId, BankCode = NilBankCode, Status = "Nil" });
        PendingBanks.Remove(NilBankCode);
        NilBankRows.Clear();
        NilBankRecordCount = 0;
        StatusMessage = $"{NilBankCode} marked as NIL and removed from active reconciliation.";
    }

    // ────────────────────────────────────────────────────────────────────────
    // Existing commands kept for backwards compatibility
    // ────────────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RunReconciliationAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _reconService.GetSummaryAsync(IpoId, SelectedBankCode);
            ApplnsxTotalRecords = result.TotalRecords;
            ExcelTotalRecords = result.ExcelTotalRecords;
            ApplnsxTotalAmount = result.TotalAmount;
            ExcelTotalAmount = result.ExcelTotalAmount;
            ApplnsxMatchedRecords = result.MatchedRecords;
            ApplnsxMatchedAmount = result.MatchedAmount;
            ApplnsxUnmatchedRecords = result.UnmatchedRecords;
            ApplnsxUnmatchedAmount = result.UnmatchedAmount;
            OnPropertyChanged(nameof(ReconVerdictText));
            OnPropertyChanged(nameof(ReconMatchColor));
            StatusMessage = $"Reconciliation summary updated for {SelectedBankCode}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reconciliation error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    partial void OnActiveTabChanged(string value)
    {
        // notify all tab visibility bindings
        OnPropertyChanged(nameof(IsTabViewExcel));
        OnPropertyChanged(nameof(IsTabTransferToSql));
        OnPropertyChanged(nameof(IsTabCleanData));
        OnPropertyChanged(nameof(IsTabApplnsxUnmatch));
        OnPropertyChanged(nameof(IsTabApplnsxMatch));
        OnPropertyChanged(nameof(IsTabSchedUnmatch));
        OnPropertyChanged(nameof(IsTabSchedUnmatchI));
        OnPropertyChanged(nameof(IsTabSchedUnmatchII));
        OnPropertyChanged(nameof(IsTabRejectionData));
        OnPropertyChanged(nameof(IsTabNilBank));
    }

    partial void OnRejSubTabChanged(string value) => OnPropertyChanged(nameof(IsRejSubTabInsert));
}

/// <summary>Lightweight column mapping row for Transfer-to-SQL UI.</summary>
public class ColumnMappingRow
{
    public string SystemField { get; set; } = string.Empty;
    public string ExcelField { get; set; } = string.Empty;
}
