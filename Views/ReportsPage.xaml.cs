using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class ReportsPage : ContentPage
{
    public ReportsPage(ReportsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
