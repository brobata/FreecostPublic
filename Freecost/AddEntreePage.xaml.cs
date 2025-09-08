using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Freecost
{
    public partial class AddEntreePage : ContentPage, INotifyPropertyChanged
    {
        private EntreeComponent? _selectedComponent;
        public EntreeComponent? SelectedComponent
        {
            get => _selectedComponent;
            set { _selectedComponent = value; OnPropertyChanged(); }
        }

        public class AllergenSelection
        {
            public string Name { get; set; } = string.Empty;
            public bool IsSelected { get; set; }
        }

        public Entree EntreeData { get; private set; }
        public List<EntreeComponent> EntreeComponents { get; private set; }

        private readonly string _restaurantId;
        private List<IngredientCsvRecord>? _masterIngredientList;
        private List<AllergenSelection> _allergens;
        private string? _uploadedPhotoUrl;

        public AddEntreePage(string currentRestaurantId, Entree? entreeToEdit = null)
        {
            InitializeComponent();
            BindingContext = this;
            _restaurantId = currentRestaurantId;

            EntreeData = entreeToEdit ?? new Entree();
            _uploadedPhotoUrl = EntreeData.PhotoUrl;
            EntreeComponents = entreeToEdit?.Components ?? new List<EntreeComponent>();

            var topAllergens = new List<string> { "Milk", "Eggs", "Fish", "Shellfish", "Tree Nuts", "Peanuts", "Wheat", "Soy", "Vegan", "Vegetarian", "Halal", "Kosher" };
            _allergens = topAllergens.Select(a => new AllergenSelection { Name = a, IsSelected = EntreeData.Allergens?.Contains(a) ?? false }).ToList();

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
            await LoadMasterIngredients();
        }

        private async void OnYieldChanged(object sender, EventArgs e)
        {
            await UpdateCostingSummary();
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
            var displayList = _masterIngredientList.Select(ing => new EntreeComponentDisplay
            {
                DisplayName = !string.IsNullOrEmpty(ing.AliasName) ? ing.AliasName : ing.ItemName ?? string.Empty,
                OriginalIngredient = ing
            }).OrderBy(d => d.DisplayName).ToList();
            ComponentPicker.ItemsSource = displayList;
            ComponentPicker.ItemDisplayBinding = new Binding("DisplayName");
        }

        private async Task UpdateCostingSummary()
        {
            if (_masterIngredientList == null)
            {
                await LoadMasterIngredients();
            }

            double totalCost = 0;
            if (_masterIngredientList != null)
            {
                foreach (var entreeComponent in EntreeComponents)
                {
                    var masterIngredient = _masterIngredientList.FirstOrDefault(i => i.Id == entreeComponent.ComponentId);
                    if (masterIngredient != null && !string.IsNullOrEmpty(masterIngredient.Unit) && !string.IsNullOrEmpty(entreeComponent.Unit))
                    {
                        try
                        {
                            totalCost += UnitConverter.Convert(entreeComponent.Quantity, entreeComponent.Unit, masterIngredient.CaseQuantity, masterIngredient.Unit, masterIngredient.CasePrice);
                        }
                        catch (ArgumentException) { /* Ignore for live summary */ }
                    }
                }
            }

            TotalCostLabel.Text = totalCost.ToString("C");
            EntreeData.FoodCost = totalCost;

            double.TryParse(PlatePriceEntry.Text, out double platePrice);
            double foodCostPercentage = (platePrice > 0) ? (totalCost / platePrice) : 0;
            FoodCostPercentageLabel.Text = foodCostPercentage.ToString("P2");
            EntreeData.Price = foodCostPercentage;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                double.TryParse(YieldEntry.Text, out double yield);
                if (yield == 0) yield = 1;
                double.TryParse(PlatePriceEntry.Text, out double platePrice);

                EntreeData.Name = EntreeNameEntry.Text;
                EntreeData.Yield = yield;
                EntreeData.PlatePrice = platePrice;
                EntreeData.YieldUnit = YieldUnitPicker.SelectedItem?.ToString() ?? "";
                EntreeData.Directions = DirectionsEditor.Text;
                EntreeData.Components = EntreeComponents;
                EntreeData.RestaurantId = _restaurantId;
                EntreeData.Allergens = _allergens.Where(a => a.IsSelected).Select(a => a.Name).ToList();
                EntreeData.PhotoUrl = _uploadedPhotoUrl;

                await UpdateCostingSummary();

                if (SessionService.IsOffline)
                {
                    var entrees = await LocalStorageService.LoadAsync<Entree>(_restaurantId);
                    if (string.IsNullOrEmpty(EntreeData.Id))
                    {
                        EntreeData.Id = Guid.NewGuid().ToString();
                        entrees.Add(EntreeData);
                    }
                    else
                    {
                        var existing = entrees.FirstOrDefault(e => e.Id == EntreeData.Id);
                        if (existing != null) entrees[entrees.IndexOf(existing)] = EntreeData;
                        else entrees.Add(EntreeData);
                    }
                    await LocalStorageService.SaveAsync(entrees, _restaurantId);
                }
                else
                {
                    if (string.IsNullOrEmpty(EntreeData.Id))
                    {
                        await FirestoreService.AddDocumentAsync("entrees", EntreeData, SessionService.AuthToken);
                    }
                    else
                    {
                        await FirestoreService.SetDocumentAsync($"entrees/{EntreeData.Id}", EntreeData, SessionService.AuthToken);
                    }
                }

                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An unexpected error occurred while saving: {ex.Message}", "OK");
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
            if (selectedComponentDisplay?.OriginalIngredient == null) return;
            var selectedMasterIngredient = selectedComponentDisplay.OriginalIngredient;
            var componentToAdd = new EntreeComponent
            {
                ComponentId = selectedMasterIngredient.Id,
                Name = !string.IsNullOrEmpty(selectedMasterIngredient.AliasName) ? selectedMasterIngredient.AliasName : selectedMasterIngredient.ItemName ?? string.Empty,
                Quantity = quantity,
                Unit = UnitPicker.SelectedItem.ToString(),
                DisplayQuantity = double.TryParse(DisplayQuantityEntry.Text, out double displayQuantity) ? displayQuantity : quantity,
                DisplayUnit = DisplayUnitPicker.SelectedItem?.ToString() ?? UnitPicker.SelectedItem.ToString()
            };
            EntreeComponents.Add(componentToAdd);
            RefreshComponentsGrid();
            await UpdateCostingSummary();
        }

        private async void OnDeleteComponentClicked(object sender, EventArgs e)
        {
            if (SelectedComponent == null)
            {
                await DisplayAlert("No Selection", "Please select a component to delete.", "OK");
                return;
            }

            bool answer = await DisplayAlert("Confirm", $"Are you sure you want to delete {SelectedComponent.Name}?", "Yes", "No");
            if (answer)
            {
                EntreeComponents.Remove(SelectedComponent);
                RefreshComponentsGrid();
                await UpdateCostingSummary();
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
            var componentDisplay = ComponentPicker.ItemsSource.Cast<EntreeComponentDisplay>().FirstOrDefault(i => i.OriginalIngredient?.Id == componentToEdit.ComponentId);
            if (componentDisplay != null) ComponentPicker.SelectedItem = componentDisplay;
            QuantityEntry.Text = componentToEdit.Quantity.ToString();
            UnitPicker.SelectedItem = componentToEdit.Unit;
            DisplayQuantityEntry.Text = componentToEdit.DisplayQuantity.ToString();
            DisplayUnitPicker.SelectedItem = componentToEdit.DisplayUnit;
            EntreeComponents.Remove(componentToEdit);
            RefreshComponentsGrid();
        }

        private void RefreshComponentsGrid()
        {
            ComponentsCollection.ItemsSource = null;
            ComponentsCollection.ItemsSource = new List<EntreeComponent>(EntreeComponents);
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

        private void OnDirectionsEditorTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is Editor editor && editor.Text.Contains(Environment.NewLine))
            {
                editor.Text = editor.Text.Replace(Environment.NewLine, "");
                OnAddStepClicked(this, EventArgs.Empty);
            }
        }

        private void LoadUnitDropdowns()
        {
            var allUnits = UnitConverter.GetAllUnitNames();
            UnitPicker.ItemsSource = new List<string>(allUnits);
            DisplayUnitPicker.ItemsSource = new List<string>(allUnits);
            YieldUnitPicker.ItemsSource = new List<string>(allUnits);
        }

        private void OnComponentPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ComponentPicker.SelectedItem is EntreeComponentDisplay selectedComponentDisplay)
            {
                var selectedMasterIngredient = selectedComponentDisplay.OriginalIngredient;
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