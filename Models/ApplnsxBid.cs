using SQLite;

namespace IPOProcessingSystem.Models;

// Applnsx: IPO Bids / Staging Table — full field set per spec §6 APPLNSX data model
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
    public decimal Price { get; set; }       // Bid price per share
    public decimal Amount { get; set; }
    [Indexed(Unique = false)]
    public string PAN { get; set; } = string.Empty;
    public string DPID { get; set; } = string.Empty;
    [Indexed(Unique = false)]
    public string CLID { get; set; } = string.Empty;
    public string RefNo { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;     // ASBAACNO / ACNO
    [Indexed(Unique = false)]
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Category { get; set; } = "RETAIL"; // Retail, HNI, Market Maker, Employee
    public string ASBA_Type { get; set; } = "BANK";  // Bank ASBA / Member ASBA / UPI
    public string Status { get; set; } = "PENDING";  // PENDING/MATCHED/REJECTED/ALLOTTED
    public string? RejectionCode { get; set; }
    public string? EmailId { get; set; }              // Applicant email (for email count)
    public string MobileNo { get; set; } = string.Empty; // Mobile number (for SMS data)
    public DateTime EntryDate { get; set; } = DateTime.Now;
    public DateTime? ApplicationDate { get; set; }    // Date of bid submission
    public bool IsBankFile { get; set; }  // true = Bank ASBA source, false = Member ASBA
    public bool IsTechnicallyRejected { get; set; }   // flagged by RejectionEngine
    public int? AllottedQuantity { get; set; }         // shares allotted (0 = refund)
    [Ignore]
    public int? AllottedShares { get => AllottedQuantity; set => AllottedQuantity = value; }

    // --- Fields added per spec §6 APPLNSX data model ---
    public string BranchCode { get; set; } = string.Empty;    // BRN_CODE
    public string BrokerCode { get; set; } = string.Empty;    // BROK_CD
    public string SubBroker { get; set; } = string.Empty;     // SUB_BROK
    public string SynMem { get; set; } = string.Empty;        // SYN_MEM (syndicate member)
    public string IFSC { get; set; } = string.Empty;          // IFSC code for NSDL Bendem mapping
    public string OK { get; set; } = string.Empty;            // Rejection flag ('$' = other-type rejection, blank = none)
    public string Remark { get; set; } = string.Empty;        // REMARK field used in recon grids

    // Address fields — populated during BenDem demographic merge (USA Applicant check)
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    // OK1 — Minor Detail flag: '$' = different rejection type, leave alone; blank = genuine minor
    public string OK1 { get; set; } = string.Empty;

    // MERGE_AC_NO — account number used to group "More Than One Application" cases
    public string MergeAcNo { get; set; } = string.Empty;
}
