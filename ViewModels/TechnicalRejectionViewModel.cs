using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>
/// Technical Rejection page — all 16 buttons per the spec.
/// </summary>
public partial class TechnicalRejectionViewModel : BaseViewModel
{
    private readonly IRejectionEngineService _rejectionService;
    private readonly IReportService _reportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string promoterPans = string.Empty;
    [ObservableProperty] private string sebiDebarredPans = string.Empty;
    [ObservableProperty] private DateTime hniCutOffDate = DateTime.Today;
    [ObservableProperty] private string hniCutOffTime = "15:00";
    [ObservableProperty] private string marketMakerSearchName = string.Empty;
    [ObservableProperty] private ApplnsxBid? selectedBid;
    [ObservableProperty] private TechnicalRejection? selectedRejection;
    [ObservableProperty] private string selectedCategory = "MARKET MAKER";

    public ObservableCollection<TechnicalRejection> Rejections { get; } = new();
    public ObservableCollection<ApplnsxBid> SearchResults { get; } = new();
    public ObservableCollection<RejectionChecklistItem> ChecklistItems { get; } = new();
    public ObservableCollection<BasisCategoryParameter> BasisCategories { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { "RETAIL", "HNI", "QIB", "MARKET MAKER", "EMPLOYEE", "DEVOLVEMENT" };

    // ── Panel visibility toggles ────────────────────────────────────────────
    [ObservableProperty] private bool showRejectionChecklist;
    [ObservableProperty] private bool showMarketMakerPanel;
    [ObservableProperty] private bool showBasisCategoryPanel;
    [ObservableProperty] private bool showAddBasisForm;

    // ── New basis category add form fields ─────────────────────────────────
    [ObservableProperty] private string newCategory = string.Empty;
    [ObservableProperty] private decimal newPSize;
    [ObservableProperty] private decimal newPSize1;
    [ObservableProperty] private int newLot;
    [ObservableProperty] private string newLName = string.Empty;

    public TechnicalRejectionViewModel(IRejectionEngineService rejectionService, IReportService reportService,
        IAlertService alertService, DatabaseService dbService)
    {
        _rejectionService = rejectionService;
        _reportService = reportService;
        _alertService = alertService;
        _dbService = dbService;
        Title = "Technical Rejection";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var items = await _dbService.Connection.Table<RejectionChecklistItem>().ToListAsync();
        ChecklistItems.Clear();
        foreach (var i in items) ChecklistItems.Add(i);
        await LoadRejectionsAsync();
        await LoadBasisCategoriesAsync();
    }

    [RelayCommand]
    private async Task CheckPromoterPanAsync()
    {
        if (string.IsNullOrWhiteSpace(PromoterPans)) { StatusMessage = "Enter PAN(s)."; return; }
        IsBusy = true;
        try
        {
            var pans = PromoterPans.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var results = await _rejectionService.CheckPromoterPanAsync(IpoId, pans);
            StatusMessage = $"{results.Count} Promoter PAN rejection(s) marked.";
            await LoadRejectionsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RevertPromoterPanAsync()
    {
        if (string.IsNullOrWhiteSpace(PromoterPans)) { StatusMessage = "Enter PAN(s) to revert."; return; }
        IsBusy = true;
        try
        {
            var pans = PromoterPans.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var rejections = await _dbService.Connection.Table<TechnicalRejection>()
                .Where(r => r.IPO_ID == IpoId && r.ReasonCode == "PromoterPAN").ToListAsync();
            foreach (var rej in rejections.Where(r => pans.Contains(r.PAN)))
                await _dbService.Connection.DeleteAsync(rej);
            StatusMessage = "Promoter PAN rejections reverted.";
            await LoadRejectionsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckSebiDebarredAsync()
    {
        IsBusy = true;
        try
        {
            var pans = SebiDebarredPans.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var results = await _rejectionService.CheckSebiDebarredAsync(IpoId, pans);
            StatusMessage = $"{results.Count} SEBI Debarred rejection(s) marked.";
            await LoadRejectionsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckHniCutOffAsync()
    {
        IsBusy = true;
        try
        {
            var cutOff = TimeSpan.Parse(HniCutOffTime);
            var results = await _rejectionService.CheckHniCutOffAsync(IpoId, cutOff, HniCutOffDate);
            StatusMessage = $"{results.Count} HNI Cut-off rejection(s) marked.";
            await _alertService.ShowSuccessAsync($"HNI Cut-off: {results.Count} application(s) flagged.");
            await LoadRejectionsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckUsaApplicantsAsync()
    {
        IsBusy = true;
        try
        {
            var keywords = new[] { "USA", "UNITED STATES", "US", "AMERICA" };
            var results = await _rejectionService.CheckUsaApplicantAsync(IpoId, keywords);
            StatusMessage = $"{results.Count} USA Applicant rejection(s) marked.";
            await LoadRejectionsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckDuplicatePanAsync()
    {
        IsBusy = true;
        try
        {
            var results = await _rejectionService.CheckDuplicatePanAsync(IpoId);
            if (results.Count == 0)
                await _alertService.ShowAsync("Duplicate PAN", "There is no duplicate PAN available for output.");
            else
            {
                StatusMessage = $"{results.Count} Duplicate PAN rejection(s) marked.";
                await LoadRejectionsAsync();
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckMultiplePanAsync()
    {
        IsBusy = true;
        try
        {
            var results = await _rejectionService.CheckMultiplePanAsync(IpoId);
            if (results.Count == 0)
                await _alertService.ShowAsync("Multiple PAN", "No duplicate PAN available.");
            else
            {
                StatusMessage = $"{results.Count} Multiple PAN rejection(s) marked.";
                await LoadRejectionsAsync();
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckMoreThanOneAsync()
    {
        IsBusy = true;
        try
        {
            var results = await _rejectionService.CheckMultipleApplicationsAsync(IpoId, maxAllowed: 1);
            StatusMessage = $"{results.Count} More-Than-One rejection(s) marked.";
            await LoadRejectionsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckMinorDetailAsync()
    {
        IsBusy = true;
        try
        {
            var results = await _rejectionService.CheckMinorDetailAsync(IpoId);
            StatusMessage = $"{results.Count} Minor Detail rejection(s) marked.";
            await LoadRejectionsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckPanMismatchAsync()
    {
        IsBusy = true;
        try
        {
            var results = await _rejectionService.CheckPanMismatchAsync(IpoId);
            StatusMessage = $"{results.Count} PAN Mismatch rejection(s) marked.";
            await LoadRejectionsAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UpdateMarketMakerAsync()
    {
        if (string.IsNullOrWhiteSpace(MarketMakerSearchName)) { StatusMessage = "Enter Order No(s) / PAN for Market Maker."; return; }
        IsBusy = true;
        try
        {
            var orderNos = MarketMakerSearchName.Split(',', StringSplitOptions.RemoveEmptyEntries);
            int count = await _rejectionService.UpdateMarketMakerCategoryAsync(IpoId, orderNos);
            StatusMessage = $"{count} application(s) updated to MARKET MAKER.";
            await _alertService.ShowSuccessAsync($"{count} application(s) marked as MARKET MAKER.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveChecklistAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var item in ChecklistItems)
                await _dbService.Connection.UpdateAsync(item);
            await _alertService.ShowSuccessAsync("Record Inserted/Updated Successfully.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RunRejectionMarkingAsync()
    {
        if (!await _alertService.ConfirmAsync("Rejection Marking", "Run rejection marking against all flagged categories?")) return;
        IsBusy = true;
        try
        {
            int count = await _rejectionService.ConfirmRejectionsAsync(IpoId);
            await _alertService.ShowSuccessAsync($"Rejection Marking: {count} record(s) marked as REJECTED.");
            StatusMessage = $"Rejection marking complete. {count} records.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RejectionConfirmAsync()
    {
        if (!await _alertService.ConfirmAsync("Rejection Confirm", "Confirm and lock all rejections?")) return;
        IsBusy = true;
        try
        {
            int count = await _rejectionService.ConfirmRejectionsAsync(IpoId);
            await _alertService.ShowSuccessAsync("Rejection Confirm: Successful.");
            StatusMessage = $"Rejection confirmed. {count} record(s).";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateWebReturnAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateTechnicalRejectionReportAsync(IpoId);
            bool open = await _alertService.ConfirmAsync("Web Return", $"File saved successfully!\n{path}\nDo you want to open?");
            if (open) await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(path) });
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _reportService.GenerateTechnicalRejectionReportAsync(IpoId);
            await _alertService.ShowSuccessAsync("Technical Rejection report generated.", path);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddBasisCategoryAsync()
    {
        // Opens inline category add — UI handles the form fields
        StatusMessage = "Fill category details and click Save.";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveBasisCategoryAsync(BasisCategoryParameter param)
    {
        if (param.Id == 0)
            await _dbService.Connection.InsertAsync(param);
        else
            await _dbService.Connection.UpdateAsync(param);
        await _alertService.ShowSuccessAsync("Basis Category saved.");
        await LoadBasisCategoriesAsync();
    }

    [RelayCommand]
    private async Task DeleteBasisCategoryAsync(BasisCategoryParameter param)
    {
        await _dbService.Connection.DeleteAsync(param);
        await LoadBasisCategoriesAsync();
    }

    private async Task LoadRejectionsAsync()
    {
        var list = await _dbService.Connection.Table<TechnicalRejection>()
            .Where(r => r.IPO_ID == IpoId).ToListAsync();
        Rejections.Clear();
        foreach (var r in list) Rejections.Add(r);
    }

    private async Task LoadBasisCategoriesAsync()
    {
        var list = await _dbService.Connection.Table<BasisCategoryParameter>()
            .Where(p => p.IPO_ID == IpoId).ToListAsync();
        BasisCategories.Clear();
        foreach (var p in list) BasisCategories.Add(p);
    }

    // ── Panel Toggles ────────────────────────────────────────────────────────
    [RelayCommand]
    private void ToggleRejectionChecklist() => ShowRejectionChecklist = !ShowRejectionChecklist;

    [RelayCommand]
    private void ToggleMarketMakerPanel() => ShowMarketMakerPanel = !ShowMarketMakerPanel;

    [RelayCommand]
    private void ToggleBasisCategoryPanel() => ShowBasisCategoryPanel = !ShowBasisCategoryPanel;

    [RelayCommand]
    private void SelectAllChecklist()
    {
        foreach (var item in ChecklistItems) item.Include = true;
        // Force UI refresh
        var temp = new List<RejectionChecklistItem>(ChecklistItems);
        ChecklistItems.Clear();
        foreach (var item in temp) ChecklistItems.Add(item);
    }

    /// <summary>Search ApplnsxBid by name/OrderNo for Market Maker reclassification.</summary>
    [RelayCommand]
    private async Task SearchMarketMakerAsync()
    {
        if (string.IsNullOrWhiteSpace(MarketMakerSearchName)) return;
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == IpoId).ToListAsync();
        var term = MarketMakerSearchName.Trim();
        var results = bids.Where(b =>
            (b.ClientName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (b.OrderNo ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (b.PAN ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        SearchResults.Clear();
        foreach (var r in results) SearchResults.Add(r);
        StatusMessage = $"{results.Count} result(s) found.";
    }

    [RelayCommand]
    private void ShowAddBasisCategory() => ShowAddBasisForm = !ShowAddBasisForm;

    [RelayCommand]
    private async Task SaveNewBasisCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategory)) { StatusMessage = "Category name required."; return; }
        var param = new BasisCategoryParameter
        {
            IPO_ID = IpoId,
            Category = NewCategory,
            PSize = NewPSize,
            PSize1 = NewPSize1,
            Lot = NewLot,
            LName = NewLName
        };
        await _dbService.Connection.InsertAsync(param);
        await _alertService.ShowSuccessAsync("Basis Category saved.");
        NewCategory = string.Empty; NewPSize = 0; NewPSize1 = 0; NewLot = 0; NewLName = string.Empty;
        ShowAddBasisForm = false;
        await LoadBasisCategoriesAsync();
    }
}

