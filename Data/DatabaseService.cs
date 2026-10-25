using IPOProcessingSystem.Models;
using SQLite;

namespace IPOProcessingSystem.Data;

/// <summary>
/// Connection factory + schema initializer for the local/embedded SQLite database.
/// Uses sqlite-net-pcl for fast synchronous/async CRUD and batch transactions,
/// per the spec's requirement for RunInTransactionAsync-based bulk bid inserts.
/// </summary>
public class DatabaseService
{
    private readonly SQLiteAsyncConnection _db;
    public static string DbPath
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPOProcessingSystem");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "ipo_processing.db3");
        }
    }

    public DatabaseService()
    {
        SQLitePCL.Batteries_V2.Init();

        // Use synchronous connection during initialization to avoid UI thread deadlocks.
        // FullMutex is safer than SharedCache for unpackaged WinUI desktop apps.
        var flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex;
        using (var syncDb = new SQLiteConnection(DbPath, flags))
        {
            syncDb.CreateTable<User>();
            syncDb.CreateTable<CompanyMaster>();
            syncDb.CreateTable<ApplnsxBid>();
            syncDb.CreateTable<BankMaster>();
            syncDb.CreateTable<DummyIfscMapping>();
            syncDb.CreateTable<ReconciliationSummary>();
            syncDb.CreateTable<UnmatchRecord>();
            syncDb.CreateTable<ReconSession>();
            syncDb.CreateTable<TechnicalRejection>();
            syncDb.CreateTable<RejectionChecklistItem>();
            syncDb.CreateTable<RejectionRecord>();
            syncDb.CreateTable<BasisOfAllotment>();
            syncDb.CreateTable<BasisCategoryParameter>();
            syncDb.CreateTable<LuckyNumberAllocation>();
            syncDb.CreateTable<ListingData>();
            syncDb.CreateTable<OfferForSaleRecord>();
            syncDb.CreateTable<CompanyAccountDetail>();
            syncDb.CreateTable<PhyAllotmentIntimation>();
            // Extended models
            syncDb.CreateTable<BendemFile>();
            syncDb.CreateTable<ColumnMapping>();
            syncDb.CreateTable<DataTransferLog>();
            syncDb.CreateTable<Allocation>();
            syncDb.CreateTable<ClientMaster>();
            syncDb.CreateTable<BidUploadLog>();
            syncDb.CreateTable<FcEmailStatus>();
            syncDb.CreateTable<SebiDebarredPan>();

            SeedDefaultUser(syncDb);
            SeedDefaultRejectionChecklist(syncDb);
            SeedDefaultBanks(syncDb);
            SeedDefaultClients(syncDb);
        }

        _db = new SQLiteAsyncConnection(DbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    public SQLiteAsyncConnection Connection => _db;

    private void SeedDefaultUser(SQLiteConnection db)
    {
        var existing = db.Table<User>().FirstOrDefault();
        if (existing == null)
        {
            // Default login per manual: USER ID 50216 / PASSWORD 1
            // NOTE: replace with a real hash function (e.g. BCrypt) before production use.
            db.Insert(new User
            {
                Username = "50216",
                PasswordHash = "1",
                Role = "Admin"
            });
        }
    }

    private void SeedDefaultRejectionChecklist(SQLiteConnection db)
    {
        var existing = db.Table<RejectionChecklistItem>().FirstOrDefault();
        if (existing != null) return;

        var defaults = new[]
        {
            ("!", "Applied by Partner/Employee not eligible"),
            ("#", "More than 5 applications from same PAN"),
            ("$", "Applied from unauthorised source"),
            ("A", "Incorrect Amount"),
            ("B", "Withdrawn Application"),
            ("C", "Multiple Applications - PAN"),
            ("D", "NRI with US address"),
            ("E", "Individual Applied in Employee/Reserved category incorrectly"),
            ("F", "SEBI Debarred Applicant"),
            ("G", "Multiple Applications - Name"),
            ("H", "HNI applied at Cut-off price"),
            ("I", "Application Bid < Minimum Lot"),
            ("J", "HNI not applied at cut-off"),
            ("K", "HNI Bid after 3 pm on closing day"),
            ("L", "Not in Multiple of Lot Size"),
            ("M", "Multiple with same DP/Client ID"),
            ("N", "No Signature of Applicant"),
            ("O", "Promoter Group PAN"),
        };
        foreach (var (code, text) in defaults)
        {
            db.Insert(new RejectionChecklistItem { MnemonicVar = code, ReasonText = text, Include = true });
        }
    }

    private void SeedDefaultBanks(SQLiteConnection db)
    {
        if (db.Table<BankMaster>().Count() == 0)
        {
            var banks = new[]
            {
                new BankMaster { BankCode = "AXIS", BankName = "Axis Bank Ltd", IFSC_Code = "UTIB0000001" },
                new BankMaster { BankCode = "HDFC", BankName = "HDFC Bank Ltd", IFSC_Code = "HDFC0000001" },
                new BankMaster { BankCode = "ICICI", BankName = "ICICI Bank Ltd", IFSC_Code = "ICIC0000001" },
                new BankMaster { BankCode = "SBI", BankName = "State Bank of India", IFSC_Code = "SBIN0000001" },
                new BankMaster { BankCode = "CANARA", BankName = "Canara Bank", IFSC_Code = "CNRB0000001" },
                new BankMaster { BankCode = "YES", BankName = "Yes Bank Ltd", IFSC_Code = "YESB0000001" },
                new BankMaster { BankCode = "KOTAK", BankName = "Kotak Mahindra Bank", IFSC_Code = "KKBK0000001" },
                new BankMaster { BankCode = "PNB", BankName = "Punjab National Bank", IFSC_Code = "PUNB0000001" }
            };
            foreach (var b in banks) db.Insert(b);
        }
    }

    private void SeedDefaultClients(SQLiteConnection db)
    {
        if (db.Table<ClientMaster>().Count() == 0)
        {
            var clients = new[]
            {
                new ClientMaster { RtaBranchId = 101, ClientId = 501, ClientName = "Reliance Industries Ltd" },
                new ClientMaster { RtaBranchId = 101, ClientId = 502, ClientName = "Tata Technologies Ltd" },
                new ClientMaster { RtaBranchId = 102, ClientId = 503, ClientName = "Zenith Infotech SME" },
                new ClientMaster { RtaBranchId = 102, ClientId = 504, ClientName = "Apex Solar Energy Ltd" },
                new ClientMaster { RtaBranchId = 103, ClientId = 505, ClientName = "Nova Pharma Chem Ltd" }
            };
            foreach (var c in clients) db.Insert(c);
        }
    }
}
