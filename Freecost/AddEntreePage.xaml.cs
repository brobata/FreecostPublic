using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Freecost;

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
        public string Name { get; set; }
        public bool IsSelected { get; set; }

        public AllergenSelection()
        {
            Name = string.Empty;
        }
    }

    public Entree EntreeData { get; private set; }
    public List<EntreeComponent> EntreeComponents { get; private set; }

    private string restaurantId;
    private FirestoreDb? db = FirestoreService.Db;
    private List<IngredientCsvRecord>? masterIngredientList;
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

        LoadMasterIngredients();
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

    private void OnComponentTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is EntreeComponent tappedComponent)
        {
            SelectedComponent = tappedComponent;
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
        YieldUnitPicker.ItemsSource = new List<string>(allUnits);
    }
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (EntreeData == null) EntreeData = new Entree();
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
        if (masterIngredientList != null)
        {
            foreach (var entreeComponent in EntreeComponents)
            {
                var masterIngredient = masterIngredientList.FirstOrDefault(i => i.Id == entreeComponent.ComponentId);
                if (masterIngredient != null && masterIngredient.Unit != null)
                {
                    try
                    {
                        totalCost += UnitConverter.Convert(entreeComponent.Quantity, entreeComponent.Unit ?? string.Empty, masterIngredient.CaseQuantity, masterIngredient.Unit, masterIngredient.CasePrice);
                    }
                    catch (ArgumentException ex)
                    {
                        await DisplayAlert("Conversion Error", $"Could not convert units for component '{entreeComponent.Name}': {ex.Message}", "OK");
                    }
                }
            }
        }
        EntreeData.FoodCost = totalCost;
        if (platePrice > 0) EntreeData.Price = totalCost / platePrice;
        else EntreeData.Price = 0;

        db = FirestoreService.Db;
        if (db == null || restaurantId == null) return;
        var collection = db.Collection("entrees");
        if (string.IsNullOrEmpty(EntreeData.Id)) await collection.AddAsync(EntreeData);
        else await collection.Document(EntreeData.Id).SetAsync(EntreeData);
        await Navigation.PopAsync();
    }

    private async void LoadMasterIngredients()
    {
        db = FirestoreService.Db;
        if (db == null || restaurantId == null) return;
        var ingredientsCollection = db.Collection("restaurants").Document(restaurantId).Collection("ingredients");
        var snapshot = await ingredientsCollection.GetSnapshotAsync();
        masterIngredientList = snapshot.Documents.Select(doc => {
            var ing = doc.ConvertTo<IngredientCsvRecord>();
            ing.Id = doc.Id;
            return ing;
        }).ToList();
        if (masterIngredientList == null) return;
        var displayList = masterIngredientList.Select(ing => new EntreeComponentDisplay
        {
            DisplayName = !string.IsNullOrEmpty(ing.AliasName) ? ing.AliasName : ing.ItemName ?? string.Empty,
            OriginalIngredient = ing
        }).OrderBy(d => d.DisplayName).ToList();
        ComponentPicker.ItemsSource = displayList;
        ComponentPicker.ItemDisplayBinding = new Binding("DisplayName");
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
                    UnitPicker.ItemsSource = UnitConverter.GetUnitsForCategory(category) ?? new List<string>();
                }
            }
        }
    }

    private void OnAddComponentClicked(object sender, EventArgs e)
    {
        if (ComponentPicker.SelectedItem == null) { return; }
        if (!double.TryParse(QuantityEntry.Text, out double quantity) || quantity <= 0) { return; }
        var selectedComponentDisplay = ComponentPicker.SelectedItem as EntreeComponentDisplay;
        if (selectedComponentDisplay?.OriginalIngredient == null) return;
        var selectedMasterIngredient = selectedComponentDisplay.OriginalIngredient;
        var componentToAdd = new EntreeComponent
        {
            ComponentId = selectedMasterIngredient.Id,
            Name = !string.IsNullOrEmpty(selectedMasterIngredient.AliasName) ? selectedMasterIngredient.AliasName : selectedMasterIngredient.ItemName ?? string.Empty,
            Quantity = quantity,
            Unit = UnitPicker.SelectedItem?.ToString() ?? string.Empty
        };
        EntreeComponents.Add(componentToAdd);
        RefreshComponentsGrid();
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
            RefreshComponentsGrid();
        }
    }

    private void OnEditComponentClicked(object sender, EventArgs e)
    {
        if (SelectedComponent == null)
        {
            DisplayAlert("No Selection", "Please select a component to edit.", "OK");
            return;
        }
        var componentToEdit = SelectedComponent;
        var componentDisplay = ComponentPicker.ItemsSource.Cast<EntreeComponentDisplay>().FirstOrDefault(i => i.OriginalIngredient?.Id == componentToEdit.ComponentId);
        if (componentDisplay != null) ComponentPicker.SelectedItem = componentDisplay;
        QuantityEntry.Text = componentToEdit.Quantity.ToString();
        UnitPicker.SelectedItem = componentToEdit.Unit;
        EntreeComponents.Remove(componentToEdit);
        RefreshComponentsGrid();
    }

    private void RefreshComponentsGrid()
    {
        ComponentsCollection.ItemsSource = null;
        ComponentsCollection.ItemsSource = new List<EntreeComponent>(EntreeComponents);
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