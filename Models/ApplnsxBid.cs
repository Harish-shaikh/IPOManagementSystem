using SQLite;

namespace IPOProcessingSystem.Models;

// Applnsx: IPO Bids / Staging Table
public class ApplnsxBid
{
    [PrimaryKey, AutoIncrement]
    public long RowId { get; set; }
    [Indexed(Unique = false)]
    public int IPO_ID { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string FormNo { get; set; } = string.Empty;        // Application form number
    public string Name { get; set; } = string.Empty;          // Applicant name
    public string OrderNo { get; set; } = string.Empty;
    public string BookId { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }        // "Debent" in source spec
    public decimal Amount { get; set; }
    [Indexed(Unique = false)]
    public string PAN { get; set; } = string.Empty;
    public string DPID { get; set; } = string.Empty;
    [Indexed(Unique = false)]
    public string CLID { get; set; } = string.Empty;
    public string RefNo { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    [Indexed(Unique = false)]
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Category { get; set; } = "RETAIL"; // Retail, HNI, Market Maker, Employee
    public string ASBA_Type { get; set; } = "BANK";  // Bank ASBA / Member ASBA / UPI
    public string Status { get; set; } = "PENDING";  // PENDING/MATCHED/REJECTED/ALLOTTED
    public string? RejectionCode { get; set; }
    public string? EmailId { get; set; }              // Applicant email (for email count)
    public DateTime EntryDate { get; set; } = DateTime.Now;
    public DateTime? ApplicationDate { get; set; }    // Date of bid submission
    public bool IsBankFile { get; set; }  // true = Bank ASBA source, false = Member ASBA
    public bool IsTechnicallyRejected { get; set; }   // flagged by RejectionEngine
    public int? AllottedQuantity { get; set; }         // shares allotted (0 = refund)
}

