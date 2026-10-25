# IPO Processing & Registry System (.NET MAUI + SQLite)

A specialized Windows desktop application built with **.NET MAUI (C# .NET 8)** and **SQLite**, engineered to handle Indian Registrar and Transfer Agent (RTA) IPO processing workflows for BSE, NSE, and ASBA bidding.

---

## Table of Contents
- [Overview & Architecture](#overview--architecture)
- [Prerequisites & System Requirements](#prerequisites--system-requirements)
- [Getting Started & Build Steps](#getting-started--build-steps)
- [Default Login Credentials](#default-login-credentials)
- [End-to-End IPO Processing Steps](#end-to-end-ipo-processing-steps)
- [Project Structure](#project-structure)
- [Git Workflow for Developers](#git-workflow-for-developers)
- [Extension Points & Customization](#extension-points--customization)

---

## Overview & Architecture

The application implements a Clean Architecture / MVVM structure using `CommunityToolkit.Mvvm`:
- **UI Layer**: .NET MAUI XAML pages and controls tailored for Windows Desktop (`net8.0-windows10.0.19041.0`).
- **ViewModel Layer**: Observable objects and asynchronous relay commands handling presentation state.
- **Service Layer**: Business logic for Bid ingestion, Bank Reconciliation, Basis of Allotment, Rejection Engine, and ClosedXML Excel reporting.
- **Data Layer**: Dual SQLite approach — `sqlite-net-pcl` for ultra-fast bulk inserts and EF Core SQLite (`AppDbContext`) for LINQ-based complex queries.

---

## Prerequisites & System Requirements

Before running the project, make sure the following tools are installed:
1. **Operating System**: Windows 10 (version 1809 or higher) or Windows 11.
2. **.NET 8 SDK**: Download and install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
3. **.NET MAUI Workload**: Installed via the .NET CLI or Visual Studio 2022.
4. **IDE**: Visual Studio 2022 (v17.8+ with *.NET Multi-platform App UI development* workload) or VS Code with C# Dev Kit.

---

## Getting Started & Build Steps

Follow these steps to set up and run the application from scratch:

### Step 1: Install MAUI Workload (First-Time Only)
Open PowerShell as Administrator or regular terminal and run:
```powershell
dotnet workload install maui
```

### Step 2: Clone or Open the Repository
Navigate to the repository folder:
```powershell
cd d:\DOTNET\IPOApplication\IPOPS\IPOProcessingSystem\IPOProcessingSystem
```

Ensure you are on the `dev` branch for new development:
```powershell
git checkout dev
```

### Step 3: Restore Dependencies
Restore all required NuGet packages (ClosedXML, CsvHelper, CommunityToolkit.Mvvm, SQLite):
```powershell
dotnet restore
```

### Step 4: Build and Run the Application
Run directly on Windows Desktop via CLI:
```powershell
dotnet build -t:Run -f net8.0-windows10.0.19041.0
```

Alternatively, open `IPOProcessingSystem.sln` in **Visual Studio 2022**, select **Windows Machine** as the run target, and press `F5`.

---

## Default Login Credentials

On first launch, the local SQLite database automatically seeds the initial operator user per RTA standards:

| Field | Default Value | Notes |
|---|---|---|
| **User ID** | `50216` | Default Operator ID |
| **Password** | `1` | Plaintext default (seed) |

> [!IMPORTANT]
> Change the default credentials for production use in `Data/DatabaseService.cs` (`SeedDefaultUserAsync`) and replace the plaintext check in `Services/AuthService.cs` with secure hashing (e.g., BCrypt).

---

## End-to-End IPO Processing Steps

The system guides the operator through the chronological stages of an IPO issue:

```mermaid
flowchart LR
    A[1. Login] --> B[2. IPO Generate]
    B --> C[3. Company Master]
    C --> D[4. Bid Upload]
    D --> E[5. Bank Reconciliation]
    E --> F[6. Technical Rejections]
    F --> G[7. Basis of Allotment]
    G --> H[8. Post-IPO Reports]
```

### Step 1: Login
- Launch the application and enter User ID `50216` and Password `1`.
- Authentication authorizes access to the Main Shell navigation.

### Step 2: IPO Generate (Issue Setup)
- Navigate to **IPO Generate**.
- Enter the IPO Issue Code, Company Name, Issue Dates, and create the designated directory structure and database file for the new IPO issue.

### Step 3: Company Master Setup
- Navigate to **Company Master**.
- Fill in:
  - Issue price / price bands (Floor and Cap).
  - Category quotas (Retail 35%, QIB 50%, NII/HNI 15%, Employee quota).
  - Minimum lot size and tick size.

### Step 4: Bid File Upload
- Navigate to **Bid Upload**.
- Select exchange files:
  - **BSE format**: `.dat` fixed-width file (e.g., sample file `BSE_BID_SAMPLE_20.dat`).
  - **NSE format**: `.csv` comma-separated file.
- The bulk batch processor inserts bids in transactional chunks of 5,000 records into `Applnsx`.

### Step 5: Day-Wise & Closing Day Processing
- Monitor daily cumulative bid counts and subscription metrics.
- Execute closing-day cutoffs to freeze modification records.

### Step 6: Bank Schedule Reconciliation
- Navigate to **Reconciliation**.
- Load bank ASBA schedule statements (Excel/CSV).
- The system matches application number, bank code, and amount against exchange bids.
- Identify unmatch cases, mark NIL bank records, and correct bank IFSC codes.

### Step 7: Technical Rejections Engine
- Run rule-based validation on bids:
  - Duplicate PAN applications within the same category.
  - Multi-application check (multiple bids under one PAN).
  - Debarred PAN list (SEBI ban list comparison).
  - Out-of-bounds cut-off price or invalid bid amounts for retail.
- Mark valid vs. rejected applications with audit trail reason codes.

### Step 8: Basis of Allotment & Lucky Draw
- Navigate to **Basis of Allotment**.
- Calculate oversubscription ratios category-wise.
- Determine proportional allotment ratios and generate lucky-draw sequence numbers for retail lots.

### Step 9: Post-IPO Reports & Exports
- Generate standard regulatory and operational reports via **ClosedXML**:
  - Bank Wise Summary & Commission Reports.
  - Refund Summary and Detail schedules.
  - Allottee list for depositories (CDSL & NSDL).
  - ROC / Stock Exchange Listing Data.

---

## Project Structure

```
IPOProcessingSystem/
├── Data/                 # DatabaseService (sqlite-net-pcl) & AppDbContext (EF Core)
├── Helpers/              # UI Converters, Formatting utilities
├── Models/               # SQLite entity definitions matching RTA specs:
│   ├── User.cs
│   ├── CompanyMaster.cs
│   ├── Applnsx.cs (Bid records)
│   ├── BankMaster.cs
│   ├── ReconciliationSummary.cs
│   ├── UnmatchRecord.cs
│   ├── TechnicalRejection.cs
│   └── BasisOfAllotment.cs
├── Platforms/            # Platform-specific initialization (Windows / WinUI 3)
├── Resources/            # App icons, splash screens, styles, and fonts
├── Services/             # Business & domain services:
│   ├── AuthService.cs
│   ├── BidImportService.cs
│   ├── ReconciliationService.cs
│   ├── BasisCalculationService.cs
│   ├── RejectionEngineService.cs
│   └── ReportService.cs
├── ViewModels/           # CommunityToolkit.Mvvm ViewModels
├── Views/                # XAML Pages bound to ViewModels
├── App.xaml / AppShell   # Navigation Shell and UI Routing
└── MauiProgram.cs        # Dependency Injection & Bootstrapper
```

---

## Git Workflow for Developers

### Working on the `dev` Branch
Always pull latest changes and create feature branches or work on `dev`:

```powershell
# Check current status
git status

# Add changed or new files
git add .

# Commit changes
git commit -m "docs: add comprehensive README and workflow guide"

# Push branch to remote
git push -u origin dev
```

---

## Extension Points & Customization

The core business logic and schema are fully implemented. The following integration hooks are clearly marked with `// TODO` in the codebase for environment-specific customization:
1. **Bank Statement Import** (`ReconciliationService.cs`): Customize Excel columns based on individual sponsor bank formats.
2. **BenDem Generation** (`BendemService.cs`): Connect to depository endpoints for CDSL (`.001` sequence) and NSDL (`.OUT` files).
3. **SEBI Debarred PAN Ingestion** (`RejectionEngineService.cs`): Wire automated download or scheduled SFTP pull for updated regulatory lists.
4. **Email / SFTP Dispatch** (`EmailService.cs`): Configure your organization's SMTP relay and secure file transfer servers.
