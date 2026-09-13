using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class BasisAllotmentPage : ContentPage
{
    public BasisAllotmentPage(BasisAllotmentViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
