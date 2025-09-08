using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Plugin.Firebase.Auth;
using Plugin.Firebase.Firestore;

namespace Freecost
{
    public partial class LoginPage : ContentPage
    {
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
            string email = EmailEntry.Text;
            string password = PasswordEntry.Text;

            try
            {
                // Use the Firebase Auth SDK to sign in
                var auth = CrossFirebaseAuth.Current;
                var result = await auth.SignInWithEmailAndPasswordAsync(email, password);
                var user = result.User;

                if (user != null)
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

                    // Fetch user permissions from Firestore
                    var userDocRef = CrossFirebase.Current.Firestore.Collection("users").Document(user.Uid);
                    var userSnapshot = await userDocRef.GetAsync();

                    if (!userSnapshot.Exists)
                    {
                        await DisplayAlert("Authorization Failed", "Login successful, but no permission document was found.", "OK");
                        return;
                    }

                    SessionService.UserUid = user.Uid;
                    SessionService.AuthToken = await user.GetIdTokenAsync(false);
                    SessionService.CurrentUserEmail = email;
                    SessionService.UserRole = userSnapshot.GetValue<string>("role");
                    SessionService.IsOffline = false;

                    var permittedRestaurantIds = userSnapshot.GetValue<List<string>>("Restaurants");

                    if (permittedRestaurantIds != null && permittedRestaurantIds.Any())
                    {
                        var restaurants = new List<Restaurant>();
                        foreach (var id in permittedRestaurantIds)
                        {
                            var docRef = CrossFirebase.Current.Firestore.Collection("restaurants").Document(id);
                            var snapshot = await docRef.GetAsync();
                            if (snapshot.Exists)
                            {
                                var restaurant = snapshot.ToObject<Restaurant>();
                                restaurant.Id = snapshot.Id;
                                restaurants.Add(restaurant);
                            }
                        }
                        SessionService.PermittedRestaurants = restaurants;
                        await LocalStorageService.SaveAsync(restaurants);

                        SessionService.DefaultRestaurantId = restaurants.FirstOrDefault()?.Id;
                        SessionService.CurrentRestaurant = restaurants.FirstOrDefault();
                        SessionService.SaveSession();

                        if (Application.Current != null)
                            Application.Current.MainPage = new MainShell();
                    }
                    else
                    {
                        await DisplayAlert("No Locations", "You are not assigned to any locations.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Login Failed", ex.Message, "OK");
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
            }
            else
            {
                if (Application.Current != null) Application.Current.MainPage = new MainShell();
            }
        }
    }
}