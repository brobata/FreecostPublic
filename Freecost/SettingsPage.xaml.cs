using System.Text.Json;
using System.ComponentModel;

namespace Freecost;

public partial class SettingsPage : ContentPage
{
    public bool IsNotLoggedIn => SessionService.IsNotLoggedIn;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SessionService.StaticPropertyChanged += OnSessionChanged;
        OnPropertyChanged(nameof(IsNotLoggedIn));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SessionService.StaticPropertyChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionService.IsNotLoggedIn))
        {
            OnPropertyChanged(nameof(IsNotLoggedIn));
        }
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
        if (SessionService.CurrentRestaurant?.Id == null)
        {
            await DisplayAlert("Location Needed", "Please select a location before importing data.", "OK");
            return;
        }

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Please select a JSON file to import",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.macOS, new[] { "json" } },
                })
            });

            if (result != null)
            {
                using (var stream = await result.OpenReadAsync())
                {
                    var allData = await JsonSerializer.DeserializeAsync<AllData>(stream);
                    if (allData != null)
                    {
                        await LocalStorageService.SaveAllDataAsync(SessionService.CurrentRestaurant.Id, allData);
                        await DisplayAlert("Import Complete", "Data has been imported successfully.", "OK");
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
        if (SessionService.CurrentRestaurant?.Id == null)
        {
            await DisplayAlert("Location Needed", "Please select a location before exporting data.", "OK");
            return;
        }

        try
        {
            var allData = await LocalStorageService.GetAllDataAsync(SessionService.CurrentRestaurant.Id);
            var json = JsonSerializer.Serialize(allData);
            var filePath = Path.Combine(FileSystem.CacheDirectory, "freecost_export.json");
            await File.WriteAllTextAsync(filePath, json);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Exported Freecost Data",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Error", $"An error occurred during export: {ex.Message}", "OK");
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
}