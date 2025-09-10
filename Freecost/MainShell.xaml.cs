using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;

namespace Freecost
{
    public partial class MainShell : Shell
    {
        public bool IsAdmin => SessionService.IsAdmin;
        public string StatusText => SessionService.StatusText;
        public bool IsLoggedIn => SessionService.IsLoggedIn;
        public bool IsNotLoggedIn => !SessionService.IsLoggedIn;
        public bool CanChangeLocation => SessionService.IsLoggedIn && (SessionService.PermittedRestaurants?.Count > 1);
        public string? CurrentLocationName => SessionService.CurrentRestaurant?.Name;

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

#if ANDROID || IOS
            this.Behaviors.Add(new StatusBarBehavior
            {
                StatusBarColor = Color.FromArgb("#4A4A4A"), // This is the 'Secondary' color
                StatusBarStyle = StatusBarStyle.LightContent
            });
#endif

            GoToSettingsCommand = new Command(async () => await OnSettingsClicked());
            ChangeLocationCommand = new Command(OnChangeLocationClicked);
            ExitCommand = new Command(OnExitClicked);
            LoginCommand = new Command(async () => await OnLoginClicked());
            LogoutCommand = new Command(async () => await OnLogoutClicked());
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
                await UsagePopupService.CheckAndShowPopupAsync();
            }
        }

        private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsNotLoggedIn));
            OnPropertyChanged(nameof(CanChangeLocation));
            OnPropertyChanged(nameof(CurrentLocationName));
        }

        private async Task GoToAdminPage()
        {
            await Shell.Current.GoToAsync(nameof(AdminPage));
            Shell.Current.FlyoutIsPresented = false;
        }

        private async Task OnSettingsClicked()
        {
            await Shell.Current.GoToAsync("//SettingsPage");
            Shell.Current.FlyoutIsPresented = false;
        }

        private void OnChangeLocationClicked()
        {
            if (Application.Current != null)
                Application.Current.MainPage = new LocationSelectionPage();
        }

        private async Task OnLoginClicked()
        {
            if (Application.Current != null)
            {
                await SessionService.Clear();
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            }
        }

        private async Task OnLogoutClicked()
        {
            if (Application.Current != null)
            {
                await SessionService.Clear();
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            }
        }

        private void OnExitClicked()
        {
            Application.Current?.Quit();
        }
    }
}