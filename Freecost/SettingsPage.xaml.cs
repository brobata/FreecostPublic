using System.Text.Json;
using System.ComponentModel;
using OfficeOpenXml;
using System.Linq;
using System.IO;
using CommunityToolkit.Maui.Storage;

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

        using (var package = new ExcelPackage())
        {
            var ingredientsSheet = package.Workbook.Worksheets.Add("Ingredients");
            ingredientsSheet.Cells.LoadFromCollection(allData.Ingredients, true);

            var recipesSheet = package.Workbook.Worksheets.Add("Recipes");
            recipesSheet.Cells.LoadFromCollection(allData.Recipes.Select(r => new {
                r.Id,
                r.SKU,
                r.Name,
                r.Yield,
                r.YieldUnit,
                r.Directions,
                r.PhotoUrl,
                r.RestaurantId,
                Allergens = r.Allergens != null ? string.Join(",", r.Allergens) : "",
                Ingredients = r.Ingredients != null ? JsonSerializer.Serialize(r.Ingredients) : "",
                r.FoodCost,
                r.Price
            }), true);

            var entreesSheet = package.Workbook.Worksheets.Add("Entrees");
            entreesSheet.Cells.LoadFromCollection(allData.Entrees.Select(e => new {
                e.Id,
                e.Name,
                e.Yield,
                e.YieldUnit,
                e.Directions,
                e.PhotoUrl,
                e.RestaurantId,
                Allergens = e.Allergens != null ? string.Join(",", e.Allergens) : "",
                Components = e.Components != null ? JsonSerializer.Serialize(e.Components) : "",
                e.FoodCost,
                e.Price,
                e.PlatePrice
            }), true);

            using var stream = new MemoryStream();
            await package.SaveAsAsync(stream);

            var fileSaverResult = await FileSaver.Default.SaveAsync("freecost_export.xlsx", stream, default);
            if (!fileSaverResult.IsSuccessful)
            {
                await DisplayAlert("Export Failed", $"There was an error saving the file: {fileSaverResult.Exception?.Message}", "OK");
            }
        }
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
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
                    { DevicePlatform.WinUI, new[] { ".xlsx" } },
                    { DevicePlatform.macOS, new[] { "xlsx" } },
                })
            });

            if (result != null)
            {
                using (var stream = await result.OpenReadAsync())
                {
                    AllData importedData = new AllData();
                    using (var package = new ExcelPackage(stream))
                    {
                        var ingredientsSheet = package.Workbook.Worksheets["Ingredients"];
                        if (ingredientsSheet != null)
                        {
                            for (int row = 2; row <= ingredientsSheet.Dimension.End.Row; row++)
                            {
                                var itemName = ingredientsSheet.Cells[row, 3].Text;
                                if (string.IsNullOrWhiteSpace(itemName)) continue;
                                importedData.Ingredients.Add(new IngredientCsvRecord
                                {
                                    Id = ingredientsSheet.Cells[row, 1].Text,
                                    SupplierName = ingredientsSheet.Cells[row, 2].Text,
                                    ItemName = itemName,
                                    AliasName = ingredientsSheet.Cells[row, 4].Text,
                                    CasePrice = double.TryParse(ingredientsSheet.Cells[row, 5].Text, out var cp) ? cp : 0,
                                    CaseQuantity = double.TryParse(ingredientsSheet.Cells[row, 6].Text, out var cq) ? cq : 0,
                                    Unit = ingredientsSheet.Cells[row, 7].Text,
                                    SKU = ingredientsSheet.Cells[row, 8].Text
                                });
                            }
                        }

                        var recipesSheet = package.Workbook.Worksheets["Recipes"];
                        if (recipesSheet != null)
                        {
                            for (int row = 2; row <= recipesSheet.Dimension.End.Row; row++)
                            {
                                var recipeName = recipesSheet.Cells[row, 3].Text;
                                if (string.IsNullOrWhiteSpace(recipeName)) continue;
                                importedData.Recipes.Add(new Recipe
                                {
                                    Id = recipesSheet.Cells[row, 1].Text,
                                    SKU = recipesSheet.Cells[row, 2].Text,
                                    Name = recipeName,
                                    Yield = double.TryParse(recipesSheet.Cells[row, 4].Text, out var y) ? y : 0,
                                    YieldUnit = recipesSheet.Cells[row, 5].Text,
                                    Directions = recipesSheet.Cells[row, 6].Text,
                                    PhotoUrl = recipesSheet.Cells[row, 7].Text,
                                    RestaurantId = restaurantId,
                                    Allergens = recipesSheet.Cells[row, 9].Text?.Split(',').ToList() ?? new List<string>(),
                                    Ingredients = JsonSerializer.Deserialize<List<RecipeIngredient>>(recipesSheet.Cells[row, 10].Text ?? "[]"),
                                    FoodCost = double.TryParse(recipesSheet.Cells[row, 11].Text, out var fc) ? fc : 0,
                                    Price = double.TryParse(recipesSheet.Cells[row, 12].Text, out var p) ? p : 0
                                });
                            }
                        }

                        var entreesSheet = package.Workbook.Worksheets["Entrees"];
                        if (entreesSheet != null)
                        {
                            for (int row = 2; row <= entreesSheet.Dimension.End.Row; row++)
                            {
                                var entreeName = entreesSheet.Cells[row, 2].Text;
                                if (string.IsNullOrWhiteSpace(entreeName)) continue;
                                importedData.Entrees.Add(new Entree
                                {
                                    Id = entreesSheet.Cells[row, 1].Text,
                                    Name = entreeName,
                                    Yield = double.TryParse(entreesSheet.Cells[row, 3].Text, out var y) ? y : 0,
                                    YieldUnit = entreesSheet.Cells[row, 4].Text,
                                    Directions = entreesSheet.Cells[row, 5].Text,
                                    PhotoUrl = entreesSheet.Cells[row, 6].Text,
                                    RestaurantId = restaurantId,
                                    Allergens = entreesSheet.Cells[row, 8].Text?.Split(',').ToList() ?? new List<string>(),
                                    Components = JsonSerializer.Deserialize<List<EntreeComponent>>(entreesSheet.Cells[row, 9].Text ?? "[]"),
                                    FoodCost = double.TryParse(entreesSheet.Cells[row, 10].Text, out var fc) ? fc : 0,
                                    Price = double.TryParse(entreesSheet.Cells[row, 11].Text, out var p) ? p : 0,
                                    PlatePrice = double.TryParse(entreesSheet.Cells[row, 12].Text, out var pp) ? pp : 0
                                });
                            }
                        }
                    }

                    var existingData = await LocalStorageService.GetAllDataAsync(restaurantId);
                    int ingredientsAdded = 0, ingredientsUpdated = 0;
                    int recipesAdded = 0, recipesUpdated = 0;
                    int entreesAdded = 0, entreesUpdated = 0;

                    var existingIngredients = existingData.Ingredients.ToDictionary(i => i.Id ?? Guid.NewGuid().ToString());
                    foreach (var imported in importedData.Ingredients)
                    {
                        var key = string.IsNullOrEmpty(imported.Id) ? Guid.NewGuid().ToString() : imported.Id;
                        if (existingIngredients.ContainsKey(key))
                        {
                            existingIngredients[key] = imported;
                            ingredientsUpdated++;
                        }
                        else
                        {
                            existingData.Ingredients.Add(imported);
                            ingredientsAdded++;
                        }
                    }

                    var existingRecipes = existingData.Recipes.ToDictionary(r => r.Id ?? Guid.NewGuid().ToString());
                    foreach (var imported in importedData.Recipes)
                    {
                        var key = string.IsNullOrEmpty(imported.Id) ? Guid.NewGuid().ToString() : imported.Id;
                        if (existingRecipes.ContainsKey(key))
                        {
                            existingRecipes[key] = imported;
                            recipesUpdated++;
                        }
                        else
                        {
                            existingData.Recipes.Add(imported);
                            recipesAdded++;
                        }
                    }

                    var existingEntrees = existingData.Entrees.ToDictionary(e => e.Id ?? Guid.NewGuid().ToString());
                    foreach (var imported in importedData.Entrees)
                    {
                        var key = string.IsNullOrEmpty(imported.Id) ? Guid.NewGuid().ToString() : imported.Id;
                        if (existingEntrees.ContainsKey(key))
                        {
                            existingEntrees[key] = imported;
                            entreesUpdated++;
                        }
                        else
                        {
                            existingData.Entrees.Add(imported);
                            entreesAdded++;
                        }
                    }

                    await LocalStorageService.SaveAllDataAsync(restaurantId, existingData);
                    await DisplayAlert("Import Complete", $"Data merged successfully:\n" +
                                                         $"- Ingredients: {ingredientsAdded} added, {ingredientsUpdated} updated.\n" +
                                                         $"- Recipes: {recipesAdded} added, {recipesUpdated} updated.\n" +
                                                         $"- Entrees: {entreesAdded} added, {entreesUpdated} updated.", "OK");
                }
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

