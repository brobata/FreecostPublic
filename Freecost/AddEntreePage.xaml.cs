using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Plugin.Firebase.Firestore;

namespace Freecost
{
    public partial class AddEntreePage : ContentPage, INotifyPropertyChanged
    {
        private EntreeComponent? _selectedComponent;
        public EntreeComponent? SelectedComponent
        {
            get => _selectedComponent;
            set
            {
                _selectedComponent = value;
                OnPropertyChanged();
            }
        }
        public class AllergenSelection
        {
            public string Name { get; set; } = string.Empty;
            public bool IsSelected { get; set; }
        }

        public Entree EntreeData { get; private set; }
        public List<EntreeComponent> EntreeComponents { get; private set; }

        private string restaurantId;
        private List<IngredientCsvRecord> _masterIngredientList = new List<IngredientCsvRecord>();
        private List<Recipe> _masterRecipeList = new List<Recipe>();
        private List<string> topAllergens = new List<string> { "Milk", "Eggs", "Fish", "Shellfish", "Tree Nuts", "Peanuts", "Wheat", "Soy", "Vegan", "Vegetarian", "Halal", "Kosher" };
        private List<AllergenSelection> Allergens { get; set; }
        private string? _uploadedPhotoUrl;

        public AddEntreePage(string currentRestaurantId, Entree? entreeToEdit = null)
        {
            InitializeComponent();
            BindingContext = this;
            restaurantId = currentRestaurantId;

            EntreeData = entreeToEdit ?? new Entree();
            _uploadedPhotoUrl = EntreeData.PhotoUrl;
            EntreeComponents = entreeToEdit?.Components ?? new List<EntreeComponent>();

            Allergens = topAllergens.Select(a => new AllergenSelection { Name = a, IsSelected = EntreeData.Allergens?.Contains(a) ?? false }).ToList();

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

            LoadUnitDropdowns();
            DirectionsEditor.TextChanged += OnDirectionsEditorTextChanged;

            if (entreeToEdit == null)
            {
                DirectionsEditor.Text = "1. ";
            }
            else
            {
                if (entreeToEdit.Name != null) EntreeNameEntry.Text = entreeToEdit.Name;
                YieldEntry.Text = entreeToEdit.Yield.ToString();
                PlatePriceEntry.Text = entreeToEdit.PlatePrice.ToString();
                if (entreeToEdit.Directions != null) DirectionsEditor.Text = entreeToEdit.Directions;
                if (entreeToEdit.YieldUnit != null) YieldUnitPicker.SelectedItem = entreeToEdit.YieldUnit;
                else YieldUnitPicker.SelectedIndex = -1;
                PreviewImage.Source = entreeToEdit.PhotoUrl;
                RefreshComponentsGrid();
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAvailableComponents();
            await UpdateCostingSummary();
        }

        private void OnComponentSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedComponent = e.CurrentSelection.FirstOrDefault() as EntreeComponent;
        }

        private void OnComponentDoubleTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is EntreeComponent tappedComponent)
            {
                SelectedComponent = tappedComponent;
                OnEditComponentClicked(sender, e);
            }
        }

        private async void OnYieldChanged(object sender, EventArgs e)
        {
            await UpdateCostingSummary();
        }

        private async Task UpdateCostingSummary()
        {
            if (!_masterIngredientList.Any() && !_masterRecipeList.Any())
            {
                await LoadAvailableComponents();
            }

            double totalCost = 0;
            foreach (var entreeComponent in EntreeComponents)
            {
                var masterIngredient = _masterIngredientList.FirstOrDefault(i => i.Id == entreeComponent.ComponentId);
                if (masterIngredient != null)
                {
                    if (!string.IsNullOrEmpty(masterIngredient.Unit) && !string.IsNullOrEmpty(entreeComponent.Unit))
                    {
                        try
                        {
                            totalCost += UnitConverter.Convert(entreeComponent.Quantity, entreeComponent.Unit, masterIngredient.CaseQuantity, masterIngredient.Unit, masterIngredient.CasePrice);
                        }
                        catch (ArgumentException) { /* Ignore for live summary */ }
                    }
                }
                else
                {
                    var masterRecipe = _masterRecipeList.FirstOrDefault(r => r.Id == entreeComponent.ComponentId);
                    if (masterRecipe != null)
                    {
                        if (!string.IsNullOrEmpty(masterRecipe.YieldUnit) && !string.IsNullOrEmpty(entreeComponent.Unit))
                        {
                            try
                            {
                                totalCost += UnitConverter.Convert(entreeComponent.Quantity, entreeComponent.Unit, masterRecipe.Yield, masterRecipe.YieldUnit, masterRecipe.FoodCost);
                            }
                            catch (ArgumentException) { /* Ignore for live summary */ }
                        }
                    }
                }
            }


            TotalCostLabel.Text = totalCost.ToString("C");

            double.TryParse(PlatePriceEntry.Text, out double platePrice);
            double foodCostPercentage = (platePrice > 0) ? (totalCost / platePrice) : 0;
            FoodCostPercentageLabel.Text = foodCostPercentage.ToString("P2");
        }

        private void OnDirectionsEditorTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is Editor editor && editor.Text.Contains(Environment.NewLine))
            {
                editor.Text = editor.Text.Replace(Environment.NewLine, "");
                OnAddStepClicked(this, EventArgs.Empty);
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

        private void LoadUnitDropdowns()
        {
            var allUnits = UnitConverter.GetAllUnitNames();
            UnitPicker.ItemsSource = new List<string>(allUnits);
            DisplayUnitPicker.ItemsSource = new List<string>(allUnits);
            YieldUnitPicker.ItemsSource = new List<string>(allUnits);
        }
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                EntreeData ??= new Entree();
                double.TryParse(YieldEntry.Text, out double yield);
                if (yield == 0) yield = 1;
                double.TryParse(PlatePriceEntry.Text, out double platePrice);
                EntreeData.Name = EntreeNameEntry.Text;
                EntreeData.Yield = yield;
                EntreeData.PlatePrice = platePrice;
                EntreeData.YieldUnit = YieldUnitPicker.SelectedItem?.ToString() ?? "";
                EntreeData.Directions = DirectionsEditor.Text;
                EntreeData.Components = EntreeComponents;
                EntreeData.RestaurantId = restaurantId;
                EntreeData.Allergens = Allergens.Where(a => a.IsSelected).Select(a => a.Name).ToList();
                EntreeData.PhotoUrl = _uploadedPhotoUrl;

                double totalCost = 0;
                foreach (var entreeComponent in EntreeComponents)
                {
                    var masterIngredient = _masterIngredientList.FirstOrDefault(i => i.Id == entreeComponent.ComponentId);
                    if (masterIngredient != null)
                    {
                        if (!string.IsNullOrEmpty(masterIngredient.Unit) && !string.IsNullOrEmpty(entreeComponent.Unit))
                        {
                            totalCost += UnitConverter.Convert(entreeComponent.Quantity, entreeComponent.Unit, masterIngredient.CaseQuantity, masterIngredient.Unit, masterIngredient.CasePrice);
                        }
                    }
                    else
                    {
                        var masterRecipe = _masterRecipeList.FirstOrDefault(r => r.Id == entreeComponent.ComponentId);
                        if (masterRecipe != null)
                        {
                            if (!string.IsNullOrEmpty(masterRecipe.YieldUnit) && !string.IsNullOrEmpty(entreeComponent.Unit))
                            {
                                totalCost += UnitConverter.Convert(entreeComponent.Quantity, entreeComponent.Unit, masterRecipe.Yield, masterRecipe.YieldUnit, masterRecipe.FoodCost);
                            }
                        }
                    }
                }
                EntreeData.FoodCost = totalCost;
                EntreeData.Price = (platePrice > 0) ? totalCost / platePrice : 0;

                if (SessionService.IsOffline)
                {
                    var entrees = await LocalStorageService.LoadAsync<Entree>(restaurantId);
                    if (string.IsNullOrEmpty(EntreeData.Id))
                    {
                        EntreeData.Id = Guid.NewGuid().ToString();
                        entrees.Add(EntreeData);
                    }
                    else
                    {
                        var existing = entrees.FirstOrDefault(e => e.Id == EntreeData.Id);
                        if (existing != null)
                        {
                            var index = entrees.IndexOf(existing);
                            entrees[index] = EntreeData;
                        }
                        else
                        {
                            entrees.Add(EntreeData);
                        }
                    }
                    await LocalStorageService.SaveAsync(entrees, restaurantId);
                }
                else
                {
                    var collection = CrossFirebase.Current.Firestore.Collection("entrees");
                    if (string.IsNullOrEmpty(EntreeData.Id))
                    {
                        await collection.AddAsync(EntreeData);
                    }
                    else
                    {
                        await collection.Document(EntreeData.Id).SetAsync(EntreeData, SetOptions.Overwrite);
                    }
                }

                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An unexpected error occurred while saving: {ex.Message}", "OK");
            }
        }

        private async Task LoadAvailableComponents()
        {
            var currentRestaurantId = SessionService.CurrentRestaurant?.Id;
            if (string.IsNullOrEmpty(currentRestaurantId)) return;

            if (SessionService.IsOffline)
            {
                _masterIngredientList = await LocalStorageService.LoadAsync<IngredientCsvRecord>(currentRestaurantId);
                _masterRecipeList = await LocalStorageService.LoadAsync<Recipe>(currentRestaurantId);
            }
            else
            {
                var ingredientsTask = FirestoreService.GetIngredientsAsync(currentRestaurantId);
                var recipesTask = FirestoreService.GetRecipesAsync(currentRestaurantId);
                await Task.WhenAll(ingredientsTask, recipesTask);
                _masterIngredientList = ingredientsTask.Result;
                _masterRecipeList = recipesTask.Result;
            }

            var displayList = new List<EntreeComponentDisplay>();

            if (_masterIngredientList != null)
            {
                displayList.AddRange(_masterIngredientList.Select(ing => new EntreeComponentDisplay
                {
                    DisplayName = !string.IsNullOrEmpty(ing.AliasName) ? ing.AliasName : (ing.ItemName ?? string.Empty),
                    Id = ing.Id ?? string.Empty,
                    Unit = ing.Unit ?? string.Empty,
                    ItemType = "Ingredient"
                }));
            }

            if (_masterRecipeList != null)
            {
                displayList.AddRange(_masterRecipeList.Select(rec => new EntreeComponentDisplay
                {
                    DisplayName = rec.Name ?? string.Empty,
                    Id = rec.Id ?? string.Empty,
                    Unit = rec.YieldUnit ?? string.Empty,
                    ItemType = "Recipe"
                }));
            }

            ComponentPicker.ItemsSource = displayList.OrderBy(d => d.DisplayName).ToList();
            ComponentPicker.ItemDisplayBinding = new Binding("DisplayName");
        }


        private void OnComponentPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ComponentPicker.SelectedItem is EntreeComponentDisplay selectedComponentDisplay)
            {
                if (!string.IsNullOrEmpty(selectedComponentDisplay.Unit))
                {
                    string? category = UnitConverter.GetCategoryForUnit(selectedComponentDisplay.Unit);
                    if (category != null)
                    {
                        UnitPicker.ItemsSource = UnitConverter.GetUnitsForCategory(category);
                    }
                }
            }
        }

        private async void OnAddComponentClicked(object sender, EventArgs e)
        {
            if (ComponentPicker.SelectedItem == null) { return; }
            if (!double.TryParse(QuantityEntry.Text, out double quantity) || quantity <= 0) { return; }
            if (UnitPicker.SelectedItem == null)
            {
                await DisplayAlert("Unit Not Selected", "Please select a unit for costing.", "OK");
                return;
            }

            var selectedComponentDisplay = ComponentPicker.SelectedItem as EntreeComponentDisplay;
            if (selectedComponentDisplay == null) return;

            var componentToAdd = new EntreeComponent
            {
                ComponentId = selectedComponentDisplay.Id,
                Name = selectedComponentDisplay.DisplayName,
                Quantity = quantity,
                Unit = UnitPicker.SelectedItem.ToString(),
                DisplayQuantity = double.TryParse(DisplayQuantityEntry.Text, out double displayQuantity) ? displayQuantity : quantity,
                DisplayUnit = DisplayUnitPicker.SelectedItem?.ToString() ?? UnitPicker.SelectedItem.ToString()
            };
            EntreeComponents.Add(componentToAdd);
            await RefreshComponentsGrid();
        }

        private async void OnDeleteComponentClicked(object sender, EventArgs e)
        {
            if (SelectedComponent == null)
            {
                await DisplayAlert("No Selection", "Please select an component to delete.", "OK");
                return;
            }

            bool answer = await DisplayAlert("Confirm", $"Are you sure you want to delete {SelectedComponent.Name}?", "Yes", "No");
            if (answer)
            {
                EntreeComponents.Remove(SelectedComponent);
                await RefreshComponentsGrid();
            }
        }

        private async void OnEditComponentClicked(object sender, EventArgs e)
        {
            if (SelectedComponent == null)
            {
                await DisplayAlert("No Selection", "Please select a component to edit.", "OK");
                return;
            }
            var componentToEdit = SelectedComponent;
            var componentDisplay = ComponentPicker.ItemsSource.Cast<EntreeComponentDisplay>().FirstOrDefault(i => i.Id == componentToEdit.ComponentId);
            if (componentDisplay != null) ComponentPicker.SelectedItem = componentDisplay;
            QuantityEntry.Text = componentToEdit.Quantity.ToString();
            UnitPicker.SelectedItem = componentToEdit.Unit;
            DisplayQuantityEntry.Text = componentToEdit.DisplayQuantity.ToString();
            DisplayUnitPicker.SelectedItem = componentToEdit.DisplayUnit;
            EntreeComponents.Remove(componentToEdit);
            await RefreshComponentsGrid();
        }

        private async Task RefreshComponentsGrid()
        {
            ComponentsCollection.ItemsSource = null;
            ComponentsCollection.ItemsSource = new List<EntreeComponent>(EntreeComponents);
            await UpdateCostingSummary();
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

        // This helper class needs to be defined within the page's scope
        public class EntreeComponentDisplay
        {
            public string? DisplayName { get; set; }
            public string Id { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public string ItemType { get; set; } = string.Empty;
        }
    }
}