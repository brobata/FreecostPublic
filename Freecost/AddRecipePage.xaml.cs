using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Freecost
{
    public partial class AddRecipePage : ContentPage
    {
        public class AllergenSelection
        {
            public string Name { get; set; }
            public bool IsSelected { get; set; }

            public AllergenSelection()
            {
                Name = string.Empty;
            }
        }
        public Recipe RecipeData { get; private set; }
        public List<RecipeIngredient> RecipeIngredients { get; private set; }

        private FirestoreDb? db;
        private string restaurantId;
        private List<IngredientCsvRecord>? masterIngredientList;
        private List<AllergenSelection> Allergens { get; set; }

        public AddRecipePage(string currentRestaurantId, Recipe? recipeToEdit = null)
        {
            InitializeComponent();
            restaurantId = currentRestaurantId;

            RecipeData = recipeToEdit ?? new Recipe();
            RecipeIngredients = recipeToEdit?.Ingredients ?? new List<RecipeIngredient>();

            var topAllergens = new List<string> { "Milk", "Eggs", "Fish", "Shellfish", "Tree Nuts", "Peanuts", "Wheat", "Soy", "Vegan", "Vegetarian", "Halal", "Kosher" };
            Allergens = topAllergens.Select(a => new AllergenSelection { Name = a, IsSelected = RecipeData.Allergens?.Contains(a) ?? false }).ToList();

            // Populate the allergen chips
            foreach (var allergen in Allergens)
            {
                var chip = new Button
                {
                    Text = allergen.Name
                };

#if ANDROID || IOS || MACCATALYST || WINDOWS
                if (Application.Current != null && Application.Current.Resources != null)
                {
                    chip.Style = (Style)Application.Current.Resources["ChipStyle"];
                    chip.BackgroundColor = allergen.IsSelected ? (Color)Application.Current.Resources["Accent"] : (Color)Application.Current.Resources["Secondary"];
                }
#endif

                chip.Clicked += (s, e) =>
                {
                    allergen.IsSelected = !allergen.IsSelected;
#if ANDROID || IOS || MACCATALYST || WINDOWS
                    if (Application.Current != null && Application.Current.Resources != null)
                    {
                        chip.BackgroundColor = allergen.IsSelected ? (Color)Application.Current.Resources["Accent"] : (Color)Application.Current.Resources["Secondary"];
                    }
#endif
                };
                AllergensLayout.Children.Add(chip);
            }


            LoadMasterIngredients();
            LoadUnitDropdowns();

            if (recipeToEdit == null) // This is for ADDING a new recipe
            {
                DirectionsEditor.Text = "1. ";
            }
            else // This is for EDITING an existing recipe
            {
                if (recipeToEdit.Name != null)
                {
                    RecipeNameEntry.Text = recipeToEdit.Name;
                }
                YieldEntry.Text = recipeToEdit.Yield.ToString();
                if (recipeToEdit.Directions != null)
                {
                    DirectionsEditor.Text = recipeToEdit.Directions;
                }
                if (recipeToEdit.YieldUnit != null)
                {
                    YieldUnitPicker.SelectedItem = recipeToEdit.YieldUnit;
                }
                else
                {
                    YieldUnitPicker.SelectedIndex = -1;
                }
                RefreshRecipeGrid();
            }
        }
        private void OnIngredientPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (IngredientPicker.SelectedItem is IngredientDisplay selectedIngredientDisplay)
            {
                var selectedMasterIngredient = selectedIngredientDisplay.OriginalIngredient;

                if (selectedMasterIngredient != null && !string.IsNullOrEmpty(selectedMasterIngredient.Unit))
                {
                    // Find the category of the selected ingredient's unit
                    string? category = UnitConverter.GetCategoryForUnit(selectedMasterIngredient.Unit);

                    // Filter the unit dropdown to only show units of the same category
                    if (category != null)
                    {
                        UnitPicker.ItemsSource = UnitConverter.GetUnitsForCategory(category);
                    }
                }
            }
        }
        private void LoadUnitDropdowns()
        {
            var allUnits = UnitConverter.GetAllUnitNames();
            UnitPicker.ItemsSource = new List<string>(allUnits);
            YieldUnitPicker.ItemsSource = new List<string>(allUnits);
        }

        private async void LoadMasterIngredients()
        {
            db = FirestoreService.Db;
            if (db == null || restaurantId == null) return;
            var ingredientsCollection = db.Collection("restaurants").Document(restaurantId).Collection("ingredients");
            var snapshot = await ingredientsCollection.GetSnapshotAsync();
            masterIngredientList = snapshot.Documents.Select(doc => doc.ConvertTo<IngredientCsvRecord>()).ToList();

            if (masterIngredientList == null) return;

            var displayList = masterIngredientList.Select(ing => new IngredientDisplay
            {
                DisplayName = !string.IsNullOrEmpty(ing.AliasName) ? ing.AliasName : ing.ItemName,
                OriginalIngredient = ing
            }).ToList();

            IngredientPicker.ItemsSource = displayList;
            IngredientPicker.ItemDisplayBinding = new Binding("DisplayName");
        }


        private void OnAddIngredientClicked(object sender, EventArgs e)
        {
#if ANDROID || IOS || MACCATALYST || WINDOWS
            if (IngredientPicker.SelectedItem == null)
            {
                if (Application.Current?.MainPage != null)
                    Application.Current.MainPage.DisplayAlert("No Ingredient Selected", "Please select an ingredient from the dropdown list first.", "OK");
                return;
            }

            if (!double.TryParse(QuantityEntry.Text, out double quantity) || quantity <= 0)
            {
                if (Application.Current?.MainPage != null)
                    Application.Current.MainPage.DisplayAlert("Invalid Quantity", "Please enter a valid, positive number for the quantity.", "OK");
                return;
            }
#endif

            var selectedIngredientDisplay = IngredientPicker.SelectedItem as IngredientDisplay;
            if (selectedIngredientDisplay?.OriginalIngredient == null) return;

            var selectedMasterIngredient = selectedIngredientDisplay.OriginalIngredient;
            var ingredientToAdd = new RecipeIngredient
            {
                IngredientId = selectedMasterIngredient.Id,
                Name = !string.IsNullOrEmpty(selectedMasterIngredient.AliasName) ? selectedMasterIngredient.AliasName : selectedMasterIngredient.ItemName ?? string.Empty,
                Quantity = quantity,
                Unit = UnitPicker.SelectedItem?.ToString() ?? string.Empty
            };

            RecipeIngredients.Add(ingredientToAdd);
            RefreshRecipeGrid();

            QuantityEntry.Text = string.Empty;
            UnitPicker.SelectedIndex = -1;
            IngredientPicker.Focus();
        }

        private void OnDeleteIngredientClicked(object sender, EventArgs e)
        {
            var swipeItem = sender as SwipeItem;
            if (swipeItem?.CommandParameter is RecipeIngredient ingredientToDelete)
            {
                RecipeIngredients.Remove(ingredientToDelete);
                RefreshRecipeGrid();
            }
        }

        private void RefreshRecipeGrid()
        {
            IngredientsCollection.ItemsSource = null;
            IngredientsCollection.ItemsSource = new List<RecipeIngredient>(RecipeIngredients);
        }

        private void OnAddStepClicked(object sender, EventArgs e)
        {
            int lastNumber = DirectionsEditor.Text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => int.TryParse(line.Split('.').FirstOrDefault(), out int num) ? num : 0).DefaultIfEmpty(0).Max();
            DirectionsEditor.Text += $"\n{lastNumber + 1}. ";
            DirectionsEditor.Focus();
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (RecipeData == null) RecipeData = new Recipe();

            double.TryParse(YieldEntry.Text, out double yield);
            if (yield == 0) yield = 1;

            RecipeData.Name = RecipeNameEntry.Text;
            RecipeData.Yield = yield;
            RecipeData.YieldUnit = YieldUnitPicker.SelectedItem?.ToString() ?? "";
            RecipeData.Directions = DirectionsEditor.Text;
            RecipeData.Ingredients = RecipeIngredients;
            RecipeData.RestaurantId = restaurantId;
            RecipeData.Allergens = Allergens.Where(a => a.IsSelected).Select(a => a.Name).ToList();

            db = FirestoreService.Db;
            if (db == null || restaurantId == null) return;

            var collection = db.Collection("recipes");

            if (string.IsNullOrEmpty(RecipeData.Id))
            {
                await collection.AddAsync(RecipeData);
            }
            else
            {
                await collection.Document(RecipeData.Id).SetAsync(RecipeData);
            }

            await Navigation.PopAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}