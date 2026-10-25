using SQLite;

namespace IPOProcessingSystem.Models;

public class LuckyNumberAllocation
{
    [PrimaryKey, AutoIncrement]
    public int LuckyId { get; set; }
    public int IPO_ID { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Cat { get; set; }           // category total (CAT column in manual)
    public string SearchType { get; set; } = "Type 1"; // Type 1 / Type 2 ...
    public int SearchTypeId { get; set; } = 1;   // Type 1, Type 2, etc.
    public int SequenceId { get; set; }           // Sequential Id within the category range
    public int SerialFrom { get; set; }
    public int SerialTo { get; set; }
    public int RandomSeedNumber { get; set; }
    public int LuckyNo { get; set; }           // actual lucky number assigned
    public bool AllocatedFlag { get; set; }
}
