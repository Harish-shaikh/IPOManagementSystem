using SQLite;

namespace IPOProcessingSystem.Models;

/// <summary>CDSL/NSDL BenDem file generated and verified during T+1 process.</summary>
public class BendemFile
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string Depository { get; set; } = "CDSL"; // CDSL / NSDL
    public string SequenceNo { get; set; } = string.Empty; // e.g. 128307.001
    public string GeneratedPath { get; set; } = string.Empty;
    public string? VerifiedPath { get; set; }
    public bool VerifiedFlag { get; set; }
    public DateTime GeneratedOn { get; set; } = DateTime.Now;
    public DateTime? VerifiedOn { get; set; }
}

/// <summary>Persisted per-bank Excel→SQL column mapping for reconciliation.</summary>
public class ColumnMapping
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;  // Excel column header
    public string TargetColumn { get; set; } = string.Empty;  // internal SQL field name
}

/// <summary>Tracks data transfer steps to B-Tas / Bigshare Online.</summary>
public class DataTransferLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string TransferType { get; set; } = "Issue"; // Issue / Post_Issue / Transfer
    public DateTime TransferDate { get; set; }
    public string Status { get; set; } = "Pending"; // Pending / Done
    public DateTime? TransferredAt { get; set; }
    public bool LockInUpdated { get; set; }
    public DateTime? PreIssueDate { get; set; }
    public DateTime? PostIssueDate { get; set; }
    public string Target { get; set; } = "BTAS"; // BTAS / BigshareOnline
}

/// <summary>Per-category allotment result after lucky draw.</summary>
public class Allocation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Debent { get; set; }    // total shares applied
    public int Appl { get; set; }          // total applications
    public decimal Allot { get; set; }     // shares allotted
    public decimal DBal { get; set; }      // debit balance (refund amount)
}

/// <summary>Client / RTA branch master for the IPO Generate Browse modal.</summary>
public class ClientMaster
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int RtaBranchId { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
}

/// <summary>Day-wise bid upload tracking (one row per exchange/ASBA type/day).</summary>
public class BidUploadLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string Exchange { get; set; } = "BSE"; // NSE / BSE
    public string AsbaType { get; set; } = "BANK ASBA";
    public int Day { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public decimal TotalQuantity { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;
}

/// <summary>FC Email Sender status per bank.</summary>
public class FcEmailStatus
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string? BankAba { get; set; }
    public string? SyndicateAba { get; set; }
    public bool FundTransferDone { get; set; }
    public bool AbaResolutionDone { get; set; }
    public bool ZipDone { get; set; }
    public bool SendDone { get; set; }
}
