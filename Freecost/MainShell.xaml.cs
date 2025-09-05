using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace Freecost
{
    public partial class MainShell : Shell
    {
        public bool IsAdmin => SessionService.IsAdmin;
        public string StatusText => SessionService.StatusText;
        public bool IsLoggedIn => SessionService.IsLoggedIn;
        public bool IsNotLoggedIn => !SessionService.IsLoggedIn;
        public bool CanChangeLocation => SessionService.IsLoggedIn && (SessionService.PermittedRestaurants?.Count > 1);

        public ICommand GoToSettingsCommand { get; }
        public ICommand ChangeLocationCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand GoToAdminCommand { get; }

        private bool _isFirstAppearance = true;

        public MainShell()
        {
            InitializeComponent();

            GoToSettingsCommand = new Command(async () => await OnSettingsClicked());
            ChangeLocationCommand = new Command(OnChangeLocationClicked);
            ExitCommand = new Command(OnExitClicked);
            LoginCommand = new Command(OnLoginClicked);
            LogoutCommand = new Command(OnLogoutClicked);
            GoToAdminCommand = new Command(async () => await GoToAdminPage());

            BindingContext = this;
            SessionService.StaticPropertyChanged += OnSessionChanged;
            Routing.RegisterRoute(nameof(AdminPage), typeof(AdminPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(LocationSelectionPage), typeof(LocationSelectionPage));
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_isFirstAppearance)
            {
                _isFirstAppearance = false;
                await CheckSessionAndNavigate();
                await UsagePopupService.CheckAndShowPopupAsync();
            }
        }

        private async Task CheckSessionAndNavigate()
        {
            var savedToken = Preferences.Get("AuthToken", string.Empty);
            if (!string.IsNullOrEmpty(savedToken))
            {
                SessionService.RestoreSession();
                if (SessionService.PermittedRestaurants != null && SessionService.PermittedRestaurants.Any())
                {
                    var defaultRestaurant = SessionService.PermittedRestaurants.FirstOrDefault(r => r.Id == SessionService.DefaultRestaurantId);
                    SessionService.CurrentRestaurant = defaultRestaurant ?? SessionService.PermittedRestaurants.FirstOrDefault();
                }
            }
            else
            {
                var offlineRestaurants = await LocalStorageService.LoadAsync<Restaurant>();
                if (offlineRestaurants.Any())
                {
                    SessionService.StartOfflineSession();
                    SessionService.PermittedRestaurants = offlineRestaurants;
                    SessionService.CurrentRestaurant = offlineRestaurants.FirstOrDefault();
                }
            }
        }

        private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsNotLoggedIn));
            OnPropertyChanged(nameof(CanChangeLocation));
        }

        private async Task GoToAdminPage()
        {
            // The Admin page is not a tab, so it must be navigated to as a route.
            await Shell.Current.GoToAsync(nameof(AdminPage));
            Shell.Current.FlyoutIsPresented = false;
        }

        private async Task OnSettingsClicked()
        {
            // The Settings page is a tab, so we can navigate to it directly.
            await Shell.Current.GoToAsync("//SettingsPage");
            Shell.Current.FlyoutIsPresented = false;
        }



        private void OnChangeLocationClicked()
        {
            if (Application.Current != null)
                Application.Current.MainPage = new LocationSelectionPage();
        }

        private void OnLoginClicked()
        {
            if (Application.Current != null)
            {
                SessionService.Clear();
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            }
        }

        private void OnLogoutClicked()
        {
            if (Application.Current != null)
            {
                SessionService.Clear();
                Application.Current.MainPage = new MainShell();
            }
        }

        private void OnExitClicked()
        {
            Application.Current?.Quit();
        }
    }
}