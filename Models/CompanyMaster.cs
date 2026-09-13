using SQLite;

namespace IPOProcessingSystem.Models;

// CompanyMaster (Pre-IPO)
public class CompanyMaster
{
    [PrimaryKey, AutoIncrement]
    public int IPO_ID { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;    // auto-filled on browse
    public string IssuerName { get; set; } = string.Empty;
    public string IssueType { get; set; } = "SME";            // SME / MAIN BOARD
    public string Exchange { get; set; } = "ONLY NSE";        // ONLY NSE / NSE+BSE / ONLY BSE
    public decimal IssueSize { get; set; }
    public decimal FaceValue { get; set; }
    public decimal IssuePrice { get; set; }      // Issue per share (Offer price)
    public decimal FloorPrice { get; set; }
    public decimal CapPrice { get; set; }         // Ceiling/Cap price
    public int MinLotSize { get; set; }
    public decimal MaxApplyNetIssue { get; set; }
    public DateTime OpeningDate { get; set; }
    public DateTime ClosingDate { get; set; }
    public string? LeadManager1 { get; set; }
    public string? LeadManager2 { get; set; }
    public bool OfferForSale { get; set; }        // false = "Unavailable"
    public bool ExchangeApproved { get; set; }   // basis approved flag
}
