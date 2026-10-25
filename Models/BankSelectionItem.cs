using CommunityToolkit.Mvvm.ComponentModel;

namespace IPOProcessingSystem.Models;

/// <summary>
/// UI-only POCO used in the Day Wise and Closing Day bank checklist panels.
/// Not a database entity — created at runtime from BankMaster seed data.
/// </summary>
public partial class BankSelectionItem : ObservableObject
{
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}

public class BankSummaryRow
{
    public string BankCode { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
}

