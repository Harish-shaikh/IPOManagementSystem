using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>Data Transfer — B-Tas (Issue/PostIssue + Lock-in) and Bigshare Online.</summary>
public partial class DataTransferViewModel : BaseViewModel
{
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string transferType = "Issue"; // Issue / Post_Issue
    [ObservableProperty] private DateTime transferDate = DateTime.Today;
    [ObservableProperty] private DateTime preIssueDate = DateTime.Today;
    [ObservableProperty] private DateTime postIssueDate = DateTime.Today.AddDays(2);
    [ObservableProperty] private string btasStatus = string.Empty;
    [ObservableProperty] private int bigshareRecordCount;
    [ObservableProperty] private string bigshareStatus = string.Empty;

    public List<string> TransferTypes { get; } = new() { "Issue", "Post_Issue", "Transfer" };

    public DataTransferViewModel(IAlertService alertService, DatabaseService dbService)
    {
        _alertService = alertService;
        _dbService = dbService;
        Title = "Data Transfer (BTAS / Website)";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var logs = await _dbService.Connection.Table<DataTransferLog>()
            .Where(l => l.IPO_ID == IpoId).ToListAsync();
        var issue = logs.FirstOrDefault(l => l.TransferType == "Issue");
        var postIssue = logs.FirstOrDefault(l => l.TransferType == "Post_Issue");

        var parts = new List<string>();
        if (issue != null) parts.Add($"Issue: Done - {issue.TransferDate:dd/MM/yyyy}");
        if (postIssue != null) parts.Add($"Post_Issue: Done - {postIssue.TransferDate:dd/MM/yyyy}");
        BtasStatus = parts.Count > 0 ? string.Join(" | ", parts) : "No transfers yet.";

        var bids = await _dbService.Connection.Table<ApplnsxBid>()
            .Where(b => b.IPO_ID == IpoId && b.Status == "ALLOTTED").ToListAsync();
        BigshareRecordCount = bids.Count;
    }

    [RelayCommand]
    private async Task TransferBtasAsync()
    {
        if (!await _alertService.ConfirmAsync("B-Tas Transfer", $"Transfer {TransferType} data to B-Tas?")) return;
        IsBusy = true;
        try
        {
            var log = new DataTransferLog
            {
                IPO_ID = IpoId,
                TransferType = TransferType,
                TransferDate = TransferDate,
                Status = "Done",
                TransferredAt = DateTime.Now,
                Target = "BTAS"
            };
            await _dbService.Connection.InsertAsync(log);
            await LoadAsync();
            await _alertService.ShowSuccessAsync($"B-Tas {TransferType} transfer completed.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UpdateLockInAsync()
    {
        // Validate both Issue and PostIssue are done
        var logs = await _dbService.Connection.Table<DataTransferLog>()
            .Where(l => l.IPO_ID == IpoId && l.Target == "BTAS").ToListAsync();
        bool issueDone = logs.Any(l => l.TransferType == "Issue");
        bool postDone = logs.Any(l => l.TransferType == "Post_Issue");

        if (!issueDone || !postDone)
        {
            await _alertService.ShowErrorAsync("⚠ Run both Issue AND Post_Issue B-Tas transfers before updating Lock-in.");
            return;
        }

        if (!await _alertService.ConfirmAsync("Update Lock-In", "Update Lock-In for both Pre and Post issue dates?")) return;

        IsBusy = true;
        try
        {
            var log = logs.First();
            log.LockInUpdated = true;
            log.PreIssueDate = PreIssueDate;
            log.PostIssueDate = PostIssueDate;
            await _dbService.Connection.UpdateAsync(log);
            await _alertService.ShowSuccessAsync("Lock-In updated successfully.");
            StatusMessage = "Lock-in updated.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TransferBigshareAsync()
    {
        IsBusy = true;
        try
        {
            // Rule: <20,000 → Transfer Records, ≥20,000 → BackUp
            bool useTransfer = BigshareRecordCount < 20000;
            string method = useTransfer ? "Transfer Records" : "BackUp";
            if (!await _alertService.ConfirmAsync("Bigshare Online", $"{BigshareRecordCount} records found. Using '{method}'. Proceed?")) return;

            var log = new DataTransferLog
            {
                IPO_ID = IpoId,
                TransferType = "Transfer",
                TransferDate = DateTime.Today,
                Status = "Done",
                TransferredAt = DateTime.Now,
                Target = "BigshareOnline"
            };
            await _dbService.Connection.InsertAsync(log);
            BigshareStatus = $"{method} completed — {BigshareRecordCount} records.";
            await _alertService.ShowSuccessAsync($"Bigshare Online {method} completed.");
        }
        finally { IsBusy = false; }
    }
}
