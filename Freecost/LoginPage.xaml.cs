using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Google.Cloud.Firestore;
using Newtonsoft.Json;

namespace Freecost;

public partial class LoginPage : ContentPage
{
    private const string WebApiKey = "AIzaSyCmS_d7lw9z-mdtNNoz8JafWsI1iprKRM0"; // Make sure your key is here
    private static readonly HttpClient client = new HttpClient();
    private FirestoreDb? db;

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

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await FirestoreService.InitializeAsync();
        db = FirestoreService.Db;
        if (db == null)
        {
            await DisplayAlert("Database Error", "Could not connect to the database.", "OK");
            return;
        }

        string email = EmailEntry.Text;
        string password = PasswordEntry.Text;
        string signInUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={WebApiKey}";

        try
        {
            var requestData = new { email = email, password = password, returnSecureToken = true };
            var jsonContent = JsonConvert.SerializeObject(requestData);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(signInUrl, httpContent);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
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

                if (userUid == null)
                {
                    await DisplayAlert("Authentication Failed", "Login failed. Could not get user ID.", "OK");
                    return;
                }

                DocumentReference userDocRef = db.Collection("users").Document(userUid);
                DocumentSnapshot userSnapshot = await userDocRef.GetSnapshotAsync();

                if (!userSnapshot.Exists)
                {
                    await DisplayAlert("Authorization Failed", "Login successful, but no permission document was found.", "OK");
                    return;
                }

                // Set session properties
                SessionService.UserUid = userUid;
                SessionService.AuthToken = idToken;
                SessionService.CurrentUserEmail = email;
                SessionService.UserRole = userSnapshot.GetValue<string>("role");
                SessionService.IsOffline = false;

                List<string>? permittedRestaurantIds = userSnapshot.GetValue<List<string>>("Restaurants");

                if (permittedRestaurantIds != null && permittedRestaurantIds.Any())
                {
                    var restaurants = new List<Restaurant>();
                    foreach (var id in permittedRestaurantIds)
                    {
                        DocumentReference docRef = db.Collection("restaurants").Document(id);
                        DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                        if (snapshot.Exists)
                        {
                            var restaurant = snapshot.ConvertTo<Restaurant>();
                            restaurant.Id = snapshot.Id;
                            restaurants.Add(restaurant);
                        }
                    }
                    SessionService.PermittedRestaurants = restaurants;
                    await LocalStorageService.SaveAsync(restaurants); // Save for offline use

                    if (restaurants.Count == 1)
                    {
                        SessionService.CurrentRestaurant = restaurants[0];
                        SessionService.SaveSession(); // Persist the session
                        if (Application.Current != null) Application.Current.MainPage = new MainShell();
                    }
                    else
                    {
                        SessionService.SaveSession(); // Persist the session before navigating
                        await Navigation.PushAsync(new LocationSelectionPage());
                    }
                }
                else
                {
                    await DisplayAlert("No Locations", "You are not assigned to any locations.", "OK");
                }
            }
            else
            {
                await DisplayAlert("Authentication Failed", "Login failed. Please check your email and password.", "OK");
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
            if (restaurants.Count > 1)
            {
                await Navigation.PushAsync(new LocationSelectionPage());
            }
            else
            {
                SessionService.CurrentRestaurant = restaurants.First();
                if (Application.Current != null) Application.Current.MainPage = new MainShell();
            }
        }
        else
        {
            if (Application.Current != null) Application.Current.MainPage = new MainShell();
        }
    }
}