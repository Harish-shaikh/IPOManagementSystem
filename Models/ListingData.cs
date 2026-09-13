using SQLite;

namespace IPOProcessingSystem.Models;

public class ListingData
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public DateTime PreIssueDate { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime PostIssueDate { get; set; }
    public bool LockInUpdated { get; set; }
    public bool TransferredToBTAS { get; set; }
    public bool TransferredToBigshareOnline { get; set; }
}

public class OfferForSaleRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string DPCL { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public string Stage { get; set; } = "PreIssue"; // PreIssue / PostIssue
}

public class CompanyAccountDetail
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string IFSC { get; set; } = string.Empty;
    public string? MICR { get; set; }
    public string? NEFTCode { get; set; }
    public string? Address { get; set; }
    public string? BankBranch { get; set; }
    public string? BankCity { get; set; }
}
