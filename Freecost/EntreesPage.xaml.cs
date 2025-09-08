using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Plugin.Firebase.Firestore;

namespace Freecost
{
    public partial class EntreesPage : ContentPage
    {
        private string? restaurantId;
        private List<EntreeDisplayRecord> _allEntrees = new List<EntreeDisplayRecord>();
        private EntreeDisplayRecord? _selectedEntree;
        private string _currentSortColumn = "Name";
        private bool _isSortAscending = true;

        public EntreesPage()
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
            Task.Run(async () => await LoadEntrees());
        }

        private async Task LoadEntrees()
        {
            restaurantId = SessionService.CurrentRestaurant?.Id;
            if (string.IsNullOrEmpty(restaurantId))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _allEntrees.Clear();
                    SortAndFilterEntrees();
                });
                return;
            }

            List<EntreeDisplayRecord> entrees;
            if (SessionService.IsOffline)
            {
                var entreeData = await LocalStorageService.LoadAsync<Entree>(restaurantId);
                entrees = entreeData.Select(e => new EntreeDisplayRecord { /* mapping */ }).ToList();
            }
            else
            {
                // This would require a new GetEntreesAsync method in FirestoreService
                var entreeData = await FirestoreService.GetEntreesAsync(restaurantId);
                entrees = entreeData.Select(e => new EntreeDisplayRecord
                {
                    Id = e.Id,
                    Name = e.Name,
                    Yield = e.Yield,
                    YieldUnit = e.YieldUnit,
                    Directions = e.Directions,
                    PhotoUrl = e.PhotoUrl,
                    RestaurantId = e.RestaurantId,
                    Allergens = e.Allergens,
                    Components = e.Components,
                    FoodCost = e.FoodCost,
                    Price = e.Price,
                    PlatePrice = e.PlatePrice
                }).ToList();
                await LocalStorageService.SaveAsync(entreeData.Cast<Entree>().ToList(), restaurantId);
            }
            _allEntrees = entrees;
            SortAndFilterEntrees();
        }

        private async void OnAddEntreeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(restaurantId)) return;
            await Navigation.PushAsync(new AddEntreePage(restaurantId));
        }

        private async void OnEditEntreeClicked(object sender, EventArgs e)
        {
            if (_selectedEntree == null)
            {
                await DisplayAlert("No Entree Selected", "Please select an entree to edit.", "OK");
                return;
            }
            if (string.IsNullOrEmpty(restaurantId)) return;
            await Navigation.PushAsync(new AddEntreePage(restaurantId, _selectedEntree));
        }

        private async void OnDeleteEntreeClicked(object sender, EventArgs e)
        {
            if (_selectedEntree == null || string.IsNullOrEmpty(_selectedEntree.Id))
            {
                await DisplayAlert("No Entree Selected", "Please select an entree to delete.", "OK");
                return;
            }

            bool answer = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete {_selectedEntree.Name}?", "Yes", "No");
            if (answer)
            {
                if (SessionService.IsOffline)
                {
                    // offline logic
                }
                else
                {
                    await CrossFirebase.Current.Firestore
                                    .Collection("entrees")
                                    .Document(_selectedEntree.Id)
                                    .DeleteAsync();
                }
                LoadData();
            }
        }

        private void SortAndFilterEntrees()
        {
            var entrees = _allEntrees;
            var searchTerm = EntreesSearchBar?.Text;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                entrees = entrees.Where(e => e.Name != null && e.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var sortedEntrees = _isSortAscending
                ? entrees.OrderBy(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList()
                : entrees.OrderByDescending(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                EntreesListView.ItemsSource = sortedEntrees;
            });
        }

        // Keep your other UI methods like OnEntreeSelected, OnBackClicked, etc.
    }
}