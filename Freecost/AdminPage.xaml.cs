using System;

namespace Freecost;

public partial class AdminPage : ContentPage
{
    public AdminPage()
    {
        InitializeComponent();
    }

    private async void OnManageImportMapsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ImportMapManagerPage());
    }

    private void OnResetPopupCounterClicked(object sender, EventArgs e)
    {
        Preferences.Set("AppOpenCount", 0);
        DisplayAlert("Counter Reset", "The popup counter has been reset. The popup will appear on the next app launch.", "OK");
    }
}