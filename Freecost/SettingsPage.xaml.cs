using System.Text.Json;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using CommunityToolkit.Maui.Storage;
using System.Linq;
using Plugin.Firebase.Firestore;
using Plugin.Firebase.Core;

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

        await DisplayAlert("Syncing", "Syncing local data to the server. This may take a moment.", "OK");
        await FirestoreService.SyncLocalToServer();
        await DisplayAlert("Syncing", "Syncing server data to local. This may take a moment.", "OK");
        await FirestoreService.SyncServerToLocal();


        await DisplayAlert("Sync Complete", "Your local data has been synced with the server.", "OK");
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        var restaurantId = SessionService.CurrentRestaurant?.Id;
        if (restaurantId == null)
        {
            await DisplayAlert("Location Needed", "Please select a location before exporting data.", "OK");
            return;
        }

        var allData = await LocalStorageService.GetAllDataAsync(restaurantId);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            async Task AddToArchive<T>(List<T> data, string fileName)
            {
                var entry = archive.CreateEntry(fileName);
                using var entryStream = entry.Open();
                await JsonSerializer.SerializeAsync(entryStream, data, new JsonSerializerOptions { WriteIndented = true });
            }

            await AddToArchive(allData.Ingredients, "ingredients.json");
            await AddToArchive(allData.Recipes, "recipes.json");
            await AddToArchive(allData.Entrees, "entrees.json");
        }

        memoryStream.Position = 0;
        var fileSaverResult = await FileSaver.Default.SaveAsync("freecost_export.zip", memoryStream, default);
        if (!fileSaverResult.IsSuccessful)
        {
            await DisplayAlert("Export Failed", $"There was an error saving the file: {fileSaverResult.Exception?.Message}", "OK");
        }
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
        var currentRestaurant = SessionService.CurrentRestaurant;

        if (currentRestaurant == null || string.IsNullOrEmpty(currentRestaurant.Id))
        {
            await DisplayAlert("No Location Selected", "Please use the main menu to change your location before importing data.", "OK");
            return;
        }

        string restaurantId = currentRestaurant.Id;
        string restaurantName = currentRestaurant.Name ?? "your current location";

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Please select a data file to import",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".zip" } },
                    { DevicePlatform.macOS, new[] { "zip" } },
                })
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                var importedData = new AllData();

                async Task<List<T>?> ReadFromArchive<T>(string fileName)
                {
                    var entry = archive.GetEntry(fileName);
                    if (entry == null) return null;
                    using var entryStream = entry.Open();
                    return await JsonSerializer.DeserializeAsync<List<T>>(entryStream);
                }

                importedData.Ingredients = await ReadFromArchive<IngredientCsvRecord>("ingredients.json") ?? new List<IngredientCsvRecord>();
                importedData.Recipes = await ReadFromArchive<Recipe>("recipes.json") ?? new List<Recipe>();
                importedData.Entrees = await ReadFromArchive<Entree>("entrees.json") ?? new List<Entree>();

                var existingData = await LocalStorageService.GetAllDataAsync(restaurantId);
                var ingredientsToAdd = new List<IngredientCsvRecord>();
                var recipesToAdd = new List<Recipe>();
                var entreesToAdd = new List<Entree>();

                // Process Ingredients
                var existingIngredientIds = new HashSet<string?>(existingData.Ingredients.Select(i => i.Id));
                foreach (var imported in importedData.Ingredients)
                {
                    if (!string.IsNullOrEmpty(imported.Id) && !existingIngredientIds.Contains(imported.Id))
                    {
                        ingredientsToAdd.Add(imported);
                    }
                }
                existingData.Ingredients.AddRange(ingredientsToAdd);

                // Process Recipes
                var existingRecipeIds = new HashSet<string?>(existingData.Recipes.Select(r => r.Id));
                foreach (var imported in importedData.Recipes)
                {
                    if (!string.IsNullOrEmpty(imported.Id) && !existingRecipeIds.Contains(imported.Id))
                    {
                        imported.RestaurantId = restaurantId; // Ensure correct restaurant ID
                        recipesToAdd.Add(imported);
                    }
                }
                existingData.Recipes.AddRange(recipesToAdd);

                // Process Entrees
                var existingEntreeIds = new HashSet<string?>(existingData.Entrees.Select(e => e.Id));
                foreach (var imported in importedData.Entrees)
                {
                    if (!string.IsNullOrEmpty(imported.Id) && !existingEntreeIds.Contains(imported.Id))
                    {
                        imported.RestaurantId = restaurantId; // Ensure correct restaurant ID
                        entreesToAdd.Add(imported);
                    }
                }
                existingData.Entrees.AddRange(entreesToAdd);

                // Save all changes to local storage first
                await LocalStorageService.SaveAllDataAsync(restaurantId, existingData);

                // If online, also save the new items to Firestore to prevent them from being overwritten
                if (!SessionService.IsOffline)
                {
                    var firestore = CrossFirebase.Current.Firestore;
                    var batch = firestore.CreateBatch();

                    // Add new ingredients to Firestore
                    var ingredientsCollection = firestore.Collection("restaurants").Document(restaurantId).Collection("ingredients");
                    foreach (var item in ingredientsToAdd)
                    {
                        if (!string.IsNullOrEmpty(item.Id)) batch.Set(ingredientsCollection.Document(item.Id), item);
                    }

                    // Add new recipes to Firestore
                    var recipesCollection = firestore.Collection("recipes");
                    foreach (var item in recipesToAdd)
                    {
                        if (!string.IsNullOrEmpty(item.Id)) batch.Set(recipesCollection.Document(item.Id), item);
                    }

                    // Add new entrees to Firestore
                    var entreesCollection = firestore.Collection("entrees");
                    foreach (var item in entreesToAdd)
                    {
                        if (!string.IsNullOrEmpty(item.Id)) batch.Set(entreesCollection.Document(item.Id), item);
                    }

                    await batch.CommitAsync();
                }

                await DisplayAlert("Import Complete", $"Data successfully imported to '{restaurantName}':\n\n" +
                                                     $"- {ingredientsToAdd.Count} new ingredients added.\n" +
                                                     $"- {recipesToAdd.Count} new recipes added.\n" +
                                                     $"- {entreesToAdd.Count} new entrees added.", "OK");

            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Error", $"An error occurred during import: {ex.Message}", "OK");
        }
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
            Application.Current.MainPage = new MainShell();
        }
    }
}

