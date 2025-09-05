using System.ComponentModel;
using System.Linq;

namespace Freecost;

public partial class MainShell : Shell
{
    public bool IsAdmin => SessionService.IsAdmin;
    public string StatusText => SessionService.StatusText;

    private readonly MenuItem _loginMenuItem;
    private readonly MenuItem _logoutMenuItem;
    private bool _isFirstAppearance = true;

    public MainShell()
    {
        InitializeComponent();
        BindingContext = this;

        _loginMenuItem = new MenuItem { Text = "Login" };
        _loginMenuItem.Clicked += OnLoginClicked;

        _logoutMenuItem = new MenuItem { Text = "Logout" };
        _logoutMenuItem.Clicked += OnLogoutClicked;

        SessionService.StaticPropertyChanged += OnSessionChanged;
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(LocationSelectionPage), typeof(LocationSelectionPage));

        UpdateMenuItems();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isFirstAppearance)
        {
            _isFirstAppearance = false;
            await UsagePopupService.CheckAndShowPopupAsync();
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(StatusText));
        UpdateMenuItems();
    }

    private void UpdateMenuItems()
    {
        if (Items.Contains(_loginMenuItem)) Items.Remove(_loginMenuItem);
        if (Items.Contains(_logoutMenuItem)) Items.Remove(_logoutMenuItem);

        if (SessionService.IsLoggedIn)
        {
            Items.Add(_logoutMenuItem);
        }
        else
        {
            Items.Add(_loginMenuItem);
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//SettingsPage");
        Shell.Current.FlyoutIsPresented = false;
    }

    private void OnChangeLocationClicked(object? sender, EventArgs e)
    {
        if (Application.Current != null)
        {
            SessionService.Clear();
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }

    private void OnLoginClicked(object? sender, EventArgs e)
    {
        if (Application.Current != null)
        {
            SessionService.Clear();
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }

    private void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (Application.Current != null)
        {
            SessionService.Clear();
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        Application.Current?.Quit();
    }
}
