using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Freecost
{
    public partial class UnitConversionManagerPage : ContentPage
    {
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
                // Note: Unit conversions are global and don't require an auth token if your rules allow it.
                // If you require login for this, pass SessionService.AuthToken
                _conversions = await FirestoreService.GetCollectionAsync<UnitConversion>("unitConversions", SessionService.AuthToken);
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
                            if (conversion.Id != null)
                            {
                                await FirestoreService.DeleteDocumentAsync($"unitConversions/{conversion.Id}", SessionService.AuthToken);
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