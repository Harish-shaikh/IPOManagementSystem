using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class DataTransferPage : ContentPage
{
    public DataTransferPage(DataTransferViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
