using System;
using System.Collections.Generic;
using System.Linq;

namespace Freecost;

public partial class AddIngredientPage : ContentPage
{
    public IngredientCsvRecord Ingredient { get; private set; } = new IngredientCsvRecord();

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

        var db = FirestoreService.Db;
        var restaurantId = SessionService.CurrentRestaurant?.Id;
        if (db == null || restaurantId == null) return;

        var collection = db.Collection("restaurants").Document(restaurantId).Collection("ingredients");

        // Create a new IngredientCsvRecord from the Ingredient property to ensure it has the FirestoreData attribute
        var ingredientToSave = new IngredientCsvRecord
        {
            SupplierName = Ingredient.SupplierName,
            ItemName = Ingredient.ItemName,
            AliasName = Ingredient.AliasName,
            SKU = Ingredient.SKU,
            CasePrice = Ingredient.CasePrice,
            CaseQuantity = Ingredient.CaseQuantity,
            Unit = Ingredient.Unit
        };

        if (string.IsNullOrEmpty(Ingredient.Id))
        {
            await collection.AddAsync(ingredientToSave);
        }
        else
        {
            await collection.Document(Ingredient.Id).SetAsync(ingredientToSave);
        }

        await Navigation.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
