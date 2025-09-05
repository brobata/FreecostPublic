namespace Freecost;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Initialize Firestore early
        _ = FirestoreService.InitializeAsync();

        // Set the main page to the Shell, which defaults to RecipesPage
        MainPage = new MainShell();
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Run startup logic on the main thread
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await FirestoreService.InitializeAsync();

            var savedToken = Preferences.Get("AuthToken", string.Empty);
            if (!string.IsNullOrEmpty(savedToken))
            {
                // A user session was saved - start in ONLINE mode
                SessionService.RestoreSession();
                if (SessionService.PermittedRestaurants != null && SessionService.PermittedRestaurants.Count > 1)
                {
                    // A multi-location user needs to choose a location first
                    MainPage = new LocationSelectionPage();
                }
                else
                {
                    // A single-location user can go straight to the app
                    MainPage = new MainShell();
                    await UsagePopupService.CheckAndShowPopupAsync();
                }
            }
            else
            {
                // No saved session - check if we can start in OFFLINE mode
                var offlineRestaurants = await LocalStorageService.LoadAsync<Restaurant>();
                if (offlineRestaurants.Any())
                {
                    SessionService.StartOfflineSession();
                    SessionService.PermittedRestaurants = offlineRestaurants;

                    if (offlineRestaurants.Count > 1)
                    {
                        // A multi-location offline user needs to choose a location
                        MainPage = new LocationSelectionPage();
                    }
                    else
                    {
                        // A single-location offline user can go straight to the app
                        SessionService.CurrentRestaurant = offlineRestaurants.First();
                        MainPage = new MainShell();
                        await UsagePopupService.CheckAndShowPopupAsync();
                    }
                }
                else
                {
                    // No saved session and no local data - must show the LoginPage
                    MainPage = new NavigationPage(new LoginPage());
                }
            }
        });
    }
}
