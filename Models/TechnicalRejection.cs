using SQLite;

namespace IPOProcessingSystem.Models;

public class TechnicalRejection
{
    [PrimaryKey, AutoIncrement]
    public int RejectionId { get; set; }
    public int IPO_ID { get; set; }
    public string FormNo { get; set; } = string.Empty;
    public string PAN { get; set; } = string.Empty;
    public string CLID { get; set; } = string.Empty;

    // ReasonCode: PromoterPAN, SEBIDebarred, DuplicatePAN, USAApplicant,
    // HNICutOff, IncorrectAmount, InvalidDemat, MinorDetail, MoreThanOne
    public string ReasonCode { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime RejectedOn { get; set; } = DateTime.Now;
}

public class RejectionChecklistItem
{
    [PrimaryKey, AutoIncrement]
    public int RejId { get; set; }
    public string MnemonicVar { get; set; } = string.Empty; // matches "mem_var" column in manual (!, #, $, A, B ...)
    public string ReasonText { get; set; } = string.Empty;
    public bool Include { get; set; } = true;
}
