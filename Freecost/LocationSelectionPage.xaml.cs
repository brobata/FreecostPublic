using System;
using System.Collections.Generic;
using System.Linq;

namespace Freecost;

public partial class LocationSelectionPage : ContentPage
{
	public LocationSelectionPage()
	{
		InitializeComponent();
        LocationPicker.ItemsSource = SessionService.PermittedRestaurants;
        LocationPicker.ItemDisplayBinding = new Binding("Name");
        if (SessionService.PermittedRestaurants?.Count > 0)
        {
            LocationPicker.SelectedIndex = 0;
        }
	}

    private void OnConfirmClicked(object sender, EventArgs e)
    {
        if (LocationPicker.SelectedItem is Restaurant selectedRestaurant)
        {
            SessionService.CurrentRestaurant = selectedRestaurant;
            if (Application.Current != null)
            {
                Application.Current.MainPage = new MainShell();
            }
        }
    }
}
