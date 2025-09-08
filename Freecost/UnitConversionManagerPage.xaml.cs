using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Freecost
{
    public partial class UnitConversionManagerPage : ContentPage
    {
        private FirestoreDb? db;
        private List<UnitConversion> _conversions = new List<UnitConversion>();

        public UnitConversionManagerPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadConversions();
        }

        private async Task LoadConversions()
        {
            if (SessionService.IsOffline)
            {
                _conversions = await LocalStorageService.LoadAsync<UnitConversion>();
            }
            else
            {
                db = FirestoreService.Db;
                if (db == null) return;
                var snapshot = await db.Collection("unitConversions").GetSnapshotAsync();
                _conversions = snapshot.Documents.Select(doc =>
                {
                    var conversion = doc.ConvertTo<UnitConversion>();
                    conversion.Id = doc.Id;
                    return conversion;
                }).ToList();
            }
            ConversionsListView.ItemsSource = _conversions;
        }

        private async void OnAddConversionClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AddEditUnitConversionPage());
        }

        private async void OnEditConversionClicked(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                var conversion = button.CommandParameter as UnitConversion;
                if (conversion != null)
                {
                    await Navigation.PushAsync(new AddEditUnitConversionPage(conversion));
                }
            }
        }

        private async void OnDeleteConversionClicked(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                var conversion = button.CommandParameter as UnitConversion;
                if (conversion != null)
                {
                    bool answer = await DisplayAlert("Confirm Deletion", $"Are you sure you want to delete the conversion for '{conversion.UnitName}'?", "Yes", "No");
                    if (answer)
                    {
                        if (SessionService.IsOffline)
                        {
                            _conversions.Remove(conversion);
                            await LocalStorageService.SaveAsync(_conversions);
                        }
                        else
                        {
                            db = FirestoreService.Db;
                            if (db != null && conversion.Id != null)
                            {
                                await db.Collection("unitConversions").Document(conversion.Id).DeleteAsync();
                            }
                        }
                        await LoadConversions();
                    }
                }
            }
        }

        private async void OnDoneClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}