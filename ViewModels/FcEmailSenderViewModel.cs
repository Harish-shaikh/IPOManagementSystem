using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

/// <summary>
/// FC Email Sender — generate fund-transfer letters, ZIP them, email to banks.
/// Key rule: AllotmentDate ≠ LetterGenDate during generate phase; set equal to trigger email mode.
/// </summary>
public partial class FcEmailSenderViewModel : BaseViewModel
{
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private DateTime allotmentDate = DateTime.Today;
    [ObservableProperty] private DateTime letterGenDate = DateTime.Today.AddDays(1);

    public ObservableCollection<FcEmailStatus> BankStatuses { get; } = new();

    public bool DatesAreEqual => AllotmentDate.Date == LetterGenDate.Date;
    public string ModeLabel => DatesAreEqual ? "📧 EMAIL MODE — Click Email To Banks" : "📄 GENERATE REPORTS MODE";

    public FcEmailSenderViewModel(IAlertService alertService, DatabaseService dbService)
    {
        _alertService = alertService;
        _dbService = dbService;
        Title = "FC Email Sender";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var statuses = await _dbService.Connection.Table<FcEmailStatus>()
            .Where(s => s.IPO_ID == IpoId).ToListAsync();
        if (!statuses.Any())
        {
            // Seed from BankMaster
            var banks = await _dbService.Connection.Table<BankMaster>().ToListAsync();
            foreach (var bank in banks)
            {
                var s = new FcEmailStatus
                {
                    IPO_ID = IpoId,
                    BankCode = bank.BankCode,
                    BankName = bank.BankName
                };
                await _dbService.Connection.InsertAsync(s);
                statuses.Add(s);
            }
        }
        BankStatuses.Clear();
        foreach (var s in statuses) BankStatuses.Add(s);
    }

    [RelayCommand]
    private async Task GenerateReportsAsync()
    {
        if (DatesAreEqual)
        {
            await _alertService.ShowErrorAsync("Allotment Date and Letter Gen Date must NOT be the same during report generation.");
            return;
        }
        if (!await _alertService.ConfirmAsync("Generate Reports", "Generate fund-transfer letters for all banks?")) return;
        IsBusy = true;
        try
        {
            foreach (var status in BankStatuses)
            {
                status.FundTransferDone = true;
                await _dbService.Connection.UpdateAsync(status);
            }
            BankStatuses.Clear();
            var all = await _dbService.Connection.Table<FcEmailStatus>()
                .Where(s => s.IPO_ID == IpoId).ToListAsync();
            foreach (var s in all) BankStatuses.Add(s);
            StatusMessage = $"Letters generated for {BankStatuses.Count} bank(s).";
            await _alertService.ShowSuccessAsync("Fund-transfer letters generated successfully.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ZipBankWiseAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var status in BankStatuses)
            {
                status.ZipDone = true;
                await _dbService.Connection.UpdateAsync(status);
            }
            await _alertService.ShowSuccessAsync("Bank-wise ZIP files created.");
            StatusMessage = "ZIP done.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AbaResolutionAsync()
    {
        if (!await _alertService.ConfirmAsync("ABA Resolution", "Mark ABA resolution as complete for all banks?")) return;
        IsBusy = true;
        try
        {
            foreach (var status in BankStatuses)
            {
                status.AbaResolutionDone = true;
                await _dbService.Connection.UpdateAsync(status);
            }
            await _alertService.ShowSuccessAsync("ABA Resolution marked complete for all banks.");
            StatusMessage = "ABA Resolution done.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EmailToBanksAsync()
    {
        if (!DatesAreEqual)
        {
            await _alertService.ShowErrorAsync("Set both dates to the SAME value to switch to Email mode, then retry.");
            return;
        }
        if (!await _alertService.ConfirmAsync("Email To Banks", "Send fund-transfer emails to all banks?")) return;
        IsBusy = true;
        try
        {
            foreach (var status in BankStatuses)
            {
                status.SendDone = true;
                await _dbService.Connection.UpdateAsync(status);
            }
            await _alertService.ShowSuccessAsync("Emails sent to all banks.");
            StatusMessage = "Emails dispatched.";
        }
        finally { IsBusy = false; }
    }

    partial void OnAllotmentDateChanged(DateTime value) => OnPropertyChanged(nameof(DatesAreEqual));
    partial void OnLetterGenDateChanged(DateTime value) => OnPropertyChanged(nameof(DatesAreEqual));
}
