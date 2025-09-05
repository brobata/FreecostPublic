namespace Freecost;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new NavigationPage(new LoginPage());
	}

    protected override async void OnStart()
    {
        base.OnStart();
        await FirestoreService.InitializeAsync();
    }
}
