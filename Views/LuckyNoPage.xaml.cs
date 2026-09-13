using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class LuckyNoPage : ContentPage
{
    public LuckyNoPage(LuckyNoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
