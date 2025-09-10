using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Freecost
{
    public static class SessionService
    {
        // Constants for Offline Mode
        public const string LocalDataId = "LocalData";
        public static readonly Restaurant LocalDataRestaurant = new Restaurant { Id = LocalDataId, Name = "Local Data" };

        // Properties
        public static string? UserUid { get; set; }
        public static string? AuthToken { get; set; }
        public static string? RefreshToken { get; set; }
        public static string? UserRole { get; set; }
        public static string? CurrentUserEmail { get; set; }
        public static List<Restaurant>? PermittedRestaurants { get; set; }

        private static Restaurant? _currentRestaurant;
        public static Restaurant? CurrentRestaurant
        {
            get => _currentRestaurant;
            set
            {
                if (_currentRestaurant != value)
                {
                    _currentRestaurant = value;
                    if (!IsOffline && _currentRestaurant != null)
                    {
                        // Save the last used ONLINE restaurant ID
                        LastOnlineRestaurantId = _currentRestaurant.Id;
                    }
                    OnStaticPropertyChanged();
                    OnStaticPropertyChanged(nameof(IsOffline)); // Notify IsOffline changes too
                }
            }
        }
        public static string? LastOnlineRestaurantId { get; set; }

        // UI-Bound Properties
        public static bool IsOffline => CurrentRestaurant?.Id == LocalDataId;
        public static bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);
        public static bool IsNotLoggedIn => !IsLoggedIn;
        public static bool IsAdmin => UserRole?.Equals("admin", StringComparison.OrdinalIgnoreCase) ?? false;
        public static string StatusText => IsLoggedIn ? $"Logged in as {CurrentUserEmail}" : "Offline Mode";

        // Events for UI updates
        public static event PropertyChangedEventHandler? StaticPropertyChanged;
        private static void OnStaticPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }

        private static async Task SyncCurrentRestaurantDataAsync()
        {
            if (IsOffline || string.IsNullOrEmpty(AuthToken)) return;

            var restaurantId = CurrentRestaurant?.Id;
            if (restaurantId == null || restaurantId == LocalDataId) return;

            var localData = await LocalStorageService.GetAllDataAsync(restaurantId);
            var tasks = new List<Task>();

            foreach (var item in localData.Ingredients)
            {
                if (item.Id != null)
                    tasks.Add(FirestoreService.SetDocumentAsync($"restaurants/{restaurantId}/ingredients/{item.Id}", item, AuthToken));
            }
            foreach (var item in localData.Recipes)
            {
                if (item.Id != null)
                    tasks.Add(FirestoreService.SetDocumentAsync($"recipes/{item.Id}", item, AuthToken));
            }
            foreach (var item in localData.Entrees)
            {
                if (item.Id != null)
                    tasks.Add(FirestoreService.SetDocumentAsync($"entrees/{item.Id}", item, AuthToken));
            }
            await Task.WhenAll(tasks);
        }

        public static async Task LogoutAsync()
        {
            // 1. Sync data before logging out
            await SyncCurrentRestaurantDataAsync();

            // 2. Clear session credentials but keep local data cache
            UserUid = null;
            AuthToken = null;
            RefreshToken = null;
            UserRole = null;
            CurrentUserEmail = null;
            PermittedRestaurants = null;

            Preferences.Remove("AuthToken");
            Preferences.Remove("RefreshToken");
            Preferences.Remove("UserUid");
            Preferences.Remove("UserRole");
            Preferences.Remove("CurrentUserEmail");
            Preferences.Remove("PermittedRestaurants");
            Preferences.Remove("CurrentRestaurant");

            Preferences.Remove("RememberMe");
            Preferences.Remove("Email");
            Preferences.Remove("Password");

            // 3. Switch to offline mode
            CurrentRestaurant = LocalDataRestaurant;

            NotifyStateChanged();
        }

        public static async Task HandleSessionExpirationAsync()
        {
            UserUid = null;
            AuthToken = null;
            RefreshToken = null;
            UserRole = null;
            CurrentUserEmail = null;
            PermittedRestaurants = null;

            Preferences.Remove("AuthToken");
            Preferences.Remove("RefreshToken");
            Preferences.Remove("UserUid");
            Preferences.Remove("UserRole");
            Preferences.Remove("CurrentUserEmail");
            Preferences.Remove("PermittedRestaurants");
            Preferences.Remove("CurrentRestaurant");

            Preferences.Remove("RememberMe");
            Preferences.Remove("Email");
            Preferences.Remove("Password");

            CurrentRestaurant = LocalDataRestaurant;

            // Clear local caches of ONLINE data, as we cannot sync
            await LocalStorageService.ClearAllOnlineDataAsync();

            NotifyStateChanged();
        }

        public static void InitializeAsOffline()
        {
            // For first-time launch or when no user is logged in.
            CurrentRestaurant = LocalDataRestaurant;
            NotifyStateChanged();
        }

        public static void StartOfflineSession()
        {
            // For when the user explicitly clicks "Work Offline".
            CurrentRestaurant = LocalDataRestaurant;
            NotifyStateChanged();
        }

        public static void RestoreSession()
        {
            AuthToken = Preferences.Get("AuthToken", string.Empty);
            RefreshToken = Preferences.Get("RefreshToken", string.Empty);
            UserUid = Preferences.Get("UserUid", string.Empty);
            UserRole = Preferences.Get("UserRole", string.Empty);
            CurrentUserEmail = Preferences.Get("CurrentUserEmail", string.Empty);
            LastOnlineRestaurantId = Preferences.Get("LastOnlineRestaurantId", string.Empty);

            var restaurantsJson = Preferences.Get("PermittedRestaurants", string.Empty);
            if (!string.IsNullOrEmpty(restaurantsJson))
                PermittedRestaurants = JsonSerializer.Deserialize<List<Restaurant>>(restaurantsJson);

            // CurrentRestaurant is set after login or in App.xaml.cs startup logic
            NotifyStateChanged();
        }

        public static void SaveSession()
        {
            Preferences.Set("AuthToken", AuthToken);
            Preferences.Set("RefreshToken", RefreshToken);
            Preferences.Set("UserUid", UserUid);
            Preferences.Set("UserRole", UserRole);
            Preferences.Set("CurrentUserEmail", CurrentUserEmail);
            Preferences.Set("LastOnlineRestaurantId", LastOnlineRestaurantId);

            if (PermittedRestaurants != null)
                Preferences.Set("PermittedRestaurants", JsonSerializer.Serialize(PermittedRestaurants));

            if (CurrentRestaurant != null && !IsOffline)
                Preferences.Set("CurrentRestaurant", JsonSerializer.Serialize(CurrentRestaurant));
            else
                Preferences.Remove("CurrentRestaurant");
        }

        public static void NotifyStateChanged()
        {
            OnStaticPropertyChanged(nameof(IsLoggedIn));
            OnStaticPropertyChanged(nameof(IsNotLoggedIn));
            OnStaticPropertyChanged(nameof(IsAdmin));
            OnStaticPropertyChanged(nameof(StatusText));
            OnStaticPropertyChanged(nameof(CurrentRestaurant));
        }
    }
}

