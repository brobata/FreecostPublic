namespace Freecost;

public partial class MainShell : Shell
{
    public bool IsAdmin => SessionService.IsAdmin;

    public MainShell()
    {
        InitializeComponent();
        BindingContext = this;
        SessionService.OnRoleChanged += OnRoleChanged;
        Routing.RegisterRoute(nameof(LocationSelectionPage), typeof(LocationSelectionPage));
    }

    private void OnRoleChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsAdmin));
    }

    private void OnChangeLocationClicked(object sender, EventArgs e)
    {
        if (Application.Current != null)
        {
            SessionService.Clear();
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }

    private void OnLogoutClicked(object sender, EventArgs e)
    {
        if (Application.Current != null)
        {
            SessionService.Clear();
            Application.Current.MainPage = new NavigationPage(new LoginPage());
        }
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        Application.Current?.Quit();
    }
}