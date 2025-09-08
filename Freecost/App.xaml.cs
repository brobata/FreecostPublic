namespace Freecost;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();


        // Set the main page to the Shell
        MainPage = new MainShell();
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
                if (!string.IsNullOrEmpty(savedRefreshToken))
                {
                    SessionService.RestoreSession();
                    bool refreshedSuccessfully = await AuthService.RefreshAuthTokenIfNeededAsync();
                    if (!refreshedSuccessfully)
                    {
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
                        MainPage = new NavigationPage(new LoginPage());
                    }
                }
            }
            catch (Exception)
            {
                // If any network call fails, simply go to offline mode.
                // The UI will reflect this status text automatically.
                SessionService.StartOfflineSession();
                var offlineRestaurants = await LocalStorageService.LoadAsync<Restaurant>();
                if (offlineRestaurants.Any())
                {
                    SessionService.PermittedRestaurants = offlineRestaurants;
                }
                MainPage = new MainShell();
            }
        });
    }
}