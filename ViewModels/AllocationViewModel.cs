using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Allocation screen — ALLOTMENT + REFUND process per category.</summary>
public partial class AllocationViewModel : BaseViewModel
{
    private readonly IAllocationService _allocationService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private decimal grandTotalDebent;
    [ObservableProperty] private int grandTotalAppl;
    [ObservableProperty] private decimal grandTotalAllot;
    [ObservableProperty] private decimal grandTotalDbal;

    public ObservableCollection<Allocation> Allocations { get; } = new();

    public AllocationViewModel(IAllocationService allocationService, IAlertService alertService, DatabaseService dbService)
    {
        _allocationService = allocationService;
        _alertService = alertService;
        _dbService = dbService;
        Title = "Allocation";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var list = await _allocationService.GetAllocationsAsync(IpoId);
        Allocations.Clear();
        foreach (var a in list) Allocations.Add(a);
        UpdateTotals();
    }

    [RelayCommand]
    private async Task RunAllotmentAsync()
    {
        if (!await _alertService.ConfirmAsync("Allotment", "Run Allotment Process? This will compute final share allocations.")) return;
        IsBusy = true;
        try
        {
            var results = await _allocationService.RunAllotmentAsync(IpoId);
            Allocations.Clear();
            foreach (var a in results) Allocations.Add(a);
            UpdateTotals();
            await _alertService.ShowSuccessAsync("Allotment Process completed successfully.");
            StatusMessage = $"Allotment done. {results.Count} category/ies processed.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RunRefundAsync()
    {
        if (!await _alertService.ConfirmAsync("Refund Process", "Run Refund calculation?")) return;
        IsBusy = true;
        try
        {
            var results = await _allocationService.RunRefundProcessAsync(IpoId);
            Allocations.Clear();
            foreach (var a in results) Allocations.Add(a);
            UpdateTotals();
            await _alertService.ShowSuccessAsync("Refund Process completed successfully.");
            StatusMessage = $"Refund calculation done for {results.Count} category/ies.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (!await _alertService.ConfirmAsync("Reset Allocation", "Reset all allocation data?")) return;
        IsBusy = true;
        try
        {
            await _allocationService.ResetAllocationAsync(IpoId);
            Allocations.Clear();
            UpdateTotals();
            StatusMessage = "Allocation reset.";
        }
        finally { IsBusy = false; }
    }

    private void UpdateTotals()
    {
        GrandTotalDebent = Allocations.Sum(a => a.Debent);
        GrandTotalAppl = Allocations.Sum(a => a.Appl);
        GrandTotalAllot = Allocations.Sum(a => a.Allot);
        GrandTotalDbal = Allocations.Sum(a => a.DBal);
    }
}
