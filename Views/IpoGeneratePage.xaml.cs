using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class IpoGeneratePage : ContentPage
{
    public IpoGeneratePage(IpoGenerateViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
