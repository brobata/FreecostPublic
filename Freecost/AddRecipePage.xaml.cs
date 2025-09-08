using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Freecost
{
    public partial class AddRecipePage : ContentPage, INotifyPropertyChanged
    {
        private RecipeIngredient? _selectedIngredient;
        public RecipeIngredient? SelectedIngredient
        {
            get => _selectedIngredient;
            set { _selectedIngredient = value; OnPropertyChanged(); }
        }

        public class AllergenSelection
        {
            public string Name { get; set; } = string.Empty;
            public bool IsSelected { get; set; }
        }

        public Recipe RecipeData { get; private set; }
        public List<RecipeIngredient> RecipeIngredients { get; private set; }

        private readonly string _restaurantId;
        private List<IngredientCsvRecord>? _masterIngredientList;
        private List<AllergenSelection> _allergens { get; set; }
        private string? _uploadedPhotoUrl;

        public AddRecipePage(string currentRestaurantId, Recipe? recipeToEdit = null)
        {
            InitializeComponent();
            BindingContext = this;
            _restaurantId = currentRestaurantId;

            RecipeData = recipeToEdit ?? new Recipe();
            _uploadedPhotoUrl = RecipeData.PhotoUrl;
            RecipeIngredients = recipeToEdit?.Ingredients ?? new List<RecipeIngredient>();

            var topAllergens = new List<string> { "Milk", "Eggs", "Fish", "Shellfish", "Tree Nuts", "Peanuts", "Wheat", "Soy", "Vegan", "Vegetarian", "Halal", "Kosher" };
            _allergens = topAllergens.Select(a => new AllergenSelection { Name = a, IsSelected = RecipeData.Allergens?.Contains(a) ?? false }).ToList();

            foreach (var allergen in _allergens)
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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadMasterIngredients();
        }

        private async void OnYieldChanged(object sender, EventArgs e)
        {
            await UpdateCostingSummary();
        }

        private void OnIngredientSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedIngredient = e.CurrentSelection.FirstOrDefault() as RecipeIngredient;
        }

        private void OnIngredientDoubleTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is RecipeIngredient tappedIngredient)
            {
                SelectedIngredient = tappedIngredient;
                OnEditIngredientClicked(sender, e);
            }
        }

        private async Task LoadMasterIngredients()
        {
            if (SessionService.IsOffline)
            {
                _masterIngredientList = await LocalStorageService.LoadAsync<IngredientCsvRecord>(_restaurantId);
            }
            else
            {
                if (string.IsNullOrEmpty(_restaurantId)) return;
                _masterIngredientList = await FirestoreService.GetCollectionAsync<IngredientCsvRecord>(
                    $"restaurants/{_restaurantId}/ingredients", SessionService.AuthToken);
            }

            if (_masterIngredientList == null) return;

            var displayList = _masterIngredientList.Select(ing => new IngredientDisplay
            {
                DisplayName = !string.IsNullOrEmpty(ing.AliasName) ? ing.AliasName : ing.ItemName,
                OriginalIngredient = ing
            }).OrderBy(d => d.DisplayName).ToList();

            IngredientPicker.ItemsSource = displayList;
            IngredientPicker.ItemDisplayBinding = new Binding("DisplayName");

            await UpdateCostingSummary();
        }

        private Task UpdateCostingSummary()
        {
            if (_masterIngredientList == null)
            {
                return Task.CompletedTask;
            }

            double totalCost = 0;
            foreach (var recipeIngredient in RecipeIngredients)
            {
                var masterIngredient = _masterIngredientList.FirstOrDefault(i => i.Id == recipeIngredient.IngredientId);
                if (masterIngredient != null && !string.IsNullOrEmpty(masterIngredient.Unit) && !string.IsNullOrEmpty(recipeIngredient.Unit))
                {
                    try
                    {
                        totalCost += UnitConverter.Convert(recipeIngredient.Quantity, recipeIngredient.Unit, masterIngredient.CaseQuantity, masterIngredient.Unit, masterIngredient.CasePrice);
                    }
                    catch (ArgumentException) { /* Inconsistent data, ignore for summary */ }
                }
            }

            TotalCostLabel.Text = totalCost.ToString("C");

            double.TryParse(YieldEntry.Text, out double yield);
            if (yield == 0) yield = 1;

            double costPerUnit = (yield > 0) ? totalCost / yield : 0;
            CostPerUnitLabel.Text = $"{costPerUnit:C} per {YieldUnitPicker.SelectedItem ?? "unit"}";
            return Task.CompletedTask;
        }

        private async void OnUploadImageClicked(object sender, EventArgs e)
        {
            await AuthService.RefreshAuthTokenIfNeededAsync();

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

        private async void OnAddIngredientClicked(object sender, EventArgs e)
        {
            if (IngredientPicker.SelectedItem == null)
            {
                await DisplayAlert("No Ingredient Selected", "Please select an ingredient from the dropdown list first.", "OK");
                return;
            }
            if (!double.TryParse(QuantityEntry.Text, out double quantity) || quantity <= 0)
            {
                await DisplayAlert("Invalid Quantity", "Please enter a valid, positive number for the quantity.", "OK");
                return;
            }
            if (UnitPicker.SelectedItem == null)
            {
                await DisplayAlert("Unit Not Selected", "Please select a unit for costing.", "OK");
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
                Unit = UnitPicker.SelectedItem.ToString(),
                DisplayQuantity = double.TryParse(DisplayQuantityEntry.Text, out double displayQuantity) ? displayQuantity : quantity,
                DisplayUnit = DisplayUnitPicker.SelectedItem?.ToString() ?? UnitPicker.SelectedItem.ToString()
            };

            RecipeIngredients.Add(ingredientToAdd);
            RefreshRecipeGrid();
            await UpdateCostingSummary();
            QuantityEntry.Text = string.Empty;
            DisplayQuantityEntry.Text = string.Empty;
            UnitPicker.SelectedIndex = -1;
            DisplayUnitPicker.SelectedIndex = -1;
            IngredientPicker.Focus();
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                double.TryParse(YieldEntry.Text, out double yield);
                if (yield == 0) yield = 1;

                RecipeData.Name = RecipeNameEntry.Text;
                RecipeData.Yield = yield;
                RecipeData.YieldUnit = YieldUnitPicker.SelectedItem?.ToString() ?? "";
                RecipeData.Directions = DirectionsEditor.Text;
                RecipeData.Ingredients = RecipeIngredients;
                RecipeData.RestaurantId = _restaurantId;
                RecipeData.Allergens = _allergens.Where(a => a.IsSelected).Select(a => a.Name).ToList();
                RecipeData.PhotoUrl = _uploadedPhotoUrl;

                await UpdateCostingSummary();
                double.TryParse(TotalCostLabel.Text, System.Globalization.NumberStyles.Currency, System.Globalization.CultureInfo.CurrentCulture, out double totalCost);
                RecipeData.FoodCost = totalCost;
                RecipeData.Price = (yield > 0) ? totalCost / yield : 0;

                if (SessionService.IsOffline)
                {
                    var recipes = await LocalStorageService.LoadAsync<Recipe>(_restaurantId);
                    if (string.IsNullOrEmpty(RecipeData.Id))
                    {
                        RecipeData.Id = Guid.NewGuid().ToString();
                        recipes.Add(RecipeData);
                    }
                    else
                    {
                        var existing = recipes.FirstOrDefault(r => r.Id == RecipeData.Id);
                        if (existing != null) recipes[recipes.IndexOf(existing)] = RecipeData;
                        else recipes.Add(RecipeData);
                    }
                    await LocalStorageService.SaveAsync(recipes, _restaurantId);
                }
                else
                {
                    if (string.IsNullOrEmpty(RecipeData.Id))
                    {
                        await FirestoreService.AddDocumentAsync("recipes", RecipeData, SessionService.AuthToken);
                    }
                    else
                    {
                        await FirestoreService.SetDocumentAsync($"recipes/{RecipeData.Id}", RecipeData, SessionService.AuthToken);
                    }
                }

                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An unexpected error occurred while saving: {ex.Message}", "OK");
            }
        }

        private async void OnDeleteIngredientClicked(object sender, EventArgs e)
        {
            if (SelectedIngredient == null) return;

            bool answer = await DisplayAlert("Confirm", $"Are you sure you want to delete {SelectedIngredient.Name}?", "Yes", "No");
            if (answer)
            {
                RecipeIngredients.Remove(SelectedIngredient);
                RefreshRecipeGrid();
                await UpdateCostingSummary();
            }
        }

        private async void OnEditIngredientClicked(object sender, EventArgs e)
        {
            if (SelectedIngredient == null)
            {
                await DisplayAlert("No Selection", "Please select an ingredient to edit.", "OK");
                return;
            }
            var ingredientToEdit = SelectedIngredient;
            var ingredientDisplay = IngredientPicker.ItemsSource.Cast<IngredientDisplay>().FirstOrDefault(i => i.OriginalIngredient?.Id == ingredientToEdit.IngredientId);
            if (ingredientDisplay != null) IngredientPicker.SelectedItem = ingredientDisplay;
            QuantityEntry.Text = ingredientToEdit.Quantity.ToString();
            UnitPicker.SelectedItem = ingredientToEdit.Unit;
            DisplayQuantityEntry.Text = ingredientToEdit.DisplayQuantity.ToString();
            DisplayUnitPicker.SelectedItem = ingredientToEdit.DisplayUnit;
            RecipeIngredients.Remove(ingredientToEdit);
            RefreshRecipeGrid();
        }

        private void OnDirectionsEditorTextChanged(object? sender, TextChangedEventArgs e) { /* ... */ }
        private void OnIngredientPicker_SelectedIndexChanged(object sender, EventArgs e) { /* ... */ }
        private void LoadUnitDropdowns() { /* ... */ }

        private void RefreshRecipeGrid()
        {
            IngredientsCollection.ItemsSource = null;
            IngredientsCollection.ItemsSource = new List<RecipeIngredient>(RecipeIngredients);
        }

        private void OnAddStepClicked(object sender, EventArgs e) { /* ... */ }

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