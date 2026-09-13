using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class CompanyAccountDetailPage : ContentPage
{
    public CompanyAccountDetailPage(CompanyAccountDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
