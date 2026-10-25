using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Models;
using IPOProcessingSystem.Services.Interfaces;
using System.Collections.ObjectModel;

namespace IPOProcessingSystem.ViewModels;

public partial class BidUploadViewModel : BaseViewModel
{
    private readonly IBidImportService _bidImportService;
    private readonly IAlertService _alertService;
    private readonly DatabaseService _dbService;
    private List<ApplnsxBid> _stagedRows = new();

    [ObservableProperty] private int ipoId;
    [ObservableProperty] private string exchange = "BSE"; // NSE / BSE
    [ObservableProperty] private string asbaType = "BANK ASBA"; // BANK ASBA / MEMBER ASBA / UPI ASBA
    [ObservableProperty] private string filePath = string.Empty;
    [ObservableProperty] private int totalRecords;
    [ObservableProperty] private decimal totalQuantity;

    public ObservableCollection<ApplnsxBid> PreviewRows { get; } = new();

    public BidUploadViewModel(IBidImportService bidImportService, IAlertService alertService, DatabaseService dbService)
    {
        _bidImportService = bidImportService;
        _alertService = alertService;
        _dbService = dbService;
        Title = "NSE/BSE Bid Upload";
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var fileTypes = Exchange == "BSE"
            ? new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
              {
                  { DevicePlatform.WinUI, new[] { ".dat" } },
                  { DevicePlatform.macOS, new[] { "dat" } }
              })
            : new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
              {
                  { DevicePlatform.WinUI, new[] { ".csv" } },
                  { DevicePlatform.macOS, new[] { "csv" } }
              });

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = $"Select {Exchange} Bid File",
            FileTypes = fileTypes
        });

        if (result != null)
        {
            FilePath = result.FullPath;
        }
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath)) { StatusMessage = "Please browse a file first."; return; }

        if (AsbaType.Contains("MEMBER", StringComparison.OrdinalIgnoreCase))
        {
            var bankCount = await _dbService.Connection.Table<ApplnsxBid>()
                .Where(b => b.IPO_ID == IpoId && b.IsBankFile).CountAsync();
            if (bankCount == 0)
            {
                bool proceed = await _alertService.ConfirmAsync(
                    "Upload Order Warning",
                    "According to RTA rules, BANK ASBA should be uploaded BEFORE MEMBER ASBA.\n\nNo Bank ASBA bids found for this IPO. Do you wish to continue anyway?");
                if (!proceed) return;
            }
        }

        IsBusy = true;
        try
        {
            bool isBankFile = AsbaType.Contains("BANK", StringComparison.OrdinalIgnoreCase) && !AsbaType.Contains("MEMBER");
            _stagedRows = await _bidImportService.ParseBidFileAsync(FilePath, IpoId, AsbaType, isBankFile, Exchange);

            PreviewRows.Clear();
            foreach (var row in _stagedRows.Take(200)) PreviewRows.Add(row); // preview cap for UI

            var result = await _bidImportService.BulkInsertAsync(_stagedRows);
            TotalRecords = result.TotalRecords;
            TotalQuantity = result.TotalQuantity;
            StatusMessage = result.Message;

            await _alertService.ShowSuccessAsync($"{Exchange} - {AsbaType} UPLOADED SUCCESSFULLY",
                $"Total Records: {TotalRecords:N0}\nTotal Quantity: {TotalQuantity:N0}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ClearDataAsync()
    {
        bool confirm = await _alertService.ConfirmAsync("Clear Uploaded Data",
            "Are you sure about Reupload? Existing data will get deleted!");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            var count = await _bidImportService.ClearDataAsync(IpoId, AsbaType);
            PreviewRows.Clear();
            TotalRecords = 0;
            TotalQuantity = 0;
            StatusMessage = $"Data cleared successfully ({count} records removed). Ready for fresh upload.";
            await _alertService.ShowAsync("Data Cleared",
                "Data cleared successfully.\n\nPlease Close and Open New window for Fresh Uploading Process.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task MapDummyIfscAsync()
    {
        var mapped = await _bidImportService.MapDummyIfscAsync(IpoId);
        StatusMessage = $"{mapped} record(s) mapped to a valid bank code.";
        await _alertService.ShowSuccessAsync("Bank Code Updated Successfully.", $"{mapped} record(s) mapped.");
    }
}
