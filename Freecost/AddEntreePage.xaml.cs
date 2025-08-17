using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Freecost;

public partial class AddEntreePage : ContentPage
{
    public Entree EntreeData { get; private set; }
    public List<EntreeComponent> EntreeComponents { get; private set; }

    private string restaurantId;
    private FirestoreDb? db = FirestoreService.Db;
    private List<IngredientCsvRecord>? masterIngredientList;
    private List<string> topAllergens = new List<string> { "Milk", "Eggs", "Fish", "Shellfish", "Tree Nuts", "Peanuts", "Wheat", "Soy", "Vegan", "Vegetarian", "Halal", "Kosher" };

    public AddEntreePage(string currentRestaurantId, Entree? entreeToEdit = null)
    {
        InitializeComponent();
        restaurantId = currentRestaurantId;

        EntreeData = entreeToEdit ?? new Entree();
        EntreeComponents = entreeToEdit?.Components ?? new List<EntreeComponent>();

        //AllergensCollection.ItemsSource = topAllergens;
        LoadMasterIngredients();
        LoadUnitDropdowns();

        if (entreeToEdit == null) // This is for ADDING a new entree
        {
            DirectionsEditor.Text = "1. ";
        }
        else // This is for EDITING an existing entree
        {
            if (entreeToEdit.Name != null)
            {
                EntreeNameEntry.Text = entreeToEdit.Name;
            }
            YieldEntry.Text = entreeToEdit.Yield.ToString();
            if (entreeToEdit.Directions != null)
            {
                DirectionsEditor.Text = entreeToEdit.Directions;
            }
            if (entreeToEdit.YieldUnit != null)
            {
                YieldUnitPicker.SelectedItem = entreeToEdit.YieldUnit;
            }
            else
            {
                YieldUnitPicker.SelectedIndex = -1;
            }
            RefreshComponentsGrid();
        }
    }
    private void OnAddStepClicked(object sender, EventArgs e)
    {
        // Figure out what the next step number should be
        int lastNumber = DirectionsEditor.Text.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => int.TryParse(line.Split('.').FirstOrDefault(), out int num) ? num : 0)
            .DefaultIfEmpty(0)
            .Max();

        // Add the new numbered line and move the cursor
        DirectionsEditor.Text += $"\n{lastNumber + 1}. ";
        DirectionsEditor.Focus();
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

        EntreeData.Name = EntreeNameEntry.Text;
        EntreeData.Yield = yield;
        EntreeData.YieldUnit = YieldUnitPicker.SelectedItem?.ToString() ?? "";
        EntreeData.Directions = DirectionsEditor.Text;
        EntreeData.Components = EntreeComponents;
        EntreeData.RestaurantId = restaurantId;
        //EntreeData.Allergens = allergensCheckedListBox.CheckedItems.Cast<string>().ToList();

        db = FirestoreService.Db;
        if (db == null || restaurantId == null) return;

        var collection = db.Collection("entrees");

        if (string.IsNullOrEmpty(EntreeData.Id))
        {
            await collection.AddAsync(EntreeData);
        }
        else
        {
            await collection.Document(EntreeData.Id).SetAsync(EntreeData);
        }

        await Navigation.PopAsync();
    }

    private async void LoadMasterIngredients()
    {
        db = FirestoreService.Db;
        if (db == null || restaurantId == null) return;
        var ingredientsCollection = db.Collection("restaurants").Document(restaurantId).Collection("ingredients");
        var snapshot = await ingredientsCollection.GetSnapshotAsync();
        masterIngredientList = snapshot.Documents.Select(doc => doc.ConvertTo<IngredientCsvRecord>()).ToList();
        if (masterIngredientList == null) return;
        var displayList = masterIngredientList.Select(ing => new EntreeComponentDisplay {
            DisplayName = !string.IsNullOrEmpty(ing.AliasName) ? ing.AliasName : ing.ItemName ?? string.Empty,
            OriginalIngredient = ing
        }).ToList();
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

    private void OnDeleteComponentClicked(object sender, EventArgs e)
    {
        var swipeItem = sender as SwipeItem;
        if (swipeItem?.CommandParameter is EntreeComponent componentToDelete)
        {
            EntreeComponents.Remove(componentToDelete);
            RefreshComponentsGrid();
        }
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
}
