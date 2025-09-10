namespace Freecost;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
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

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await FirestoreService.InitializeAsync();
                var savedRefreshToken = Preferences.Get("RefreshToken", string.Empty);

                if (string.IsNullOrEmpty(savedRefreshToken))
                {
                    // Not logged in, start in offline mode.
                    SessionService.InitializeAsOffline();
                    MainPage = new MainShell();
                }
                else
                {
                    // Was logged in, try to restore and refresh session.
                    SessionService.RestoreSession();
                    bool refreshedSuccessfully = await AuthService.RefreshAuthTokenIfNeededAsync();

                    if (!refreshedSuccessfully)
                    {
                        // Refresh failed. SessionService now handles this by switching to offline mode.
                        MainPage = new MainShell();
                        await MainPage.DisplayAlert("Session Expired", "Your session has expired. Please log in again to sync your data.", "OK");
                    }
                    else
                    {
                        // Successfully refreshed. We are online.
                        // Restore the last used online location.
                        var lastRestaurantId = SessionService.LastOnlineRestaurantId;
                        SessionService.CurrentRestaurant = SessionService.PermittedRestaurants?.FirstOrDefault(r => r.Id == lastRestaurantId) ?? SessionService.PermittedRestaurants?.FirstOrDefault();

                        if (SessionService.CurrentRestaurant == null)
                        {
                            // User is logged in but has no valid location selected.
                            if (SessionService.PermittedRestaurants != null && SessionService.PermittedRestaurants.Count > 1)
                            {
                                MainPage = new LocationSelectionPage();
                            }
                            else if (SessionService.PermittedRestaurants != null && SessionService.PermittedRestaurants.Count == 1)
                            {
                                SessionService.CurrentRestaurant = SessionService.PermittedRestaurants.First();
                                MainPage = new MainShell();
                            }
                            else
                            {
                                // Logged in but not assigned to any locations. Log them out.
                                await SessionService.HandleSessionExpirationAsync();
                                MainPage = new MainShell();
                                await MainPage.DisplayAlert("No Locations", "Your account is not assigned to any locations. Please contact support.", "OK");
                            }
                        }
                        else
                        {
                            MainPage = new MainShell();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Likely a network error on startup. Default to offline mode.
                SessionService.InitializeAsOffline();
                MainPage = new MainShell();
                await MainPage.DisplayAlert("Startup Error", "Could not connect to online services. Starting in offline mode.", "OK");
            }
        });
    }
}

