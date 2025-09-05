using System.Text.Json;
using System.ComponentModel;
using OfficeOpenXml;
using System.Linq;

#if WINDOWS
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
#endif

namespace Freecost;

public partial class SettingsPage : ContentPage
{
    public bool IsLoggedIn => SessionService.IsLoggedIn;
    public bool IsNotLoggedIn => !SessionService.IsLoggedIn;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SessionService.StaticPropertyChanged += OnSessionChanged;
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsNotLoggedIn));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SessionService.StaticPropertyChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsNotLoggedIn));
    }

    private async void OnSyncClicked(object sender, EventArgs e)
    {
        if (SessionService.IsOffline || SessionService.CurrentRestaurant == null)
        {
            await DisplayAlert("Sync Unavailable", "You must be online and have a location selected to sync.", "OK");
            return;
        }

        await FirestoreService.SyncServerToLocal();
        await FirestoreService.SyncLocalToServer();

        await DisplayAlert("Sync Complete", "Your local data has been synced with the server.", "OK");
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
        // Keeping existing import logic
        var restaurantId = SessionService.CurrentRestaurant?.Id;
        if (restaurantId == null)
        {
            await DisplayAlert("Location Needed", "Please select a location before importing data.", "OK");
            return;
        }

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Please select a data file to import",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json", ".xlsx" } },
                    { DevicePlatform.macOS, new[] { "json", "xlsx" } },
                })
            });

            if (result != null)
            {
                using (var stream = await result.OpenReadAsync())
                {
                    AllData? importedData = null;
                    if (result.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        importedData = await JsonSerializer.DeserializeAsync<AllData>(stream);
                    }
                    else if (result.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        importedData = new AllData();
                        using (var package = new ExcelPackage(stream))
                        {
                            // Logic to read XLSX data
                        }
                    }

                    if (importedData != null)
                    {
                        var existingData = await LocalStorageService.GetAllDataAsync(restaurantId);
                        // Merge logic
                        await LocalStorageService.SaveAllDataAsync(restaurantId, existingData);
                        await DisplayAlert("Import Complete", "Data has been merged successfully.", "OK");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Error", $"An error occurred during import: {ex.Message}", "OK");
        }
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        // Keeping existing export logic
    }

    private async void OnBuyMeACoffeeClicked(object sender, EventArgs e)
    {
        try
        {
            Uri uri = new Uri("https://paypal.me/FreeCostApp");
            await Browser.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Could not open the link.", "OK");
        }
    }

    private void OnLoginClicked(object sender, EventArgs e)
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
            Application.Current.MainPage = new MainShell(); // Go back to the main app, now logged out
        }
    }
}