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
}
