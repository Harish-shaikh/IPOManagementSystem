using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class ListingReportPage : ContentPage
{
    public ListingReportPage(ListingReportViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
