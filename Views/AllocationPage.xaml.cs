using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class AllocationPage : ContentPage
{
    public AllocationPage(AllocationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
