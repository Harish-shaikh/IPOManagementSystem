using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class FcEmailSenderPage : ContentPage
{
    public FcEmailSenderPage(FcEmailSenderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
