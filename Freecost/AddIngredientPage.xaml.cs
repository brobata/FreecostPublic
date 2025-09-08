using System;
using System.Linq;

namespace Freecost;

public partial class AddIngredientPage : ContentPage
{
    public IngredientCsvRecord Ingredient { get; private set; }

    public AddIngredientPage(IngredientCsvRecord? ingredientToEdit = null)
    {
        InitializeComponent();
        LoadUnitDropdown();

        if (ingredientToEdit != null)
        {
            Ingredient = ingredientToEdit;
            SupplierNameEntry.Text = ingredientToEdit.SupplierName;
            ItemNameEntry.Text = ingredientToEdit.ItemName;
            AliasNameEntry.Text = ingredientToEdit.AliasName;
            SKUEntry.Text = ingredientToEdit.SKU;
            CasePriceEntry.Text = ingredientToEdit.CasePrice.ToString();
            CaseQuantityEntry.Text = ingredientToEdit.CaseQuantity.ToString();
            UnitPicker.SelectedItem = ingredientToEdit.Unit;
        }
        else
        {
            Ingredient = new IngredientCsvRecord();
        }
    }

    private void LoadUnitDropdown()
    {
        UnitPicker.ItemsSource = UnitConverter.GetAllUnitNames();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        Ingredient.SupplierName = SupplierNameEntry.Text;
        Ingredient.ItemName = ItemNameEntry.Text;
        Ingredient.AliasName = AliasNameEntry.Text;
        Ingredient.SKU = SKUEntry.Text;
        Ingredient.CasePrice = Convert.ToDouble(CasePriceEntry.Text);
        Ingredient.CaseQuantity = Convert.ToDouble(CaseQuantityEntry.Text);
        Ingredient.Unit = UnitPicker.SelectedItem?.ToString() ?? string.Empty;

        var restaurantId = SessionService.CurrentRestaurant?.Id;
        if (restaurantId == null)
        {
            await DisplayAlert("Error", "No restaurant selected.", "OK");
            return;
        }

        // Save to local storage first for offline-first approach
        var ingredients = await LocalStorageService.LoadAsync<IngredientCsvRecord>(restaurantId);
        if (string.IsNullOrEmpty(Ingredient.Id))
        {
            Ingredient.Id = Guid.NewGuid().ToString();
            ingredients.Add(Ingredient);
        }
        else
        {
            var existing = ingredients.FirstOrDefault(i => i.Id == Ingredient.Id);
            if (existing != null)
            {
                ingredients[ingredients.IndexOf(existing)] = Ingredient;
            }
        }
        await LocalStorageService.SaveAsync(ingredients, restaurantId);

        // If online, also save to Firestore
        if (!SessionService.IsOffline)
        {
            var ingredientToSave = new // Create a clean object without the ID for adding
            {
                Ingredient.SupplierName,
                Ingredient.ItemName,
                Ingredient.AliasName,
                Ingredient.SKU,
                Ingredient.CasePrice,
                Ingredient.CaseQuantity,
                Ingredient.Unit
            };

            if (string.IsNullOrEmpty(Ingredient.Id) || ingredients.Any(i => i.Id == Ingredient.Id)) // It's a new item
            {
                await FirestoreService.AddDocumentAsync($"restaurants/{restaurantId}/ingredients", ingredientToSave, SessionService.AuthToken);
            }
            else
            {
                await FirestoreService.SetDocumentAsync($"restaurants/{restaurantId}/ingredients/{Ingredient.Id}", ingredientToSave, SessionService.AuthToken);
            }
        }

        await Navigation.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}