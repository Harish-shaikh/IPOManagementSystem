using SQLite;

namespace IPOProcessingSystem.Models;

public class BasisOfAllotment
{
    [PrimaryKey, AutoIncrement]
    public int BasisId { get; set; }
    public int IPO_ID { get; set; }
    public string Category { get; set; } = string.Empty;   // Retail/HNI/Market Maker/Employee
    public int Serial { get; set; }
    public int Applns { get; set; }         // No. of applications (APPLNS)
    public decimal ShrApld { get; set; }    // Shares applied (SHRAPLD)
    public decimal PropAlloc { get; set; }  // Proportionate allocation (PROPALLOC)
    public decimal Proper { get; set; }     // PROPER
    public int MLot { get; set; }           // Min lot (MLOT)
    public int NoAlto { get; set; }         // No. allotted (NOALTD)
    public decimal ShrAlto { get; set; }    // Shares allotted (SHRALTD)
    public int Numerator { get; set; } = 1;
    public int Denomin { get; set; } = 1;
    public decimal Excess { get; set; }
    public decimal AppPercent { get; set; }  // APPERCE
    public decimal ShrPercent { get; set; }  // SHRPERCE
}

// Per-category lot-size parameters (IPO_TMAST in manual)
public class BasisCategoryParameter
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public int CategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal PSize { get; set; }   // total shares reserved for category
    public decimal PSize1 { get; set; }  // working copy, adjustable
    public decimal OverSubscription { get; set; }
    public int Lot { get; set; }         // min lot for this category
}
