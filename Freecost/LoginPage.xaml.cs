using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Freecost;

public partial class LoginPage : ContentPage
{
    private const string WebApiKey = "AIzaSyCmS_d7lw9z-mdtNNoz8JafWsI1iprKRM0"; // Make sure your key is here
    private static readonly HttpClient client = new HttpClient();

    public LoginPage()
    {
        InitializeComponent();
        LoadCredentials();
    }

    private void LoadCredentials()
    {
        if (Preferences.Get("RememberMe", false))
        {
            EmailEntry.Text = Preferences.Get("Email", string.Empty);
            PasswordEntry.Text = Preferences.Get("Password", string.Empty);
            RememberMeCheckBox.IsChecked = true;
        }
    }

    private async Task DownloadAllDataForRestaurant(string restaurantId, string authToken)
    {
        // Ingredients
        var serverIngredients = await FirestoreService.GetCollectionAsync<IngredientCsvRecord>($"restaurants/{restaurantId}/ingredients", authToken);
        await LocalStorageService.SaveAsync(serverIngredients, restaurantId);

        // Recipes
        var allServerRecipes = await FirestoreService.GetCollectionAsync<Recipe>("recipes", authToken);
        var restaurantRecipes = allServerRecipes.Where(r => r.RestaurantId == restaurantId).ToList();
        await LocalStorageService.SaveAsync(restaurantRecipes, restaurantId);

        // Entrees
        var allServerEntrees = await FirestoreService.GetCollectionAsync<Entree>("entrees", authToken);
        var restaurantEntrees = allServerEntrees.Where(e => e.RestaurantId == restaurantId).ToList();
        await LocalStorageService.SaveAsync(restaurantEntrees, restaurantId);
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string email = EmailEntry.Text;
        string password = PasswordEntry.Text;
        string signInUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={WebApiKey}";

        try
        {
            await SessionService.Clear();

            var requestData = new { email, password, returnSecureToken = true };
            var jsonContent = JsonConvert.SerializeObject(requestData);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(signInUrl, httpContent);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Authentication Failed", "Login failed. Please check your email and password.", "OK");
                return;
            }

            if (RememberMeCheckBox.IsChecked)
            {
                Preferences.Set("RememberMe", true);
                Preferences.Set("Email", email);
                Preferences.Set("Password", password);
            }
            else
            {
                Preferences.Remove("RememberMe");
                Preferences.Remove("Email");
                Preferences.Remove("Password");
            }

            var authData = JsonConvert.DeserializeObject<dynamic>(responseJson);
            string? userUid = authData?.localId.ToString();
            string? idToken = authData?.idToken.ToString();
            string? refreshToken = authData?.refreshToken.ToString();

            if (string.IsNullOrEmpty(userUid) || string.IsNullOrEmpty(idToken))
            {
                await DisplayAlert("Authentication Failed", "Login failed. Could not get user details.", "OK");
                return;
            }

            var userDoc = await FirestoreService.GetDocumentAsync<Dictionary<string, object>>($"users/{userUid}", idToken);
            if (userDoc == null)
            {
                await DisplayAlert("Authorization Failed", "Login successful, but no permission document was found.", "OK");
                return;
            }

            SessionService.UserUid = userUid;
            SessionService.AuthToken = idToken;
            SessionService.RefreshToken = refreshToken;
            SessionService.CurrentUserEmail = email;
            SessionService.UserRole = userDoc.TryGetValue("role", out var role) ? role.ToString() : null;
            SessionService.IsOffline = false;

            var permittedRestaurantIds = new List<string>();
            if (userDoc.TryGetValue("Restaurants", out var ids) && ids is JArray idsArray)
            {
                permittedRestaurantIds = idsArray.ToObject<List<string>>() ?? new List<string>();
            }

            if (permittedRestaurantIds != null && permittedRestaurantIds.Any())
            {
                var restaurants = new List<Restaurant>();
                var allRestaurants = await FirestoreService.GetCollectionAsync<Restaurant>("restaurants", idToken);
                foreach (var id in permittedRestaurantIds)
                {
                    var restaurant = allRestaurants.FirstOrDefault(r => r.Id == id);
                    if (restaurant != null)
                    {
                        restaurants.Add(restaurant);
                    }
                }

                SessionService.PermittedRestaurants = restaurants;
                await LocalStorageService.SaveAsync(restaurants);

                if (string.IsNullOrEmpty(SessionService.DefaultRestaurantId))
                {
                    SessionService.DefaultRestaurantId = restaurants.FirstOrDefault()?.Id;
                }

                SessionService.CurrentRestaurant = restaurants.FirstOrDefault(r => r.Id == SessionService.DefaultRestaurantId) ?? restaurants.FirstOrDefault();

                if (SessionService.CurrentRestaurant?.Id != null && idToken != null)
                {
                    await DownloadAllDataForRestaurant(SessionService.CurrentRestaurant.Id, idToken);
                }

                SessionService.SaveSession();

                await UnitConverter.InitializeAsync();

                if (Application.Current != null)
                    Application.Current.MainPage = new MainShell();
            }
            else
            {
                await DisplayAlert("No Locations", "You are not assigned to any locations.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "An unexpected error occurred: " + ex.Message, "OK");
        }
    }

    private async void OnOfflineClicked(object sender, EventArgs e)
    {
        SessionService.StartOfflineSession();
        var restaurants = await LocalStorageService.LoadAsync<Restaurant>();

        if (restaurants != null && restaurants.Any())
        {
            SessionService.PermittedRestaurants = restaurants;
            if (Application.Current != null) Application.Current.MainPage = new MainShell();

            if (restaurants.Count > 1)
            {
                await Shell.Current.GoToAsync(nameof(LocationSelectionPage));
            }
            else
            {
                SessionService.CurrentRestaurant = restaurants.First();
            }
        }
        else
        {
            if (Application.Current != null) Application.Current.MainPage = new MainShell();
        }
    }
}