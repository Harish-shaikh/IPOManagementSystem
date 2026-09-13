using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class AllotmentCountPage : ContentPage
{
    public AllotmentCountPage(AllotmentCountViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
