using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Plugin.Firebase.Firestore;

namespace Freecost
{
    public partial class AddEditUnitConversionPage : ContentPage
    {
        private UnitConversion _conversion;

        public AddEditUnitConversionPage(UnitConversion? conversion = null)
        {
            InitializeComponent();
            _conversion = conversion ?? new UnitConversion();
            if (conversion != null)
            {
                UnitNameEntry.Text = _conversion.UnitName;
                CategoryEntry.Text = _conversion.Category;
                ToBaseFactorEntry.Text = _conversion.ToBaseFactor.ToString();
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                _conversion.UnitName = UnitNameEntry.Text;
                _conversion.Category = CategoryEntry.Text;
                _conversion.ToBaseFactor = Convert.ToDouble(ToBaseFactorEntry.Text);

                if (SessionService.IsOffline)
                {
                    var conversions = await LocalStorageService.LoadAsync<UnitConversion>();
                    if (string.IsNullOrEmpty(_conversion.Id))
                    {
                        _conversion.Id = Guid.NewGuid().ToString();
                        conversions.Add(_conversion);
                    }
                    else
                    {
                        var existing = conversions.FirstOrDefault(c => c.Id == _conversion.Id);
                        if (existing != null) conversions[conversions.IndexOf(existing)] = _conversion;
                    }
                    await LocalStorageService.SaveAsync(conversions);
                }
                else
                {
                    var collection = CrossFirebase.Current.Firestore.Collection("unitConversions");
                    if (string.IsNullOrEmpty(_conversion.Id))
                    {
                        await collection.AddAsync(_conversion);
                    }
                    else
                    {
                        await collection.Document(_conversion.Id).SetAsync(_conversion);
                    }
                }

                await UnitConverter.InitializeAsync(); // Re-initialize to get latest conversions
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save Error", ex.Message, "OK");
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}