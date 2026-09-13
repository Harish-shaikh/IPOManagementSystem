using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Lucky No screen — export template, import results, update, confirm Done.</summary>
public partial class LuckyNoViewModel : BaseViewModel
{
    private readonly IBasisCalculationService _basisService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string exportPath = string.Empty;
    [ObservableProperty] private string importPath = string.Empty;

    public ObservableCollection<LuckyNumberAllocation> LuckyNumbers { get; } = new();

    private readonly AppState _appState;

    public LuckyNoViewModel(IBasisCalculationService basisService, IAlertService alertService, DatabaseService dbService, AppState appState)
    {
        _basisService = basisService;
        _alertService = alertService;
        _dbService = dbService;
        _appState = appState;
        IpoId = _appState.ActiveIpoId;
        _appState.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppState.ActiveIpoId))
                IpoId = _appState.ActiveIpoId;
        };
        Title = "Lucky No";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var list = await _dbService.Connection.Table<LuckyNumberAllocation>()
            .Where(l => l.IPO_ID == IpoId).ToListAsync();
        LuckyNumbers.Clear();
        foreach (var l in list) LuckyNumbers.Add(l);
    }

    [RelayCommand]
    private async Task ExportTemplateAsync()
    {
        IsBusy = true;
        try
        {
            var ranges = await _basisService.GenerateLuckyDrawRangesAsync(IpoId);
            var path = Path.Combine(FileSystem.AppDataDirectory, "Exports", $"IPO_{IpoId}", $"LuckyNo_{IpoId}_Template.xlsx");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("LuckyNo");
            ws.Cell(1, 1).Value = "IPO_Id"; ws.Cell(1, 2).Value = "Category";
            ws.Cell(1, 3).Value = "SearchType"; ws.Cell(1, 4).Value = "SerialFrom";
            ws.Cell(1, 5).Value = "SerialTo"; ws.Cell(1, 6).Value = "LuckyNo";
            int r = 2;
            foreach (var lucky in ranges)
            {
                ws.Cell(r, 1).Value = lucky.IPO_ID;
                ws.Cell(r, 2).Value = lucky.Category;
                ws.Cell(r, 3).Value = lucky.SearchType;
                ws.Cell(r, 4).Value = lucky.SerialFrom;
                ws.Cell(r, 5).Value = lucky.SerialTo;
                ws.Cell(r, 6).Value = 0; // to be filled manually
                r++;
            }
            wb.SaveAs(path);
            ExportPath = path;
            await _alertService.ShowSuccessAsync("Lucky No template exported. Fill in the LuckyNo column and import.", path);
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BrowseImportAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Lucky No Excel file"
        });
        if (result != null) ImportPath = result.FullPath;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportPath)) { StatusMessage = "Select import file first."; return; }
        IsBusy = true;
        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook(ImportPath);
            var ws = wb.Worksheets.First();
            var header = ws.Row(1);
            // Category col=2, SearchType=3, SerialFrom=4, SerialTo=5, LuckyNo=6
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var category = row.Cell(2).GetValue<string>();
                var luckyNo = row.Cell(6).GetValue<int>();
                var existing = await _dbService.Connection.Table<LuckyNumberAllocation>()
                    .Where(l => l.IPO_ID == IpoId && l.Category == category)
                    .FirstOrDefaultAsync();
                if (existing != null)
                {
                    existing.RandomSeedNumber = luckyNo;
                    await _dbService.Connection.UpdateAsync(existing);
                }
            }
            await _alertService.ShowSuccessAsync("Lucky No Inserted Successfully.");
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UpdateLuckyNoAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var lucky in LuckyNumbers)
                await _dbService.Connection.UpdateAsync(lucky);
            await _alertService.ShowSuccessAsync("Lucky No Inserted Successfully.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LuckyNoDoneAsync()
    {
        if (!await _alertService.ConfirmAsync("Lucky No Done", "Confirm Lucky No is final?")) return;
        IsBusy = true;
        try
        {
            foreach (var lucky in LuckyNumbers)
            {
                lucky.AllocatedFlag = true;
                await _dbService.Connection.UpdateAsync(lucky);
            }
            await _alertService.ShowSuccessAsync("Lucky No Update Confirm.");
            StatusMessage = "Lucky No confirmed.";
        }
        finally { IsBusy = false; }
    }
}
