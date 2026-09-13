using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class CompanyMasterPage : ContentPage
{
    public CompanyMasterPage(CompanyMasterViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
