using SQLite;

namespace IPOProcessingSystem.Models;

public class BankMaster
{
    [PrimaryKey]
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    [Indexed(Unique = false)]
    public string IFSC_Code { get; set; } = string.Empty;
    public string? MappedBankCode { get; set; }
    public bool IsNilForCurrentIpo { get; set; }
}

public class DummyIfscMapping
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int IPO_ID { get; set; }
    public string IFSC_CODE { get; set; } = string.Empty;
    public string BANK_CODE { get; set; } = string.Empty;
}
