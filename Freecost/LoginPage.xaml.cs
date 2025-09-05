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
        db = FirestoreService.Db;
        if (db == null)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert("Database Error", "Could not connect to the database.", "OK");
            }
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
                    Preferences.Set("RememberMe", false);
                    Preferences.Set("Email", string.Empty);
                    Preferences.Set("Password", string.Empty);
                }

                var authData = JsonConvert.DeserializeObject<dynamic>(responseJson);
                string? userUid = authData?.localId.ToString();
                string? idToken = authData?.idToken.ToString(); // Capture the token

                // Add this line to print the token
                System.Diagnostics.Debug.WriteLine($"FIREBASE_TOKEN: {idToken}");

                if (userUid == null)
                {
                    if (Application.Current?.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert("Authentication Failed", "Login failed. Could not get user ID.", "OK");
                    }
                    return;
                }

                DocumentReference userDocRef = db.Collection("users").Document(userUid);
                DocumentSnapshot userSnapshot = await userDocRef.GetSnapshotAsync();

                if (!userSnapshot.Exists)
                {
                    if (Application.Current?.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert("Authorization Failed", "Login successful, but no permission document was found.", "OK");
                    }
                    return;
                }

                SessionService.UserUid = userUid;
                SessionService.AuthToken = idToken; // Save the token
                SessionService.UserRole = userSnapshot.GetValue<string>("role");
                List<string>? permittedRestaurantIds = userSnapshot.GetValue<List<string>>("Restaurants");

                if (permittedRestaurantIds != null && permittedRestaurantIds.Count > 0)
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

                    if (restaurants.Count == 1)
                    {
                        SessionService.CurrentRestaurant = restaurants[0];
                        if (Application.Current != null)
                        {
                            Application.Current.MainPage = new MainShell();
                        }
                    }
                    else
                    {
                        await Navigation.PushAsync(new LocationSelectionPage());
                    }
                }
                else
                {
                    if (Application.Current?.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert("No Locations", "You are not assigned to any locations.", "OK");
                    }
                }
            }
            else
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Authentication Failed", "Login failed. Please check your email and password.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "An unexpected error occurred: " + ex.Message, "OK");
            }
        }
    }
}