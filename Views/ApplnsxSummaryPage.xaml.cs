using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class ApplnsxSummaryPage : ContentPage
{
    public ApplnsxSummaryPage(ApplnsxSummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
