using IPOProcessingSystem.ViewModels;

namespace IPOProcessingSystem.Views;

public partial class DayWisePage : ContentPage
{
    private readonly DayWiseViewModel _vm;
    public DayWisePage(DayWiseViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        // Sync IPO ID from AppState if set
    }
}
