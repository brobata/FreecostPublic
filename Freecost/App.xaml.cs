namespace Freecost;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Set a temporary loading page to prevent the app from crashing on startup
        MainPage = new LoadingPage();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        return window;
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // All startup logic is now safely handled here
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await FirestoreService.InitializeAsync();

                var savedRefreshToken = Preferences.Get("RefreshToken", string.Empty);
                if (!string.IsNullOrEmpty(savedRefreshToken))
                {
                    SessionService.RestoreSession();
                    bool refreshedSuccessfully = await AuthService.RefreshAuthTokenIfNeededAsync();
                    if (!refreshedSuccessfully)
                    {
                        MainPage = new NavigationPage(new LoginPage());
                        return;
                    }

                    if (SessionService.CurrentRestaurant == null && SessionService.PermittedRestaurants != null && SessionService.PermittedRestaurants.Count > 1)
                    {
                        MainPage = new LocationSelectionPage();
                    }
                    else if (SessionService.CurrentRestaurant != null)
                    {
                        MainPage = new MainShell();
                    }
                    else
                    {
                        await SessionService.Clear();
                        MainPage = new NavigationPage(new LoginPage());
                        await MainPage.DisplayAlert("No Locations", "Your account is not assigned to any locations. Please contact support.", "OK");
                    }
                }
                else
                {
                    MainPage = new NavigationPage(new LoginPage());
                }
            }
            catch (Exception)
            {
                MainPage = new NavigationPage(new LoginPage());
                await MainPage.DisplayAlert("Startup Error", "Could not connect to online services. You can work offline if you have local data.", "OK");
            }
        });
    }
}