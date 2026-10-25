using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class PhyAllotmentIntimationPage : ContentPage
{
    public PhyAllotmentIntimationPage(PhyAllotmentIntimationViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
