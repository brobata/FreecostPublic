using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

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

        private void OnItemDoubleTapped(object sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is EntreeDisplayRecord tappedEntree)
            {
                _selectedEntree = tappedEntree;
                OnEditEntreeClicked(this, EventArgs.Empty);
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
            Task.Run(async () => await LoadEntrees());
        }

        private async Task LoadEntrees()
        {
            restaurantId = SessionService.CurrentRestaurant?.Id;
            if (restaurantId == null) return;

            List<EntreeDisplayRecord> entrees;
            if (SessionService.IsOffline)
            {
                var entreeData = await LocalStorageService.LoadAsync<Entree>(restaurantId);
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
            }
            else
            {
                var allEntrees = await FirestoreService.GetCollectionAsync<Entree>("entrees", SessionService.AuthToken);
                var restaurantEntrees = allEntrees.Where(e => e.RestaurantId == restaurantId).ToList();

                entrees = restaurantEntrees.Select(e => new EntreeDisplayRecord
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
                await LocalStorageService.SaveAsync(entrees.Cast<Entree>().ToList(), restaurantId);
            }
            _allEntrees = entrees;
            SortAndFilterEntrees();
        }

        private async void OnAddEntreeClicked(object sender, EventArgs e)
        {
            if (restaurantId == null) return;
            await Navigation.PushAsync(new AddEntreePage(restaurantId));
        }

        private async void OnEditEntreeClicked(object sender, EventArgs e)
        {
            if (_selectedEntree == null)
            {
                await DisplayAlert("No Entree Selected", "Please select an entree to edit.", "OK");
                return;
            }
            if (restaurantId == null) return;
            await Navigation.PushAsync(new AddEntreePage(restaurantId, _selectedEntree));
        }

        private async void OnDeleteEntreeClicked(object sender, EventArgs e)
        {
            if (_selectedEntree == null)
            {
                await DisplayAlert("No Entree Selected", "Please select an entree to delete.", "OK");
                return;
            }

            bool answer = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete {_selectedEntree.Name}?", "Yes", "No");
            if (answer)
            {
                if (restaurantId == null || _selectedEntree.Id == null) return;

                if (SessionService.IsOffline)
                {
                    var localEntrees = await LocalStorageService.LoadAsync<Entree>(restaurantId);
                    localEntrees.RemoveAll(ent => ent.Id == _selectedEntree.Id);
                    await LocalStorageService.SaveAsync(localEntrees, restaurantId);
                }
                else
                {
                    await FirestoreService.DeleteDocumentAsync($"entrees/{_selectedEntree.Id}", SessionService.AuthToken);
                }

                await LoadEntrees();
            }
        }


private void OnEntreeSelected(object sender, SelectedItemChangedEventArgs e)
        {
            // First, un-select the previously selected item if it exists
            if (_selectedEntree != null)
            {
                _selectedEntree.IsSelected = false;
            }

            _selectedEntree = e.SelectedItem as EntreeDisplayRecord;

            if (_selectedEntree != null)
            {
                _selectedEntree.IsSelected = true;

                EntreeNameLabel.Text = _selectedEntree.Name;
                EntreeImage.Source = _selectedEntree.PhotoUrl;

                if (_selectedEntree.Allergens != null && _selectedEntree.Allergens.Any())
                {
                    AllergensLabel.Text = " " + string.Join(", ", _selectedEntree.Allergens);
                }
                else
                {
                    AllergensLabel.Text = " None listed.";
                }

                ComponentsListView.ItemsSource = _selectedEntree.Components;
                DirectionsLabel.Text = _selectedEntree.Directions ?? "No directions provided.";
#if ANDROID
                ListPanel.IsVisible = false;
                DetailPanel.IsVisible = true;
                EntreeDetailsView.IsVisible = true;
                SelectEntreeView.IsVisible = false;
#else
                EntreeDetailsView.IsVisible = true;
                SelectEntreeView.IsVisible = false;
#endif
            }
            else
            {
#if !ANDROID
                EntreeDetailsView.IsVisible = false;
                SelectEntreeView.IsVisible = true;
#endif
            }
        }

        private void OnBackClicked(object sender, EventArgs e)
        {
#if ANDROID
            ListPanel.IsVisible = true;
            DetailPanel.IsVisible = false;
            if (_selectedEntree != null)
            {
                _selectedEntree.IsSelected = false;
                _selectedEntree = null;
            }
            EntreesListView.SelectedItem = null;
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

            SortAndFilterEntrees();
        }
        private void OnSearchBarTextChanged(object sender, TextChangedEventArgs e)
        {
            SortAndFilterEntrees();
        }

        private void SortAndFilterEntrees()
        {
            var entrees = _allEntrees;
            var searchBar = this.FindByName("EntreesSearchBar") as SearchBar;
            var searchTerm = searchBar?.Text;

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
    }
}