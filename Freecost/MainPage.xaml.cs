using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json;

namespace Freecost;

public partial class MainPage : ContentPage
{
    public string? UserUid { get; set; }
    public string? UserRole { get; set; }
    public List<Restaurant>? PermittedRestaurants { get; set; }


	public MainPage()
	{
		InitializeComponent();
        UserUid = SessionService.UserUid;
        UserRole = SessionService.UserRole;
        PermittedRestaurants = SessionService.PermittedRestaurants;

        if (PermittedRestaurants != null && PermittedRestaurants.Count > 1)
        {
            LocationPicker.ItemsSource = PermittedRestaurants;
            LocationPicker.ItemDisplayBinding = new Binding("Name");
            LocationPicker.SelectedIndex = 0;
        }
        else if (PermittedRestaurants != null && PermittedRestaurants.Count == 1)
        {
            SessionService.CurrentRestaurant = PermittedRestaurants[0];
            // Automatically navigate to the main content or hide selection UI
            SelectLocationLabel.IsVisible = false;
            LocationPicker.IsVisible = false;
            ConfirmLocationButton.IsVisible = false;
            Task.Run(async () => await Shell.Current.GoToAsync("//IngredientsPage"));
        }
        else
        {
            // Handle case with no permitted restaurants
            SelectLocationLabel.Text = "No locations available.";
            LocationPicker.IsVisible = false;
            ConfirmLocationButton.IsVisible = false;
        }
	}

    private async void OnConfirmLocationClicked(object sender, System.EventArgs e)
    {
        if (LocationPicker.SelectedItem is Restaurant selectedRestaurant)
        {
            SessionService.CurrentRestaurant = selectedRestaurant;
            await Shell.Current.GoToAsync("//IngredientsPage");
        }
    }
}
