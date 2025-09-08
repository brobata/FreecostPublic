namespace Freecost;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Set up the global exception handler
        AppDomain.CurrentDomain.UnhandledException += GlobalExceptionHandler.HandleException;

        // Set the main page to the Shell
        MainPage = new MainShell();
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Run startup logic on the main thread
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await UnitConverter.InitializeAsync();

            var savedRefreshToken = Preferences.Get("RefreshToken", string.Empty);
            if (!string.IsNullOrEmpty(savedRefreshToken))
            {
                // A session was saved - attempt to refresh the token
                SessionService.RestoreSession();
                bool refreshedSuccessfully = await AuthService.RefreshAuthTokenIfNeededAsync();

                if (!refreshedSuccessfully)
                {
                    // AuthService handles navigation to LoginPage
                    return;
                }

                if (SessionService.CurrentRestaurant != null)
                {
                    MainPage = new MainShell();
                }
                else if (SessionService.PermittedRestaurants != null && SessionService.PermittedRestaurants.Count > 1)
                {
                    MainPage = new LocationSelectionPage();
                }
                else
                {
                    MainPage = new MainShell();
                }
            }
            else
            {
                // No saved session - check for offline data
                var offlineRestaurants = await LocalStorageService.LoadAsync<Restaurant>();
                if (offlineRestaurants.Any())
                {
                    SessionService.StartOfflineSession();
                    SessionService.PermittedRestaurants = offlineRestaurants;

                    if (offlineRestaurants.Count > 1)
                    {
                        MainPage = new LocationSelectionPage();
                    }
                    else
                    {
                        SessionService.CurrentRestaurant = offlineRestaurants.First();
                        MainPage = new MainShell();
                    }
                }
                else
                {
                    // No session and no offline data, force login.
                    MainPage = new NavigationPage(new LoginPage());
                }
            }
            await UsagePopupService.CheckAndShowPopupAsync();
        });
    }
}
