using System;
using System.Linq;
using System.Threading.Tasks;

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

        if (string.IsNullOrEmpty(Ingredient.Id))
        {
            Ingredient.Id = Guid.NewGuid().ToString();
        }

        if (SessionService.IsOffline)
        {
            var ingredients = await LocalStorageService.LoadAsync<IngredientCsvRecord>(restaurantId);
            var existing = ingredients.FirstOrDefault(i => i.Id == Ingredient.Id);
            if (existing != null)
            {
                ingredients[ingredients.IndexOf(existing)] = Ingredient;
            }
            else
            {
                ingredients.Add(Ingredient);
            }
            await LocalStorageService.SaveAsync(ingredients, restaurantId);
        }
        else
        {
            await FirestoreService.SetDocumentAsync($"restaurants/{restaurantId}/ingredients/{Ingredient.Id}", Ingredient, SessionService.AuthToken);
            // Also update the local cache
            var ingredients = await LocalStorageService.LoadAsync<IngredientCsvRecord>(restaurantId);
            var existing = ingredients.FirstOrDefault(i => i.Id == Ingredient.Id);
            if (existing != null)
            {
                ingredients[ingredients.IndexOf(existing)] = Ingredient;
            }
            else
            {
                ingredients.Add(Ingredient);
            }
            await LocalStorageService.SaveAsync(ingredients, restaurantId);
        }

        await Navigation.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}