using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Freecost;

public partial class EntreesPage : ContentPage
{
    private FirestoreDb? db;
    private string? restaurantId;
    private EntreeDisplayRecord? _selectedEntree;
    private EntreeDisplayRecord? _previousSelectedEntree;
    private string _currentSortColumn = "Name";
    private bool _isSortAscending = true;
    private const double TargetFoodCostPercentage = 0.30;

	public EntreesPage()
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
        Task.Run(async () => await LoadEntrees());
    }

    private async Task LoadEntrees()
    {
        db = FirestoreService.Db;
        if (db == null || restaurantId == null) return;

        var entrees = new List<EntreeDisplayRecord>();
        var query = db.Collection("entrees").WhereEqualTo("RestaurantId", restaurantId);
        var snapshot = await query.GetSnapshotAsync();
        var documents = snapshot.Documents.ToList();

        for (int i = 0; i < documents.Count; i++)
        {
            var document = documents[i];
            var entree = document.ConvertTo<Entree>();
            var displayRecord = new EntreeDisplayRecord
            {
                Id = document.Id,
                Name = entree.Name,
                Yield = entree.Yield,
                YieldUnit = entree.YieldUnit,
                Directions = entree.Directions,
                PhotoUrl = entree.PhotoUrl,
                RestaurantId = entree.RestaurantId,
                Allergens = entree.Allergens,
                Components = entree.Components,
                FoodCost = entree.FoodCost,
                Price = entree.Price
            };
            entrees.Add(displayRecord);
        }
        MainThread.BeginInvokeOnMainThread(() =>
        {
            EntreesListView.ItemsSource = entrees;
        });
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
            if (db == null || restaurantId == null) return;
            await db.Collection("entrees").Document(_selectedEntree.Id).DeleteAsync();
            LoadData();
        }
    }

    private void OnEntreeSelected(object sender, SelectedItemChangedEventArgs e)
    {
        if (e.SelectedItem == null) return;

        _selectedEntree = e.SelectedItem as EntreeDisplayRecord;
        if (_selectedEntree != null)
        {
            _previousSelectedEntree = _selectedEntree;

            EntreeDetailsView.IsVisible = true;
            SelectEntreeLabel.IsVisible = false;

            EntreeImage.Source = _selectedEntree.PhotoUrl;
            if (_selectedEntree.Components != null)
            {
                ComponentsLabel.Text = string.Join(Environment.NewLine, _selectedEntree.Components.Select(i => $"{i.Name}: {i.Quantity} {i.Unit}"));
            }
            else
            {
                ComponentsLabel.Text = string.Empty;
            }

            if (_selectedEntree.Allergens != null)
            {
                AllergensLabel.Text = string.Join(", ", _selectedEntree.Allergens);
            }
            else
            {
                AllergensLabel.Text = string.Empty;
            }

            DirectionsLabel.Text = _selectedEntree.Directions;
        }
        else
        {
            EntreeDetailsView.IsVisible = false;
            SelectEntreeLabel.IsVisible = true;
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

        if (EntreesListView.ItemsSource is not List<EntreeDisplayRecord> entrees) return;

        var sortedEntrees = _isSortAscending
            ? entrees.OrderBy(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList()
            : entrees.OrderByDescending(p => p.GetType().GetProperty(_currentSortColumn)?.GetValue(p, null)).ToList();

        EntreesListView.ItemsSource = sortedEntrees;
    }
}
