using System.Text.Json;
using System.ComponentModel;
using OfficeOpenXml;
using System.Linq;
using System.Globalization;
using System.IO;
using CsvHelper;

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
            // --- Ingredients Sheet ---
            var ingredientsSheet = package.Workbook.Worksheets.Add("Ingredients");
            ingredientsSheet.Cells.LoadFromCollection(allData.Ingredients, true);

            // --- Recipes Sheet ---
            var recipesSheet = package.Workbook.Worksheets.Add("Recipes");
            // Write headers manually
            recipesSheet.Cells[1, 1].Value = "Id";
            recipesSheet.Cells[1, 2].Value = "SKU";
            recipesSheet.Cells[1, 3].Value = "Name";
            recipesSheet.Cells[1, 4].Value = "Yield";
            recipesSheet.Cells[1, 5].Value = "YieldUnit";
            recipesSheet.Cells[1, 6].Value = "Directions";
            recipesSheet.Cells[1, 7].Value = "PhotoUrl";
            recipesSheet.Cells[1, 8].Value = "RestaurantId";
            recipesSheet.Cells[1, 9].Value = "Allergens";
            recipesSheet.Cells[1, 10].Value = "Ingredients";
            recipesSheet.Cells[1, 11].Value = "FoodCost";
            recipesSheet.Cells[1, 12].Value = "Price";
            // Write data row by row
            for (int i = 0; i < allData.Recipes.Count; i++)
            {
                var r = allData.Recipes[i];
                int row = i + 2;
                recipesSheet.Cells[row, 1].Value = r.Id;
                recipesSheet.Cells[row, 2].Value = r.SKU;
                recipesSheet.Cells[row, 3].Value = r.Name;
                recipesSheet.Cells[row, 4].Value = r.Yield;
                recipesSheet.Cells[row, 5].Value = r.YieldUnit;
                recipesSheet.Cells[row, 6].Value = r.Directions;
                recipesSheet.Cells[row, 7].Value = r.PhotoUrl;
                recipesSheet.Cells[row, 8].Value = r.RestaurantId;
                recipesSheet.Cells[row, 9].Value = r.Allergens != null ? string.Join(",", r.Allergens) : "";
                recipesSheet.Cells[row, 10].Value = r.Ingredients != null ? JsonSerializer.Serialize(r.Ingredients) : "";
                recipesSheet.Cells[row, 11].Value = r.FoodCost;
                recipesSheet.Cells[row, 12].Value = r.Price;
            }

            // --- Entrees Sheet ---
            var entreesSheet = package.Workbook.Worksheets.Add("Entrees");
            // Write headers manually
            entreesSheet.Cells[1, 1].Value = "Id";
            entreesSheet.Cells[1, 2].Value = "Name";
            entreesSheet.Cells[1, 3].Value = "Yield";
            entreesSheet.Cells[1, 4].Value = "YieldUnit";
            entreesSheet.Cells[1, 5].Value = "Directions";
            entreesSheet.Cells[1, 6].Value = "PhotoUrl";
            entreesSheet.Cells[1, 7].Value = "RestaurantId";
            entreesSheet.Cells[1, 8].Value = "Allergens";
            entreesSheet.Cells[1, 9].Value = "Components";
            entreesSheet.Cells[1, 10].Value = "FoodCost";
            entreesSheet.Cells[1, 11].Value = "Price";
            entreesSheet.Cells[1, 12].Value = "PlatePrice";
            // Write data row by row
            for (int i = 0; i < allData.Entrees.Count; i++)
            {
                var eData = allData.Entrees[i];
                int row = i + 2;
                entreesSheet.Cells[row, 1].Value = eData.Id;
                entreesSheet.Cells[row, 2].Value = eData.Name;
                entreesSheet.Cells[row, 3].Value = eData.Yield;
                entreesSheet.Cells[row, 4].Value = eData.YieldUnit;
                entreesSheet.Cells[row, 5].Value = eData.Directions;
                entreesSheet.Cells[row, 6].Value = eData.PhotoUrl;
                entreesSheet.Cells[row, 7].Value = eData.RestaurantId;
                entreesSheet.Cells[row, 8].Value = eData.Allergens != null ? string.Join(",", eData.Allergens) : "";
                entreesSheet.Cells[row, 9].Value = eData.Components != null ? JsonSerializer.Serialize(eData.Components) : "";
                entreesSheet.Cells[row, 10].Value = eData.FoodCost;
                entreesSheet.Cells[row, 11].Value = eData.Price;
                entreesSheet.Cells[row, 12].Value = eData.PlatePrice;
            }

#if WINDOWS || MACCATALYST
            var fileSaverResult = await CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync("freecost_export.xlsx", new MemoryStream(package.GetAsByteArray()), default);
            if (!fileSaverResult.IsSuccessful)
            {
                await DisplayAlert("Export Failed", $"There was an error saving the file: {fileSaverResult.Exception?.Message}", "OK");
            }
#else
            var filePath = Path.Combine(FileSystem.CacheDirectory, "freecost_export.xlsx");
            File.WriteAllBytes(filePath, package.GetAsByteArray());
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Exported Data",
                File = new ShareFile(filePath)
            });
#endif
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
                    // Add other platforms if needed
                })
            });

            if (result != null)
            {
                using (var stream = await result.OpenReadAsync())
                {
                    AllData importedData = new AllData();
                    using (var package = new ExcelPackage(stream))
                    {
                        // --- Read Ingredients ---
                        var ingredientsSheet = package.Workbook.Worksheets["Ingredients"];
                        if (ingredientsSheet != null)
                        {
                            for (int row = 2; row <= ingredientsSheet.Dimension.End.Row; row++)
                            {
                                // Skip if ItemName is empty, likely a blank row
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

                        // --- Read Recipes ---
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
                                    RestaurantId = recipesSheet.Cells[row, 8].Text,
                                    Allergens = recipesSheet.Cells[row, 9].Text?.Split(',').ToList() ?? new List<string>(),
                                    Ingredients = JsonSerializer.Deserialize<List<RecipeIngredient>>(recipesSheet.Cells[row, 10].Text ?? "[]"),
                                    FoodCost = double.TryParse(recipesSheet.Cells[row, 11].Text, out var fc) ? fc : 0,
                                    Price = double.TryParse(recipesSheet.Cells[row, 12].Text, out var p) ? p : 0
                                });
                            }
                        }

                        // --- Read Entrees ---
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
                                    RestaurantId = entreesSheet.Cells[row, 7].Text,
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

                    // --- Merge Ingredients using ID ---
                    var existingIngredients = existingData.Ingredients.Where(i => !string.IsNullOrEmpty(i.Id)).ToDictionary(i => i.Id!);
                    foreach (var imported in importedData.Ingredients)
                    {
                        if (!string.IsNullOrEmpty(imported.Id) && existingIngredients.TryGetValue(imported.Id, out var existing))
                        {
                            existing.SupplierName = imported.SupplierName;
                            existing.ItemName = imported.ItemName;
                            existing.AliasName = imported.AliasName;
                            existing.CasePrice = imported.CasePrice;
                            existing.CaseQuantity = imported.CaseQuantity;
                            existing.Unit = imported.Unit;
                            existing.SKU = imported.SKU;
                            ingredientsUpdated++;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(imported.Id)) imported.Id = Guid.NewGuid().ToString();
                            existingData.Ingredients.Add(imported);
                            ingredientsAdded++;
                        }
                    }

                    // --- Merge Recipes using ID ---
                    var existingRecipes = existingData.Recipes.Where(r => !string.IsNullOrEmpty(r.Id)).ToDictionary(r => r.Id!);
                    foreach (var imported in importedData.Recipes)
                    {
                        imported.RestaurantId = restaurantId; // Ensure correct restaurant ID
                        if (!string.IsNullOrEmpty(imported.Id) && existingRecipes.TryGetValue(imported.Id, out var existing))
                        {
                            existing.Name = imported.Name;
                            existing.SKU = imported.SKU;
                            existing.Yield = imported.Yield;
                            existing.YieldUnit = imported.YieldUnit;
                            existing.Directions = imported.Directions;
                            existing.PhotoUrl = imported.PhotoUrl;
                            existing.Allergens = imported.Allergens;
                            existing.Ingredients = imported.Ingredients;
                            existing.FoodCost = imported.FoodCost;
                            existing.Price = imported.Price;
                            recipesUpdated++;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(imported.Id)) imported.Id = Guid.NewGuid().ToString();
                            existingData.Recipes.Add(imported);
                            recipesAdded++;
                        }
                    }

                    // --- Merge Entrees using ID ---
                    var existingEntrees = existingData.Entrees.Where(e => !string.IsNullOrEmpty(e.Id)).ToDictionary(e => e.Id!);
                    foreach (var imported in importedData.Entrees)
                    {
                        imported.RestaurantId = restaurantId; // Ensure correct restaurant ID
                        if (!string.IsNullOrEmpty(imported.Id) && existingEntrees.TryGetValue(imported.Id, out var existing))
                        {
                            existing.Name = imported.Name;
                            existing.Yield = imported.Yield;
                            existing.YieldUnit = imported.YieldUnit;
                            existing.Directions = imported.Directions;
                            existing.PhotoUrl = imported.PhotoUrl;
                            existing.Allergens = imported.Allergens;
                            existing.Components = imported.Components;
                            existing.FoodCost = imported.FoodCost;
                            existing.Price = imported.Price;
                            existing.PlatePrice = imported.PlatePrice;
                            entreesUpdated++;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(imported.Id)) imported.Id = Guid.NewGuid().ToString();
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