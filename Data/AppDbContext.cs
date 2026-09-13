using IPOProcessingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace IPOProcessingSystem.Data;

/// <summary>
/// EF Core Sqlite context - used where LINQ-based querying/reporting is more
/// convenient than raw sqlite-net-pcl (e.g. joins for report generation).
/// The high-throughput bid-import path uses DatabaseService (sqlite-net-pcl)
/// directly for RunInTransactionAsync batch performance.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<CompanyMaster> CompanyMasters => Set<CompanyMaster>();
    public DbSet<ApplnsxBid> ApplnsxBids => Set<ApplnsxBid>();
    public DbSet<BankMaster> BankMasters => Set<BankMaster>();
    public DbSet<ReconciliationSummary> ReconciliationSummaries => Set<ReconciliationSummary>();
    public DbSet<UnmatchRecord> UnmatchRecords => Set<UnmatchRecord>();
    public DbSet<TechnicalRejection> TechnicalRejections => Set<TechnicalRejection>();
    public DbSet<BasisOfAllotment> BasisOfAllotments => Set<BasisOfAllotment>();
    public DbSet<LuckyNumberAllocation> LuckyNumberAllocations => Set<LuckyNumberAllocation>();
    public DbSet<ListingData> ListingData => Set<ListingData>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={DatabaseService.DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplnsxBid>()
            .HasIndex(a => new { a.IPO_ID, a.BankCode });
        modelBuilder.Entity<ApplnsxBid>()
            .HasIndex(a => a.PAN);
        modelBuilder.Entity<ApplnsxBid>()
            .HasIndex(a => a.CLID);
    }
}
