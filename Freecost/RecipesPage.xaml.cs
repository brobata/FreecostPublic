using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
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
            SessionService.OnRestaurantChanged += (s, e) => LoadData();
#if ANDROID
            Grid.SetColumn(DetailPanel, 0);
            ListPanel.IsVisible = true;
            DetailPanel.IsVisible = false;
#endif
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
            _selectedRecipe = e.SelectedItem as RecipeDisplayRecord;

            if (_selectedRecipe != null)
            {
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
                SelectRecipeLabel.IsVisible = false;
#else
                RecipeDetailsView.IsVisible = true;
                SelectRecipeLabel.IsVisible = false;
#endif
            }
            else
            {
#if !ANDROID
                RecipeDetailsView.IsVisible = false;
                SelectRecipeLabel.IsVisible = true;
#endif
            }
        }

        private void OnBackClicked(object sender, EventArgs e)
        {
#if ANDROID
            ListPanel.IsVisible = true;
            DetailPanel.IsVisible = false;
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