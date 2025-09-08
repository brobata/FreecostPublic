using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Freecost
{
    public partial class RecipesPage : ContentPage
    {
        private string? restaurantId;
        private List<RecipeDisplayRecord> _allRecipes = new List<RecipeDisplayRecord>();
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

        private void OnItemDoubleTapped(object sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is RecipeDisplayRecord tappedRecipe)
            {
                _selectedRecipe = tappedRecipe;
                OnEditRecipeClicked(this, EventArgs.Empty);
            }
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
                // This will need to be a more complex query using the REST API if you filter by restaurantId on the server
                // For now, we get all and filter locally.
                var allRecipes = await FirestoreService.GetCollectionAsync<Recipe>("recipes", SessionService.AuthToken);
                var restaurantRecipes = allRecipes.Where(r => r.RestaurantId == restaurantId).ToList();

                recipes = restaurantRecipes.Select(r => new RecipeDisplayRecord
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

                await LocalStorageService.SaveAsync(recipes.Cast<Recipe>().ToList(), restaurantId);
            }
            _allRecipes = recipes;
            SortAndFilterRecipes();
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
                if (restaurantId == null || _selectedRecipe.Id == null) return;

                if (SessionService.IsOffline)
                {
                    var localRecipes = await LocalStorageService.LoadAsync<Recipe>(restaurantId);
                    localRecipes.RemoveAll(r => r.Id == _selectedRecipe.Id);
                    await LocalStorageService.SaveAsync(localRecipes, restaurantId);
                }
                else
                {
                    await FirestoreService.DeleteDocumentAsync($"recipes/{_selectedRecipe.Id}", SessionService.AuthToken);
                }

                await LoadRecipes();
            }
        }

        private void OnRecipeSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (_selectedRecipe != null)
            {
                _selectedRecipe.IsSelected = false;
            }

            _selectedRecipe = e.SelectedItem as RecipeDisplayRecord;

            if (_selectedRecipe != null)
            {
                _selectedRecipe.IsSelected = true;
                RecipeNameLabel.Text = _selectedRecipe.Name;
                RecipeImage.Source = _selectedRecipe.PhotoUrl;
                AllergensLabel.Text = _selectedRecipe.Allergens != null && _selectedRecipe.Allergens.Any()
                    ? " " + string.Join(", ", _selectedRecipe.Allergens)
                    : " None listed.";
                IngredientsListView.ItemsSource = _selectedRecipe.Ingredients;
                DirectionsLabel.Text = _selectedRecipe.Directions ?? "No directions provided.";

#if ANDROID
                ListPanel.IsVisible = false;
                DetailPanel.IsVisible = true;
#endif
                RecipeDetailsView.IsVisible = true;
                SelectRecipeView.IsVisible = false;
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

        private void OnSortClicked(object sender, Microsoft.Maui.Controls.TappedEventArgs e)
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
            SortAndFilterRecipes();
        }
        private void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
        {
            SortAndFilterRecipes();
        }

        private void SortAndFilterRecipes()
        {
            var recipes = _allRecipes;
            var searchBar = this.FindByName("RecipesSearchBar") as SearchBar;
            var searchTerm = searchBar?.Text;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                recipes = recipes.Where(r => r.Name != null && r.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var sortedRecipes = _isSortAscending
                ? recipes.OrderBy(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList()
                : recipes.OrderByDescending(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                RecipesListView.ItemsSource = sortedRecipes;
            });
        }
    }
}