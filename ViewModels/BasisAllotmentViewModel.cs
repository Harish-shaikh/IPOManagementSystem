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

    // Manual Upload Tab
    [ObservableProperty] private bool isManualUploadTab;
    [ObservableProperty] private string manualFilePath = string.Empty;
    [ObservableProperty] private int manualUploadedCount;

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
            LastExportPath = await _basisService.ExportBseFormatAsync(IpoId);
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
            LastExportPath = await _basisService.ExportNetBasNseAsync(IpoId);
            await _alertService.ShowSuccessAsync("NSE NetBAS exported.", LastExportPath);
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

    [RelayCommand]
    private void SwitchToAutoTab() => IsManualUploadTab = false;

    [RelayCommand]
    private void SwitchToManualTab() => IsManualUploadTab = true;

    [RelayCommand]
    private async Task BrowseManualFileAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Manual Basis File (.xlsx / .csv)"
        });
        if (result != null)
        {
            ManualFilePath = result.FullPath;
        }
    }

    [RelayCommand]
    private async Task UploadManualBasisAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualFilePath) || !File.Exists(ManualFilePath))
        {
            await _alertService.ShowAsync("File Required", "Please browse and select a valid .xlsx or .csv file first.");
            return;
        }

        IsBusy = true;
        try
        {
            var rowsToInsert = new List<BasisOfAllotment>();
            string ext = Path.GetExtension(ManualFilePath).ToLowerInvariant();

            if (ext == ".xlsx")
            {
                using var wb = new ClosedXML.Excel.XLWorkbook(ManualFilePath);
                var ws = wb.Worksheet(1);
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                for (int r = 2; r <= lastRow; r++)
                {
                    string cat = ws.Cell(r, 1).GetString().Trim();
                    if (string.IsNullOrEmpty(cat)) continue;

                    int.TryParse(ws.Cell(r, 2).GetString(), out int serial);
                    int.TryParse(ws.Cell(r, 3).GetString(), out int applns);
                    decimal.TryParse(ws.Cell(r, 4).GetString(), out decimal shrApld);
                    decimal.TryParse(ws.Cell(r, 5).GetString(), out decimal propAlloc);
                    int.TryParse(ws.Cell(r, 6).GetString(), out int mlot);
                    int.TryParse(ws.Cell(r, 7).GetString(), out int noAlto);
                    decimal.TryParse(ws.Cell(r, 8).GetString(), out decimal shrAlto);
                    int.TryParse(ws.Cell(r, 9).GetString(), out int num);
                    int.TryParse(ws.Cell(r, 10).GetString(), out int denom);

                    rowsToInsert.Add(new BasisOfAllotment
                    {
                        IPO_ID = IpoId,
                        Category = cat,
                        Serial = serial,
                        Applns = applns,
                        ShrApld = shrApld,
                        PropAlloc = propAlloc,
                        MLot = mlot,
                        NoAlto = noAlto,
                        ShrAlto = shrAlto,
                        Numerator = num <= 0 ? 1 : num,
                        Denomin = denom <= 0 ? 1 : denom,
                        Excess = Math.Max(0, shrApld - shrAlto),
                        AppPercent = applns > 0 ? (noAlto * 100m / applns) : 0,
                        ShrPercent = shrApld > 0 ? (shrAlto * 100m / shrApld) : 0
                    });
                }
            }
            else
            {
                var lines = await File.ReadAllLinesAsync(ManualFilePath);
                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(new[] { ',', '\t', '|' });
                    if (parts.Length < 4) continue;

                    string cat = parts[0].Trim();
                    int.TryParse(parts.Length > 1 ? parts[1] : "0", out int serial);
                    int.TryParse(parts.Length > 2 ? parts[2] : "0", out int applns);
                    decimal.TryParse(parts.Length > 3 ? parts[3] : "0", out decimal shrApld);
                    decimal.TryParse(parts.Length > 4 ? parts[4] : "0", out decimal propAlloc);
                    int.TryParse(parts.Length > 5 ? parts[5] : "0", out int mlot);
                    int.TryParse(parts.Length > 6 ? parts[6] : "0", out int noAlto);
                    decimal.TryParse(parts.Length > 7 ? parts[7] : "0", out decimal shrAlto);

                    rowsToInsert.Add(new BasisOfAllotment
                    {
                        IPO_ID = IpoId,
                        Category = cat,
                        Serial = serial,
                        Applns = applns,
                        ShrApld = shrApld,
                        PropAlloc = propAlloc,
                        MLot = mlot,
                        NoAlto = noAlto,
                        ShrAlto = shrAlto,
                        Numerator = 1,
                        Denomin = 1,
                        Excess = Math.Max(0, shrApld - shrAlto),
                        AppPercent = applns > 0 ? (noAlto * 100m / applns) : 0,
                        ShrPercent = shrApld > 0 ? (shrAlto * 100m / shrApld) : 0
                    });
                }
            }

            if (rowsToInsert.Count == 0)
            {
                await _alertService.ShowAsync("No Rows", "No valid basis rows could be read from the file.");
                return;
            }

            // Remove existing basis for this IPO
            var existing = await _dbService.Connection.Table<BasisOfAllotment>().Where(b => b.IPO_ID == IpoId).ToListAsync();
            foreach (var e in existing) await _dbService.Connection.DeleteAsync(e);

            await _dbService.Connection.InsertAllAsync(rowsToInsert);
            ManualUploadedCount = rowsToInsert.Count;
            StatusMessage = $"Successfully uploaded {ManualUploadedCount} manual basis rows.";
            await _alertService.ShowSuccessAsync($"Uploaded {ManualUploadedCount} manual basis rows successfully.");
            await LoadAsync();
            IsManualUploadTab = false;
        }
        catch (Exception ex)
        {
            await _alertService.ShowErrorAsync($"Manual upload error: {ex.Message}");
        }
        finally { IsBusy = false; }
    }
}

