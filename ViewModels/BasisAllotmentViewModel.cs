using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

public partial class BasisAllotmentViewModel : BaseViewModel
{
    private readonly IBasisCalculationService _basisService;
    private readonly IReportService _reportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string? selectedCategory;
    [ObservableProperty] private BasisOfAllotment? selectedRow;
    [ObservableProperty] private string? lastExportPath;
    [ObservableProperty] private bool exchangeApproved;
    [ObservableProperty] private bool isEditingRow;

    // Edit panel fields
    [ObservableProperty] private string editCategory = string.Empty;
    [ObservableProperty] private int editSerial;
    [ObservableProperty] private int editApplns;
    [ObservableProperty] private decimal editShrApld;
    [ObservableProperty] private decimal editPropAlloc;
    [ObservableProperty] private int editMlot;
    [ObservableProperty] private int editNoAlto;
    [ObservableProperty] private decimal editShrAlto;
    [ObservableProperty] private int editNumerator = 1;
    [ObservableProperty] private int editDenominator = 1;

    public ObservableCollection<BasisOfAllotment> BasisRows { get; } = new();
    public ObservableCollection<BasisOfAllotment> FilteredRows { get; } = new();
    public ObservableCollection<BasisCategoryParameter> CategoryParams { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { "RETAIL", "HNI", "MARKET MAKER", "EMPLOYEE" };

    // New category parameter edit
    [ObservableProperty] private BasisCategoryParameter newCategoryParam = new();

    public BasisAllotmentViewModel(IBasisCalculationService basisService, IReportService reportService,
        IAlertService alertService, DatabaseService dbService)
    {
        _basisService = basisService;
        _reportService = reportService;
        _alertService = alertService;
        _dbService = dbService;
        Title = "Basis of Allotment";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var rows = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == IpoId).ToListAsync();
        BasisRows.Clear();
        foreach (var r in rows) BasisRows.Add(r);
        FilteredRows.Clear();
        foreach (var r in rows) FilteredRows.Add(r);

        var catParams = await _dbService.Connection.Table<BasisCategoryParameter>()
            .Where(p => p.IPO_ID == IpoId).ToListAsync();
        CategoryParams.Clear();
        foreach (var p in catParams) CategoryParams.Add(p);
    }

    [RelayCommand]
    private async Task ProcessBasisAsync()
    {
        if (!await _alertService.ConfirmAsync("Process Basis", "Are you sure you want to process Basis?")) return;
        IsBusy = true;
        try
        {
            var rows = await _basisService.ProcessBasisAsync(IpoId);
            BasisRows.Clear();
            FilteredRows.Clear();
            foreach (var r in rows) { BasisRows.Add(r); FilteredRows.Add(r); }
            StatusMessage = $"Basis processed: {rows.Count} row(s) generated.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void FilterByCategory()
    {
        FilteredRows.Clear();
        var src = string.IsNullOrEmpty(SelectedCategory)
            ? BasisRows
            : (IEnumerable<BasisOfAllotment>)BasisRows.Where(r =>
                string.Equals(r.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        foreach (var r in src) FilteredRows.Add(r);
    }

    [RelayCommand]
    private void EditSelectedRow()
    {
        if (SelectedRow == null) return;
        EditCategory = SelectedRow.Category;
        EditSerial = SelectedRow.Serial;
        EditApplns = SelectedRow.Applns;
        EditShrApld = SelectedRow.ShrApld;
        EditPropAlloc = SelectedRow.PropAlloc;
        EditMlot = SelectedRow.MLot;
        EditNoAlto = SelectedRow.NoAlto;
        EditShrAlto = SelectedRow.ShrAlto;
        EditNumerator = SelectedRow.Numerator;
        EditDenominator = SelectedRow.Denomin;
        IsEditingRow = true;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (SelectedRow == null) return;
        SelectedRow.PropAlloc = EditPropAlloc;
        SelectedRow.MLot = EditMlot;
        SelectedRow.NoAlto = EditNoAlto;
        SelectedRow.ShrAlto = EditShrAlto;
        SelectedRow.Numerator = EditNumerator;
        SelectedRow.Denomin = EditDenominator;
        SelectedRow.Excess = SelectedRow.PropAlloc - SelectedRow.ShrAlto;
        var updated = _basisService.RecalculateRow(SelectedRow);
        await _dbService.Connection.UpdateAsync(updated);
        IsEditingRow = false;
        await LoadAsync();
        StatusMessage = "Row updated.";
    }

    [RelayCommand]
    private void HideEdit() => IsEditingRow = false;

    [RelayCommand]
    private async Task ApproveExchangeAsync()
    {
        if (!await _alertService.ConfirmAsync("Exchange Approved", "Are you sure? Basis Update Confirm.")) return;
        IsBusy = true;
        try
        {
            await _basisService.ApproveBasisAsync(IpoId);
            ExchangeApproved = true;
            await _alertService.ShowSuccessAsync("Basis done Update Confirm.");
            StatusMessage = "Exchange Approved.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportBseAsync()
    {
        IsBusy = true;
        try
        {
            LastExportPath = await _reportService.GenerateAllotteesReportAsync(IpoId, false);
            await _alertService.ShowSuccessAsync("BSE Basis exported.", LastExportPath);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportNseNetbasAsync()
    {
        IsBusy = true;
        try
        {
            // NSE NetBAS format — similar to allottees but NSE-structured
            var path = Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{IpoId}",
                $"NetBAS_NSE_{IpoId}_{DateTime.Now:yyyyMMddHHmm}.xlsx");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("NetBAS");
            var rows = await _dbService.Connection.Table<BasisOfAllotment>()
                .Where(b => b.IPO_ID == IpoId).ToListAsync();
            ws.Cell(1, 1).Value = "Category"; ws.Cell(1, 2).Value = "Applns";
            ws.Cell(1, 3).Value = "ShrApld"; ws.Cell(1, 4).Value = "ShrAlto";
            ws.Cell(1, 5).Value = "Numerator"; ws.Cell(1, 6).Value = "Denomin";
            int r = 2;
            foreach (var row in rows)
            {
                ws.Cell(r, 1).Value = row.Category; ws.Cell(r, 2).Value = row.Applns;
                ws.Cell(r, 3).Value = row.ShrApld;  ws.Cell(r, 4).Value = row.ShrAlto;
                ws.Cell(r, 5).Value = row.Numerator; ws.Cell(r, 6).Value = row.Denomin;
                r++;
            }
            wb.SaveAs(path);
            LastExportPath = path;
            await _alertService.ShowSuccessAsync("NSE NetBAS exported.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResetBasisAsync()
    {
        if (!await _alertService.ConfirmAsync("Reset Basis", "Delete all basis rows and start over?")) return;
        var old = await _dbService.Connection.Table<BasisOfAllotment>()
            .Where(b => b.IPO_ID == IpoId).ToListAsync();
        foreach (var o in old) await _dbService.Connection.DeleteAsync(o);
        BasisRows.Clear(); FilteredRows.Clear();
        ExchangeApproved = false;
        StatusMessage = "Basis reset.";
    }

    [RelayCommand]
    private async Task GenerateLuckyDrawAsync()
    {
        var ranges = await _basisService.GenerateLuckyDrawRangesAsync(IpoId);
        StatusMessage = $"Lucky draw ranges generated for {ranges.Count} categor(y/ies).";
    }

    // ── Basis Category CRUD ───────────────────────────────────────────────────
    [RelayCommand]
    private async Task AddCategoryParamAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryParam.Category)) { StatusMessage = "Enter category name."; return; }
        NewCategoryParam.IPO_ID = IpoId;
        await _dbService.Connection.InsertAsync(NewCategoryParam);
        CategoryParams.Add(NewCategoryParam);
        NewCategoryParam = new BasisCategoryParameter();
        await _alertService.ShowSuccessAsync("Basis Category added.");
    }

    [RelayCommand]
    private async Task DeleteCategoryParamAsync(BasisCategoryParameter param)
    {
        await _dbService.Connection.DeleteAsync(param);
        CategoryParams.Remove(param);
    }
}
