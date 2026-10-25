using SQLite;

namespace IPOProcessingSystem.Models;

public class ReconciliationSummary
{
    [PrimaryKey, AutoIncrement]
    public int ReconId { get; set; }
    public int IPO_ID { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string ReconType { get; set; } = "NonASBA";  // ASBA / NonASBA
    public string FormNo { get; set; } = string.Empty;
    public decimal ExcelAmount { get; set; }
    public decimal ApplnAmount { get; set; }
    public string MatchStatus { get; set; } = "PENDING"; // MATCHED / UNMATCHED
    public bool IsNilBank { get; set; }
    // Clean Data Summary totals
    public int TotalRecords { get; set; }
    public decimal TotalAmount { get; set; }
    public int MatchedCount { get; set; }
    public decimal MatchedAmount { get; set; }
    public int UnmatchedCount { get; set; }
    public decimal UnmatchedAmount { get; set; }
}

public class UnmatchRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string FormNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Debent { get; set; }
    public string PAN { get; set; } = string.Empty;
    public string CLID { get; set; } = string.Empty;
    public string ACNO { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string UnmatchType { get; set; } = string.Empty;
    // AVAILABLE_IN_APPLNSX_NOT_SCHEDULE / AVAILABLE_IN_SCHEDULE_DIFF_BANK / AMOUNT_UNMATCH
    public decimal ExcelAmount { get; set; }
    public decimal ApplnAmount { get; set; }
    public bool Resolved { get; set; }
}

/// <summary>Per-bank reconciliation session — tracks Pending vs Completed banks.</summary>
public class ReconSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    [Ignore]
    public int IPO_Id { get => IPO_ID; set => IPO_ID = value; }
    public string BankCode { get; set; } = string.Empty;
    public string ReconType { get; set; } = "NonASBA"; // ASBA / NonASBA
    public string Status { get; set; } = "Pending";   // Pending / Completed
    // Applnsx summary figures
    public int ApplnsxTotalRecords { get; set; }
    public decimal ApplnsxTotalAmount { get; set; }
    public int ApplnsxMatchedCount { get; set; }
    public decimal ApplnsxMatchedAmount { get; set; }
    // Excel (bank file) summary figures
    public int ExcelTotalRecords { get; set; }
    public decimal ExcelTotalAmount { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Records rejected via Technical Rejection — can be reverted (Reverted status).</summary>
public class RejectionRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    [Ignore]
    public int IPO_Id { get => IPO_ID; set => IPO_ID = value; }
    [Ignore]
    public bool IsSelected { get; set; }
    public string FormNo { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Debent { get; set; }
    public decimal Amount { get; set; }
    public string PAN { get; set; } = string.Empty;
    public string CLID { get; set; } = string.Empty;
    public string ACNO { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";    // Active / Reverted
    public DateTime RejectedOn { get; set; } = DateTime.Now;
}

/// <summary>Physical Allotment Intimation letter data — §3.1.1.</summary>
public class PhyAllotmentIntimation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Tel { get; set; } = string.Empty;
    public string? Fax { get; set; }
    public string? Website { get; set; }
    public string EmailId { get; set; } = string.Empty;
    public string ExchangeType { get; set; } = "NSE";  // NSE / BSE
    public string IpoDetails { get; set; } = string.Empty;   // free-text IPO description
    public decimal IssuePrice { get; set; }
    public string ModeRemittance { get; set; } = "ASBA UNBLOCK";
    public string ContactPerson { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string? GeneratedFilePath { get; set; }
    public DateTime? GeneratedOn { get; set; }
}

