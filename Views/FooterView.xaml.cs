using IPOProcessingSystem.Services;

namespace IPOProcessingSystem.Views;

public partial class FooterView : ContentView
{
    private AppState? _appState;

    public FooterView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _appState = Handler?.MauiContext?.Services.GetService<AppState>();
        if (_appState != null)
        {
            _appState.PropertyChanged += OnAppStatePropertyChanged;
            UpdateDisplay();
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_appState != null)
        {
            _appState.PropertyChanged -= OnAppStatePropertyChanged;
        }
    }

    private void OnAppStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(UpdateDisplay);
    }

    private void UpdateDisplay()
    {
        if (_appState == null) return;
        IpoIdLabel.Text = _appState.ActiveIpoId.ToString();
        ClientNameLabel.Text = _appState.ClientName ?? "None";
        LoginByLabel.Text = _appState.LoggedInUser ?? "Unknown";
        VersionLabel.Text = _appState.Version;
    }
}
