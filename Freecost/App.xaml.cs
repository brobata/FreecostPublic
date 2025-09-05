namespace Freecost;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Initialize Firestore early
        _ = FirestoreService.InitializeAsync();

        // Set the main page to the Shell
        MainPage = new MainShell();
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Run startup logic on the main thread
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await FirestoreService.InitializeAsync();

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

                // *** MODIFIED LOGIC STARTS HERE ***

                // Check if a location was already saved from a previous session
                if (SessionService.CurrentRestaurant != null)
                {
                    // A location is already selected, go directly to the app.
                    MainPage = new MainShell();
                    await UsagePopupService.CheckAndShowPopupAsync();
                }
                // If no location is saved and the user has access to multiple, show the selection page.
                else if (SessionService.PermittedRestaurants != null && SessionService.PermittedRestaurants.Count > 1)
                {
                    MainPage = new LocationSelectionPage();
                }
                else
                {
                    // User has access to 0 or 1 locations, so no selection is needed.
                    MainPage = new MainShell();
                    await UsagePopupService.CheckAndShowPopupAsync();
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
                        await UsagePopupService.CheckAndShowPopupAsync();
                    }
                }
                else
                {
                    // No session and no offline data, force login.
                    MainPage = new NavigationPage(new LoginPage());
                }
            }
        });
    }
}