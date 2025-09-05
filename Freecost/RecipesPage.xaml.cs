using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Freecost
{
    public partial class RecipesPage : ContentPage
    {
        private FirestoreDb? db;
        private string? restaurantId;
        private RecipeDisplayRecord? _selectedRecipe;
        private string _currentSortColumn = "Name";
        private bool _isSortAscending = true;

        public RecipesPage()
        {
            InitializeComponent();
            SessionService.StaticPropertyChanged += OnSessionChanged;
#if ANDROID
            Grid.SetColumn(DetailPanel, 0);
            ListPanel.IsVisible = true;
            DetailPanel.IsVisible = false;
#endif
        }

        private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionService.CurrentRestaurant))
            {
                LoadData();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadData();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            SessionService.StaticPropertyChanged -= OnSessionChanged;
        }

        private void LoadData()
        {
            restaurantId = SessionService.CurrentRestaurant?.Id;
            Task.Run(async () => await LoadRecipes());
        }

        private async Task LoadRecipes()
        {
            restaurantId = SessionService.CurrentRestaurant?.Id;
            if (restaurantId == null) return;

            List<RecipeDisplayRecord> recipes;
            if (SessionService.IsOffline)
            {
                var recipeData = await LocalStorageService.LoadAsync<Recipe>(restaurantId);
                recipes = recipeData.Select(r => new RecipeDisplayRecord
                {
                    Id = r.Id,
                    Name = r.Name,
                    Yield = r.Yield,
                    YieldUnit = r.YieldUnit,
                    Directions = r.Directions,
                    PhotoUrl = r.PhotoUrl,
                    RestaurantId = r.RestaurantId,
                    Allergens = r.Allergens,
                    Ingredients = r.Ingredients,
                    FoodCost = r.FoodCost,
                    Price = r.Price
                }).ToList();
            }
            else
            {
                db = FirestoreService.Db;
                if (db == null) return;

                recipes = new List<RecipeDisplayRecord>();
                var query = db.Collection("recipes").WhereEqualTo("RestaurantId", restaurantId);
                var snapshot = await query.GetSnapshotAsync();

                recipes = snapshot.Documents.Select(document =>
                {
                    var recipe = document.ConvertTo<Recipe>();
                    return new RecipeDisplayRecord
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
                }).ToList();
                await LocalStorageService.SaveAsync(recipes.Cast<Recipe>().ToList(), restaurantId);
            }

            var sortedRecipes = _isSortAscending
                ? recipes.OrderBy(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList()
                : recipes.OrderByDescending(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                RecipesListView.ItemsSource = sortedRecipes;
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
                if (db == null || restaurantId == null || _selectedRecipe.Id == null) return;
                await db.Collection("recipes").Document(_selectedRecipe.Id).DeleteAsync();
                LoadData();
            }
        }

        private void OnRecipeSelected(object sender, SelectedItemChangedEventArgs e)
        {
            // First, un-select the previously selected item if it exists
            if (_selectedRecipe != null)
            {
                _selectedRecipe.IsSelected = false;
            }

            // Get the newly selected item
            _selectedRecipe = e.SelectedItem as RecipeDisplayRecord;

            if (_selectedRecipe != null)
            {
                // Set the IsSelected property to true to trigger the highlight
                _selectedRecipe.IsSelected = true;

                // The rest of your logic to display details
                RecipeNameLabel.Text = _selectedRecipe.Name;
                RecipeImage.Source = _selectedRecipe.PhotoUrl;

                if (_selectedRecipe.Allergens != null && _selectedRecipe.Allergens.Any())
                {
                    AllergensLabel.Text = " " + string.Join(", ", _selectedRecipe.Allergens);
                }
                else
                {
                    AllergensLabel.Text = " None listed.";
                }

                IngredientsListView.ItemsSource = _selectedRecipe.Ingredients;
                DirectionsLabel.Text = _selectedRecipe.Directions ?? "No directions provided.";

#if ANDROID
                ListPanel.IsVisible = false;
                DetailPanel.IsVisible = true;
                RecipeDetailsView.IsVisible = true;
                SelectRecipeView.IsVisible = false;
#else
                RecipeDetailsView.IsVisible = true;
                SelectRecipeView.IsVisible = false;
#endif
            }
            else
            {
#if !ANDROID
                RecipeDetailsView.IsVisible = false;
                SelectRecipeView.IsVisible = true;
#endif
            }
        }

        private void OnBackClicked(object sender, EventArgs e)
        {
#if ANDROID
            ListPanel.IsVisible = true;
            DetailPanel.IsVisible = false;
            if (_selectedRecipe != null)
            {
                _selectedRecipe.IsSelected = false;
                _selectedRecipe = null;
            }
            RecipesListView.SelectedItem = null;
#endif
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

            LoadData();
        }
    }
}