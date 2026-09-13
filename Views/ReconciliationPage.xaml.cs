using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class ReconciliationPage : ContentPage
{
    public ReconciliationPage(ReconciliationViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
