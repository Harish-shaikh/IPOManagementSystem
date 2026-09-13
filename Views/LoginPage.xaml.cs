using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
