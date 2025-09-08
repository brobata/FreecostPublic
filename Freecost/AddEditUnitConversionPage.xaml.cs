using System;
using System.Linq;
using Google.Cloud.Firestore;

namespace Freecost
{
    public partial class AddEditUnitConversionPage : ContentPage
    {
        private UnitConversion _conversion;

        public AddEditUnitConversionPage(UnitConversion? conversion = null)
        {
            InitializeComponent();
            if (conversion != null)
            {
                _conversion = conversion;
                UnitNameEntry.Text = _conversion.UnitName;
                CategoryEntry.Text = _conversion.Category;
                ToBaseFactorEntry.Text = _conversion.ToBaseFactor.ToString();
            }
            else
            {
                _conversion = new UnitConversion();
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
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
                    if (existing != null)
                    {
                        var index = conversions.IndexOf(existing);
                        conversions[index] = _conversion;
                    }
                }
                await LocalStorageService.SaveAsync(conversions);
            }
            else
            {
                var db = FirestoreService.Db;
                if (db == null) return;
                if (string.IsNullOrEmpty(_conversion.Id))
                {
                    await db.Collection("unitConversions").AddAsync(_conversion);
                }
                else
                {
                    await db.Collection("unitConversions").Document(_conversion.Id).SetAsync(_conversion);
                }
            }

            await Navigation.PopAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}