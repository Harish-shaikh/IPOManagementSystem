using IPOProcessingSystem.Services;

namespace IPOProcessingSystem.Views;

public partial class FooterView : ContentView
{
    private AppState? _appState;

    /// <summary>
    /// Bindable property so the XAML Border can toggle IsVisible based on login state.
    /// True once AppState.LoggedInUser is set.
    /// </summary>
    public static readonly BindableProperty IsLoggedInProperty =
        BindableProperty.Create(nameof(IsLoggedIn), typeof(bool), typeof(FooterView), false);

    public bool IsLoggedIn
    {
        get => (bool)GetValue(IsLoggedInProperty);
        private set => SetValue(IsLoggedInProperty, value);
    }

    public FooterView()
    {
        InitializeComponent();
        BindingContext = this;   // so {Binding IsLoggedIn, Source={x:Reference FooterSelf}} works
        Loaded   += OnLoaded;
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
            _appState.PropertyChanged -= OnAppStatePropertyChanged;
    }

    private void OnAppStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(UpdateDisplay);
    }

    private void UpdateDisplay()
    {
        if (_appState == null) return;

        var loggedIn = _appState.IsLoggedIn;
        IsLoggedIn = loggedIn;

        if (!loggedIn)
        {
            IpoIdLabel.Text      = "---";
            ClientNameLabel.Text = "---";
            LoginByLabel.Text    = "---";
            VersionLabel.Text    = _appState.Version;
            return;
        }

        IpoIdLabel.Text      = _appState.ActiveIpoId > 0
                                   ? _appState.ActiveIpoId.ToString()
                                   : "(none)";
        ClientNameLabel.Text = !string.IsNullOrWhiteSpace(_appState.ClientName)
                                   ? _appState.ClientName
                                   : "(none)";
        LoginByLabel.Text    = _appState.LoggedInUser ?? string.Empty;
        VersionLabel.Text    = _appState.Version;
    }
}
