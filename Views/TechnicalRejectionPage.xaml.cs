using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class TechnicalRejectionPage : ContentPage
{
    public TechnicalRejectionPage(TechnicalRejectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
