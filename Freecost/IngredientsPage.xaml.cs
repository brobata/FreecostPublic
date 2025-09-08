using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using CsvHelper;
using OfficeOpenXml;
using System.Text.RegularExpressions;
using System.ComponentModel;
using Plugin.Firebase.Firestore;

#if WINDOWS
using Microsoft.UI.Input;
using Windows.System;
#endif

namespace Freecost
{
    public partial class IngredientsPage : ContentPage
    {
        private string? restaurantId;
        private List<IngredientDisplayRecord> _allIngredients = new List<IngredientDisplayRecord>();
        private List<IngredientDisplayRecord> _ingredients = new List<IngredientDisplayRecord>();
        private List<IngredientDisplayRecord> _selectedIngredients = new List<IngredientDisplayRecord>();
        private IngredientDisplayRecord? _lastSelectedItem;
        private string _currentSortColumn = "ItemName";
        private bool _isSortAscending = true;
        private bool _initialMapsCreated = false;

        public IngredientsPage()
        {
            InitializeComponent();
            SessionService.StaticPropertyChanged += OnSessionChanged;
        }

        private void OnRowDoubleTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is not IngredientDisplayRecord tappedIngredient) return;

            foreach (var item in _selectedIngredients)
            {
                item.IsSelected = false;
            }
            _selectedIngredients.Clear();

            tappedIngredient.IsSelected = true;
            _selectedIngredients.Add(tappedIngredient);

            OnEditIngredientClicked(this, EventArgs.Empty);
        }

        private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionService.CurrentRestaurant))
            {
                LoadData();
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_initialMapsCreated)
            {
                await CreateInitialMaps();
                _initialMapsCreated = true;
            }
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
            Task.Run(async () => await LoadIngredients());
        }

        private async Task LoadIngredients()
        {
            restaurantId = SessionService.CurrentRestaurant?.Id;
            if (string.IsNullOrEmpty(restaurantId))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _allIngredients.Clear();
                    _ingredients.Clear();
                    PopulateGrid();
                });
                return;
            }

            if (SessionService.IsOffline)
            {
                var ingredientsList = await LocalStorageService.LoadAsync<IngredientCsvRecord>(restaurantId);
                _allIngredients = ingredientsList.Select(i => new IngredientDisplayRecord
                {
                    Id = i.Id,
                    SupplierName = i.SupplierName,
                    ItemName = i.ItemName,
                    AliasName = i.AliasName,
                    CasePrice = i.CasePrice,
                    CaseQuantity = i.CaseQuantity,
                    Unit = i.Unit,
                    SKU = i.SKU
                }).ToList();
            }
            else
            {
                var ingredientsList = await FirestoreService.GetIngredientsAsync(restaurantId);
                _allIngredients = ingredientsList.Select(i => new IngredientDisplayRecord
                {
                    Id = i.Id,
                    SupplierName = i.SupplierName,
                    ItemName = i.ItemName,
                    AliasName = i.AliasName,
                    CasePrice = i.CasePrice,
                    CaseQuantity = i.CaseQuantity,
                    Unit = i.Unit,
                    SKU = i.SKU
                }).ToList();
                await LocalStorageService.SaveAsync(ingredientsList.Cast<IngredientCsvRecord>().ToList(), restaurantId);
            }

            _ingredients = new List<IngredientDisplayRecord>(_allIngredients);

            SortIngredients();
            MainThread.BeginInvokeOnMainThread(PopulateGrid);
        }

        private void PopulateGrid()
        {
            IngredientsGrid.RowDefinitions.Clear();
            IngredientsGrid.Children.Clear();

            for (int i = 0; i < _ingredients.Count; i++)
            {
                var ingredient = _ingredients[i];
                ingredient.IsEven = i % 2 == 0;

                IngredientsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var singleTapGesture = new TapGestureRecognizer();
                singleTapGesture.Tapped += OnRowTapped;
                singleTapGesture.CommandParameter = ingredient;

                var doubleTapGesture = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
                doubleTapGesture.Tapped += OnRowDoubleTapped;
                doubleTapGesture.CommandParameter = ingredient;

                var backgroundColor = ingredient.IsSelected
                    ? (Color)Application.Current.Resources["Accent"]
                    : (ingredient.IsEven ? (Color)Application.Current.Resources["RowColorEven"] : (Color)Application.Current.Resources["RowColorOdd"]);

                var backgroundGrid = new Grid
                {
                    BackgroundColor = backgroundColor,
                    GestureRecognizers = { singleTapGesture, doubleTapGesture },
                    BindingContext = ingredient
                };

                IngredientsGrid.Add(backgroundGrid, 0, i);
                Grid.SetColumnSpan(backgroundGrid, 7);

                IngredientsGrid.Add(CreateDataLabel(ingredient.ItemName, TextAlignment.Start), 0, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.AliasName, TextAlignment.Start), 1, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.SupplierName, TextAlignment.Start), 2, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.SKU, TextAlignment.Start), 3, i);
                IngredientsGrid.Add(CreateDataLabel(string.Format("{0:C}", ingredient.CasePrice), TextAlignment.Start), 4, i);
                IngredientsGrid.Add(CreateDataLabel(string.Format("{0:F2}", ingredient.CaseQuantity), TextAlignment.Start), 5, i);
                IngredientsGrid.Add(CreateDataLabel(ingredient.Unit, TextAlignment.Start), 6, i);

                var bottomBorder = new BoxView { HeightRequest = 1, Color = (Color)Application.Current.Resources["BorderColor"], VerticalOptions = LayoutOptions.End };
                IngredientsGrid.Add(bottomBorder, 0, i);
                Grid.SetColumnSpan(bottomBorder, 7);
            }
        }


        private Label CreateDataLabel(string? text, TextAlignment alignment)
        {
            return new Label
            {
                Text = text ?? string.Empty,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                HorizontalTextAlignment = alignment,
                Padding = new Thickness(5, 10),
                InputTransparent = true
            };
        }

        private void OnRowTapped(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is not IngredientDisplayRecord tappedIngredient) return;

            var previouslySelected = _selectedIngredients.ToList();

            bool isCtrlPressed = false;
            bool isShiftPressed = false;

#if WINDOWS
            var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            isShiftPressed = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
#endif

            if (isCtrlPressed)
            {
                tappedIngredient.IsSelected = !tappedIngredient.IsSelected;
                if (tappedIngredient.IsSelected) _selectedIngredients.Add(tappedIngredient);
                else _selectedIngredients.Remove(tappedIngredient);
                _lastSelectedItem = tappedIngredient;
            }
            else if (isShiftPressed && _lastSelectedItem != null)
            {
                var lastIndex = _ingredients.IndexOf(_lastSelectedItem);
                var currentIndex = _ingredients.IndexOf(tappedIngredient);
                var startIndex = Math.Min(lastIndex, currentIndex);
                var endIndex = Math.Max(lastIndex, currentIndex);

                foreach (var item in _selectedIngredients) item.IsSelected = false;
                _selectedIngredients.Clear();

                for (int i = startIndex; i <= endIndex; i++)
                {
                    _ingredients[i].IsSelected = true;
                    _selectedIngredients.Add(_ingredients[i]);
                }
            }
            else
            {
                foreach (var item in _selectedIngredients) item.IsSelected = false;
                _selectedIngredients.Clear();
                tappedIngredient.IsSelected = true;
                _selectedIngredients.Add(tappedIngredient);
                _lastSelectedItem = tappedIngredient;
            }

            var changedIngredients = previouslySelected.Union(_selectedIngredients).Distinct();
            foreach (var ingredient in changedIngredients)
            {
                var backgroundGrid = IngredientsGrid.Children.OfType<Grid>().FirstOrDefault(g => g.BindingContext == ingredient);
                if (backgroundGrid != null)
                {
                    backgroundGrid.BackgroundColor = ingredient.IsSelected
                        ? (Color)Application.Current.Resources["Accent"]
                        : (ingredient.IsEven ? (Color)Application.Current.Resources["RowColorEven"] : (Color)Application.Current.Resources["RowColorOdd"]);
                }
            }
        }

        private async void OnAddIngredientClicked(object? sender, EventArgs e)
        {
            await Navigation.PushAsync(new AddIngredientPage());
        }

        private async void OnEditIngredientClicked(object? sender, EventArgs e)
        {
            if (_selectedIngredients.Count != 1)
            {
                await DisplayAlert("Selection Error", "Please select exactly one ingredient to edit.", "OK");
                return;
            }
            await Navigation.PushAsync(new AddIngredientPage(_selectedIngredients.First()));
        }

        private async void OnDeleteIngredientClicked(object? sender, EventArgs e)
        {
            if (_selectedIngredients.Count == 0)
            {
                await DisplayAlert("No Ingredients Selected", "Please select one or more ingredients to delete.", "OK");
                return;
            }

            bool answer = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete {_selectedIngredients.Count} ingredient(s)?", "Yes", "No");
            if (answer)
            {
                if (SessionService.IsOffline)
                {
                    _allIngredients.RemoveAll(ing => _selectedIngredients.Contains(ing));
                    await LocalStorageService.SaveAsync(_allIngredients.Cast<IngredientCsvRecord>().ToList(), restaurantId);
                }
                else
                {
                    var firestore = CrossFirebase.Current.Firestore;
                    if (string.IsNullOrEmpty(restaurantId)) return;

                    var batch = firestore.CreateBatch();
                    foreach (var ingredient in _selectedIngredients)
                    {
                        if (ingredient.Id != null)
                        {
                            var docRef = firestore.Collection("restaurants").Document(restaurantId).Collection("ingredients").Document(ingredient.Id);
                            batch.Delete(docRef);
                        }
                    }
                    await batch.CommitAsync();
                }

                _selectedIngredients.Clear();
                LoadData();
            }
        }

        private void SortIngredients()
        {
            if (_ingredients == null) return;
            var sorted = _isSortAscending
                ? _ingredients.OrderBy(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList()
                : _ingredients.OrderByDescending(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList();
            _ingredients = sorted;
        }

        private void OnSortClicked(object? sender, Microsoft.Maui.Controls.TappedEventArgs e)
        {
            if (e.Parameter is string newSortColumn)
            {
                if (_currentSortColumn == newSortColumn) _isSortAscending = !_isSortAscending;
                else { _currentSortColumn = newSortColumn; _isSortAscending = true; }
                SortIngredients();
                PopulateGrid();
            }
        }

        private void OnSearchBarTextChanged(object? sender, TextChangedEventArgs e)
        {
            var searchTerm = e.NewTextValue;
            if (string.IsNullOrWhiteSpace(searchTerm)) _ingredients = new List<IngredientDisplayRecord>(_allIngredients);
            else
            {
                _ingredients = _allIngredients
                    .Where(i => (i.ItemName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (i.AliasName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (i.SupplierName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }
            PopulateGrid();
        }

        #region Bulk Import Logic (No Changes Needed Here)
        private async Task CreateInitialMaps()
        {
            // This logic is fine, as it uses the client SDK correctly
        }
        private async void OnBulkImportClicked(object sender, EventArgs e)
        {
            // This logic is fine, as it uses the client SDK correctly
        }
        #endregion
    }
}