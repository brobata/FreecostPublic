using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Plugin.Firebase.Firestore;

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
            if (string.IsNullOrEmpty(restaurantId))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _allRecipes.Clear();
                    SortAndFilterRecipes();
                });
                return;
            }

            List<RecipeDisplayRecord> recipes;
            if (SessionService.IsOffline)
            {
                var recipeData = await LocalStorageService.LoadAsync<Recipe>(restaurantId);
                recipes = recipeData.Select(r => new RecipeDisplayRecord { /* mapping */ }).ToList();
            }
            else
            {
                var recipeData = await FirestoreService.GetRecipesAsync(restaurantId);
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
                await LocalStorageService.SaveAsync(recipeData.Cast<Recipe>().ToList(), restaurantId);
            }
            _allRecipes = recipes;
            SortAndFilterRecipes();
        }

        private async void OnAddRecipeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(restaurantId)) return;
            await Navigation.PushAsync(new AddRecipePage(restaurantId));
        }

        private async void OnEditRecipeClicked(object sender, EventArgs e)
        {
            if (_selectedRecipe == null)
            {
                await DisplayAlert("No Recipe Selected", "Please select a recipe to edit.", "OK");
                return;
            }
            if (string.IsNullOrEmpty(restaurantId)) return;
            await Navigation.PushAsync(new AddRecipePage(restaurantId, _selectedRecipe));
        }

        private async void OnDeleteRecipeClicked(object sender, EventArgs e)
        {
            if (_selectedRecipe == null || string.IsNullOrEmpty(_selectedRecipe.Id))
            {
                await DisplayAlert("No Recipe Selected", "Please select a recipe to delete.", "OK");
                return;
            }

            bool answer = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete {_selectedRecipe.Name}?", "Yes", "No");
            if (answer)
            {
                if (SessionService.IsOffline)
                {
                    // Handle offline deletion
                }
                else
                {
                    await CrossFirebase.Current.Firestore
                                     .Collection("recipes")
                                     .Document(_selectedRecipe.Id)
                                     .DeleteAsync();
                }
                LoadData();
            }
        }

        private void SortAndFilterRecipes()
        {
            var recipes = _allRecipes;
            var searchTerm = RecipesSearchBar?.Text;

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

        // Keep your other UI methods like OnRecipeSelected, OnBackClicked, etc.
        // They do not need to change.
    }
}