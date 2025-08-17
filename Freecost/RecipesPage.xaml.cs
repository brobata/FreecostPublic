using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Freecost;

public partial class RecipesPage : ContentPage
{
    private FirestoreDb? db;
    private string? restaurantId;
    private RecipeDisplayRecord? _selectedRecipe;
    private RecipeDisplayRecord? _previousSelectedRecipe;
    private string _currentSortColumn = "Name";
    private bool _isSortAscending = true;
    private const double TargetFoodCostPercentage = 0.30;

	public RecipesPage()
	{
		InitializeComponent();
        SessionService.OnRestaurantChanged += (s, e) => LoadData();
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadData();
    }

    private void LoadData()
    {
        restaurantId = SessionService.CurrentRestaurant?.Id;
        Task.Run(async () => await LoadRecipes());
    }

    private async Task LoadRecipes()
    {
        db = FirestoreService.Db;
        if (db == null || restaurantId == null) return;

        var recipes = new List<RecipeDisplayRecord>();
        var query = db.Collection("recipes").WhereEqualTo("RestaurantId", restaurantId);
        var snapshot = await query.GetSnapshotAsync();
        var documents = snapshot.Documents.ToList();

        for (int i = 0; i < documents.Count; i++)
        {
            var document = documents[i];
            var recipe = document.ConvertTo<Recipe>();
            var displayRecord = new RecipeDisplayRecord
            {
                Id = document.Id,
                Name = recipe.Name,
                Yield = recipe.Yield,
                YieldUnit = recipe.YieldUnit,
                Directions = recipe.Directions,
                PhotoUrl = recipe.PhotoUrl,
                RestaurantId = recipe.RestaurantId,
                Allergens = recipe.Allergens,
                Ingredients = recipe.Ingredients,
                FoodCost = recipe.FoodCost,
                Price = recipe.Price
            };
            recipes.Add(displayRecord);
        }
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RecipesListView.ItemsSource = recipes;
        });
    }

	private async void OnAddRecipeClicked(object sender, EventArgs e)
	{
        if (restaurantId == null) return;
		await Navigation.PushAsync(new AddRecipePage(restaurantId));
	}

    private async void OnEditRecipeClicked(object sender, EventArgs e)
    {
        if (_selectedRecipe == null)
        {
            await DisplayAlert("No Recipe Selected", "Please select a recipe to edit.", "OK");
            return;
        }
        if (restaurantId == null) return;
        await Navigation.PushAsync(new AddRecipePage(restaurantId, _selectedRecipe));
    }

    private async void OnDeleteRecipeClicked(object sender, EventArgs e)
    {
        if (_selectedRecipe == null)
        {
            await DisplayAlert("No Recipe Selected", "Please select a recipe to delete.", "OK");
            return;
        }

        bool answer = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete {_selectedRecipe.Name}?", "Yes", "No");
        if (answer)
        {
            if (db == null || restaurantId == null) return;
            await db.Collection("recipes").Document(_selectedRecipe.Id).DeleteAsync();
            LoadData();
        }
    }

    private void OnRecipeSelected(object sender, SelectedItemChangedEventArgs e)
    {
        if (e.SelectedItem == null) return;

        _selectedRecipe = e.SelectedItem as RecipeDisplayRecord;

        if (_selectedRecipe != null)
        {
            _previousSelectedRecipe = _selectedRecipe;

            RecipeDetailsView.IsVisible = true;
            SelectRecipeLabel.IsVisible = false;

            RecipeImage.Source = _selectedRecipe.PhotoUrl;
            if (_selectedRecipe.Ingredients != null)
            {
                IngredientsLabel.Text = string.Join(Environment.NewLine, _selectedRecipe.Ingredients.Select(i => $"{i.Name}: {i.Quantity} {i.Unit}"));
            }
            else
            {
                IngredientsLabel.Text = string.Empty;
            }

            if (_selectedRecipe.Allergens != null)
            {
                AllergensLabel.Text = string.Join(", ", _selectedRecipe.Allergens);
            }
            else
            {
                AllergensLabel.Text = string.Empty;
            }

            DirectionsLabel.Text = _selectedRecipe.Directions;
        }
        else
        {
            RecipeDetailsView.IsVisible = false;
            SelectRecipeLabel.IsVisible = true;
        }

    }

    private void OnSortClicked(object sender, TappedEventArgs e)
    {
        var newSortColumn = e.Parameter as string;
        if (string.IsNullOrEmpty(newSortColumn)) return;

        if (_currentSortColumn == newSortColumn)
        {
            _isSortAscending = !_isSortAscending;
        }
        else
        {
            _currentSortColumn = newSortColumn;
            _isSortAscending = true;
        }

        if (RecipesListView.ItemsSource is not List<RecipeDisplayRecord> recipes) return;

        var sortedRecipes = _isSortAscending
            ? recipes.OrderBy(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList()
            : recipes.OrderByDescending(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList();

        RecipesListView.ItemsSource = sortedRecipes;
    }
}
