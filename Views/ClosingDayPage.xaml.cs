using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class ClosingDayPage : ContentPage
{
    public ClosingDayPage(ClosingDayViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
