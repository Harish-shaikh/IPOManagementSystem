using CommunityToolkit.Mvvm.Messaging;
using IPOProcessingSystem.Data;
using IPOProcessingSystem.Services;
using IPOProcessingSystem.Services.Interfaces;
using IPOProcessingSystem.ViewModels;
using IPOProcessingSystem.Views;
using Microsoft.Extensions.Logging;

namespace IPOProcessingSystem;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // -------- Shell --------
        builder.Services.AddSingleton<AppShell>();

        // -------- Data layer --------
        builder.Services.AddSingleton<DatabaseService>();

        // -------- Shared app state --------
        builder.Services.AddSingleton<AppState>();

        // -------- Core business services --------
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IAlertService, AlertService>();
        builder.Services.AddSingleton<IBidImportService, BidImportService>();
        builder.Services.AddSingleton<IReconciliationService, ReconciliationService>();
        builder.Services.AddSingleton<IBasisCalculationService, BasisCalculationService>();
        builder.Services.AddSingleton<IRejectionEngineService, RejectionEngineService>();
        builder.Services.AddSingleton<IReportService, ReportService>();
        builder.Services.AddSingleton<IBendemService, BendemService>();
        builder.Services.AddSingleton<IAllocationService, AllocationService>();
        builder.Services.AddSingleton<IEmailService, EmailService>();
        builder.Services.AddSingleton<INetworkPathService, NetworkPathService>();

        // -------- ViewModels --------
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<IpoGenerateViewModel>();
        builder.Services.AddTransient<CompanyMasterViewModel>();
        builder.Services.AddTransient<BidUploadViewModel>();
        builder.Services.AddTransient<DayWiseViewModel>();
        builder.Services.AddTransient<ClosingNextDayViewModel>();
        builder.Services.AddTransient<ClosingDayViewModel>();
        builder.Services.AddTransient<ReconciliationViewModel>();
        builder.Services.AddTransient<TechnicalRejectionViewModel>();
        builder.Services.AddTransient<BasisAllotmentViewModel>();
        builder.Services.AddTransient<LuckyNoViewModel>();
        builder.Services.AddTransient<AllocationViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<ListingReportViewModel>();
        builder.Services.AddTransient<DataTransferViewModel>();
        builder.Services.AddTransient<CompanyAccountDetailViewModel>();
        builder.Services.AddTransient<FcEmailSenderViewModel>();
        builder.Services.AddTransient<AllotmentCountViewModel>();
        builder.Services.AddTransient<ApplnsxSummaryViewModel>();
        builder.Services.AddTransient<PhyAllotmentIntimationViewModel>();

        // -------- Views --------
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<IpoGeneratePage>();
        builder.Services.AddTransient<CompanyMasterPage>();
        builder.Services.AddTransient<BidUploadPage>();
        builder.Services.AddTransient<DayWisePage>();
        builder.Services.AddTransient<ClosingNextDayPage>();
        builder.Services.AddTransient<ClosingDayPage>();
        builder.Services.AddTransient<ReconciliationPage>();
        builder.Services.AddTransient<TechnicalRejectionPage>();
        builder.Services.AddTransient<BasisAllotmentPage>();
        builder.Services.AddTransient<LuckyNoPage>();
        builder.Services.AddTransient<AllocationPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<ListingReportPage>();
        builder.Services.AddTransient<DataTransferPage>();
        builder.Services.AddTransient<CompanyAccountDetailPage>();
        builder.Services.AddTransient<FcEmailSenderPage>();
        builder.Services.AddTransient<AllotmentCountPage>();
        builder.Services.AddTransient<ApplnsxSummaryPage>();
        builder.Services.AddTransient<PhyAllotmentIntimationPage>();
        builder.Services.AddTransient<FooterView>();

        return builder.Build();
    }
}

