using System;
using System.Linq;

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
                    if (existing != null) conversions[conversions.IndexOf(existing)] = _conversion;
                }
                await LocalStorageService.SaveAsync(conversions);
            }
            else
            {
                if (string.IsNullOrEmpty(_conversion.Id))
                {
                    await FirestoreService.AddDocumentAsync("unitConversions", _conversion, SessionService.AuthToken);
                }
                else
                {
                    await FirestoreService.SetDocumentAsync($"unitConversions/{_conversion.Id}", _conversion, SessionService.AuthToken);
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