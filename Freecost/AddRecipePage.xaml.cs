using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Freecost
{
    public partial class AddRecipePage : ContentPage, INotifyPropertyChanged
    {
        private RecipeIngredient? _selectedIngredient;
        public RecipeIngredient? SelectedIngredient
        {
            get => _selectedIngredient;
            set
            {
                _selectedIngredient = value;
                OnPropertyChanged();
            }
        }

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
        private string? _uploadedPhotoUrl;

        public AddRecipePage(string currentRestaurantId, Recipe? recipeToEdit = null)
        {
            InitializeComponent();
            BindingContext = this;
            restaurantId = currentRestaurantId;

            RecipeData = recipeToEdit ?? new Recipe();
            _uploadedPhotoUrl = RecipeData.PhotoUrl;
            RecipeIngredients = recipeToEdit?.Ingredients ?? new List<RecipeIngredient>();

            var topAllergens = new List<string> { "Milk", "Eggs", "Fish", "Shellfish", "Tree Nuts", "Peanuts", "Wheat", "Soy", "Vegan", "Vegetarian", "Halal", "Kosher" };
            Allergens = topAllergens.Select(a => new AllergenSelection { Name = a, IsSelected = RecipeData.Allergens?.Contains(a) ?? false }).ToList();

            foreach (var allergen in Allergens)
            {
                var chip = new Button { Text = allergen.Name };
                if (Application.Current?.Resources != null)
                {
                    chip.Style = (Style)Application.Current.Resources["ChipStyle"];
                    chip.BackgroundColor = allergen.IsSelected ? (Color)Application.Current.Resources["Accent"] : (Color)Application.Current.Resources["Secondary"];
                }
                chip.Clicked += (s, e) =>
                {
                    allergen.IsSelected = !allergen.IsSelected;
                    if (Application.Current?.Resources != null)
                    {
                        chip.BackgroundColor = allergen.IsSelected ? (Color)Application.Current.Resources["Accent"] : (Color)Application.Current.Resources["Secondary"];
                    }
                };
                AllergensLayout.Children.Add(chip);
            }

            LoadMasterIngredients();
            LoadUnitDropdowns();
            DirectionsEditor.TextChanged += OnDirectionsEditorTextChanged;

            if (recipeToEdit == null)
            {
                DirectionsEditor.Text = "1. ";
            }
            else
            {
                if (recipeToEdit.Name != null) RecipeNameEntry.Text = recipeToEdit.Name;
                YieldEntry.Text = recipeToEdit.Yield.ToString();
                if (recipeToEdit.Directions != null) DirectionsEditor.Text = recipeToEdit.Directions;
                if (recipeToEdit.YieldUnit != null) YieldUnitPicker.SelectedItem = recipeToEdit.YieldUnit;
                else YieldUnitPicker.SelectedIndex = -1;
                PreviewImage.Source = recipeToEdit.PhotoUrl;
                RefreshRecipeGrid();
            }
        }

        private void OnIngredientTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is RecipeIngredient tappedIngredient)
            {
                SelectedIngredient = tappedIngredient;
            }
        }

        private async void OnUploadImageClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Please select an image file", FileTypes = FilePickerFileType.Images });
                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(result.FileName)}";
                    var downloadUrl = await FirebaseStorageService.UploadImageAsync(stream, fileName);
                    _uploadedPhotoUrl = downloadUrl;
                    PreviewImage.Source = ImageSource.FromUri(new Uri(downloadUrl));
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Image upload failed: {ex.Message}", "OK");
            }
        }

        private void OnDirectionsEditorTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is Editor editor && editor.Text.Contains(Environment.NewLine))
            {
                editor.Text = editor.Text.Replace(Environment.NewLine, "");
                OnAddStepClicked(this, EventArgs.Empty);
            }
        }

        private void OnIngredientPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (IngredientPicker.SelectedItem is IngredientDisplay selectedIngredientDisplay)
            {
                var selectedMasterIngredient = selectedIngredientDisplay.OriginalIngredient;
                if (selectedMasterIngredient != null && !string.IsNullOrEmpty(selectedMasterIngredient.Unit))
                {
                    string? category = UnitConverter.GetCategoryForUnit(selectedMasterIngredient.Unit);
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
            if (SessionService.IsOffline)
            {
                masterIngredientList = await LocalStorageService.LoadAsync<IngredientCsvRecord>();
            }
            else
            {
                db = FirestoreService.Db;
                if (db == null || restaurantId == null) return;
                var ingredientsCollection = db.Collection("restaurants").Document(restaurantId).Collection("ingredients");
                var snapshot = await ingredientsCollection.GetSnapshotAsync();
                masterIngredientList = snapshot.Documents.Select(doc =>
                {
                    var ingredient = doc.ConvertTo<IngredientCsvRecord>();
                    ingredient.Id = doc.Id;
                    return ingredient;
                }).ToList();
            }

            if (masterIngredientList == null) return;

            var displayList = masterIngredientList.Select(ing => new IngredientDisplay
            {
                DisplayName = !string.IsNullOrEmpty(ing.AliasName) ? ing.AliasName : ing.ItemName,
                OriginalIngredient = ing
            }).OrderBy(d => d.DisplayName).ToList();

            IngredientPicker.ItemsSource = displayList;
            IngredientPicker.ItemDisplayBinding = new Binding("DisplayName");
        }


        private void OnAddIngredientClicked(object sender, EventArgs e)
        {
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

        private async void OnDeleteIngredientClicked(object sender, EventArgs e)
        {
            if (SelectedIngredient == null)
            {
                await DisplayAlert("No Selection", "Please select an ingredient to delete.", "OK");
                return;
            }

            bool answer = await DisplayAlert("Confirm", $"Are you sure you want to delete {SelectedIngredient.Name}?", "Yes", "No");
            if (answer)
            {
                RecipeIngredients.Remove(SelectedIngredient);
                RefreshRecipeGrid();
            }
        }

        private void OnEditIngredientClicked(object sender, EventArgs e)
        {
            if (SelectedIngredient == null)
            {
                DisplayAlert("No Selection", "Please select an ingredient to edit.", "OK");
                return;
            }
            var ingredientToEdit = SelectedIngredient;
            var ingredientDisplay = IngredientPicker.ItemsSource.Cast<IngredientDisplay>().FirstOrDefault(i => i.OriginalIngredient?.Id == ingredientToEdit.IngredientId);
            if (ingredientDisplay != null) IngredientPicker.SelectedItem = ingredientDisplay;
            QuantityEntry.Text = ingredientToEdit.Quantity.ToString();
            UnitPicker.SelectedItem = ingredientToEdit.Unit;
            RecipeIngredients.Remove(ingredientToEdit);
            RefreshRecipeGrid();
        }

        private void RefreshRecipeGrid()
        {
            IngredientsCollection.ItemsSource = null;
            IngredientsCollection.ItemsSource = new List<RecipeIngredient>(RecipeIngredients);
        }

        private void OnAddStepClicked(object sender, EventArgs e)
        {
            DirectionsEditor.TextChanged -= OnDirectionsEditorTextChanged;

            var text = DirectionsEditor.Text;
            int lastNumber = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => {
                    var parts = line.Trim().Split('.');
                    int.TryParse(parts.FirstOrDefault(), out int num);
                    return num;
                })
                .DefaultIfEmpty(0)
                .Max();

            if (!string.IsNullOrEmpty(text) && !text.EndsWith(Environment.NewLine))
            {
                DirectionsEditor.Text += Environment.NewLine;
            }
            DirectionsEditor.Text += $"{lastNumber + 1}. ";
            DirectionsEditor.Focus();

            DirectionsEditor.TextChanged += OnDirectionsEditorTextChanged;
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
            RecipeData.PhotoUrl = _uploadedPhotoUrl;

            double totalCost = 0;
            if (masterIngredientList != null)
            {
                foreach (var recipeIngredient in RecipeIngredients)
                {
                    var masterIngredient = masterIngredientList.FirstOrDefault(i => i.Id == recipeIngredient.IngredientId);
                    if (masterIngredient != null && masterIngredient.Unit != null)
                    {
                        try
                        {
                            totalCost += UnitConverter.Convert(recipeIngredient.Quantity, recipeIngredient.Unit ?? string.Empty, masterIngredient.CaseQuantity, masterIngredient.Unit, masterIngredient.CasePrice);
                        }
                        catch (ArgumentException ex)
                        {
                            await DisplayAlert("Conversion Error", $"Could not convert units for ingredient '{recipeIngredient.Name}': {ex.Message}", "OK");
                        }
                    }
                }
            }

            RecipeData.FoodCost = totalCost;
            if (yield > 0) RecipeData.Price = totalCost / yield;
            else RecipeData.Price = 0;

            if (SessionService.IsOffline)
            {
                // Pass the restaurantId when loading and saving
                var recipes = await LocalStorageService.LoadAsync<Recipe>(restaurantId);
                if (string.IsNullOrEmpty(RecipeData.Id))
                {
                    RecipeData.Id = Guid.NewGuid().ToString();
                    recipes.Add(RecipeData);
                }
                else
                {
                    var existing = recipes.FirstOrDefault(r => r.Id == RecipeData.Id);
                    if (existing != null)
                    {
                        recipes.Remove(existing);
                        recipes.Add(RecipeData);
                    }
                }
                await LocalStorageService.SaveAsync(recipes, restaurantId);
            }
            else
            {
                db = FirestoreService.Db;
                if (db == null || restaurantId == null) return;
                var collection = db.Collection("recipes");
                if (string.IsNullOrEmpty(RecipeData.Id)) await collection.AddAsync(RecipeData);
                else await collection.Document(RecipeData.Id).SetAsync(RecipeData);
            }

            await Navigation.PopAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}