using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Plugin.Firebase.Firestore;

namespace Freecost
{
    public partial class AddIngredientPage : ContentPage
    {
        public IngredientCsvRecord Ingredient { get; private set; }

        public AddIngredientPage(IngredientCsvRecord? ingredientToEdit = null)
        {
            InitializeComponent();
            LoadUnitDropdown();

            Ingredient = ingredientToEdit ?? new IngredientCsvRecord();
            if (ingredientToEdit != null)
            {
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
            Ingredient ??= new IngredientCsvRecord();

            Ingredient.SupplierName = SupplierNameEntry.Text;
            Ingredient.ItemName = ItemNameEntry.Text;
            Ingredient.AliasName = AliasNameEntry.Text;
            Ingredient.SKU = SKUEntry.Text;
            double.TryParse(CasePriceEntry.Text, out var price);
            Ingredient.CasePrice = price;
            double.TryParse(CaseQuantityEntry.Text, out var quantity);
            Ingredient.CaseQuantity = quantity;
            Ingredient.Unit = UnitPicker.SelectedItem?.ToString() ?? string.Empty;

            var restaurantId = SessionService.CurrentRestaurant?.Id;
            if (string.IsNullOrEmpty(restaurantId))
            {
                await DisplayAlert("Error", "No restaurant selected.", "OK");
                return;
            }

            try
            {
                if (SessionService.IsOffline)
                {
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
                            var index = ingredients.IndexOf(existing);
                            ingredients[index] = Ingredient;
                        }
                    }
                    await LocalStorageService.SaveAsync(ingredients, restaurantId);
                }
                else
                {
                    var collection = CrossFirebase.Current.Firestore
                        .Collection("restaurants")
                        .Document(restaurantId)
                        .Collection("ingredients");

                    if (string.IsNullOrEmpty(Ingredient.Id))
                    {
                        await collection.AddAsync(Ingredient);
                    }
                    else
                    {
                        await collection.Document(Ingredient.Id).SetAsync(Ingredient);
                    }
                }
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save Error", $"Failed to save ingredient: {ex.Message}", "OK");
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}