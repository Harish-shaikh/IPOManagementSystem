using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Allotment Count — read-only per-category allotment summary (F1).</summary>
public partial class AllotmentCountViewModel : BaseViewModel
{
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private decimal totalShares;
    [ObservableProperty] private int totalCount;

    public ObservableCollection<Allocation> AllotmentRows { get; } = new();

    public AllotmentCountViewModel(DatabaseService dbService)
    {
        _dbService = dbService;
        Title = "Allotment Count";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var list = await _dbService.Connection.Table<Allocation>()
            .Where(a => a.IPO_ID == IpoId).ToListAsync();
        AllotmentRows.Clear();
        foreach (var a in list) AllotmentRows.Add(a);
        TotalShares = list.Sum(a => a.Allot);
        TotalCount = list.Sum(a => a.Appl);
    }
}

/// <summary>Applnsx Summary — grand total check across all applications (F1 / Alt shortcut).</summary>
public partial class ApplnsxSummaryViewModel : BaseViewModel
{
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private decimal totalDebent;
    [ObservableProperty] private int totalApplications;
    [ObservableProperty] private decimal totalAmount;

    public ApplnsxSummaryViewModel(DatabaseService dbService)
    {
        _dbService = dbService;
        Title = "Applnsx Summary";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == IpoId).ToListAsync();
        TotalDebent = bids.Sum(b => b.Quantity);
        TotalApplications = bids.Count;
        TotalAmount = bids.Sum(b => b.Amount);
    }
}
