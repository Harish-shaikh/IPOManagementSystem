using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class ClosingNextDayPage : ContentPage
{
    public ClosingNextDayPage(ClosingNextDayViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
