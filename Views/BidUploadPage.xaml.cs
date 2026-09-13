using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class BidUploadPage : ContentPage
{
    public BidUploadPage(BidUploadViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
