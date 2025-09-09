using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Freecost
{
    public static class SessionService
    {
        // Properties
        public static string? UserUid { get; set; }
        public static string? AuthToken { get; set; }
        public static string? RefreshToken { get; set; } // Added
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
                    OnStaticPropertyChanged();
                }
            }
        }
        public static bool IsOffline { get; set; }
        public static string? DefaultRestaurantId { get; set; }

        public static bool ShowConnectionErrorOnNextLoad { get; set; } = false;
        // UI-Bound Properties
        public static bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);
        public static bool IsNotLoggedIn => !IsLoggedIn;
        public static bool IsAdmin => UserRole?.Equals("admin", StringComparison.OrdinalIgnoreCase) ?? false;
        public static string StatusText => IsLoggedIn ? $"Logged in as {CurrentUserEmail}" : (IsOffline ? "Offline Mode" : "Not Logged In");

        // Events for UI updates
        public static event PropertyChangedEventHandler? StaticPropertyChanged;
        private static void OnStaticPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }

        public static void RestoreSession()
        {
            AuthToken = Preferences.Get("AuthToken", string.Empty);
            RefreshToken = Preferences.Get("RefreshToken", string.Empty); // Added
            UserUid = Preferences.Get("UserUid", string.Empty);
            UserRole = Preferences.Get("UserRole", string.Empty);
            CurrentUserEmail = Preferences.Get("CurrentUserEmail", string.Empty);
            DefaultRestaurantId = Preferences.Get("DefaultRestaurantId", string.Empty);
            IsOffline = false;

            var restaurantsJson = Preferences.Get("PermittedRestaurants", string.Empty);
            if (!string.IsNullOrEmpty(restaurantsJson))
                PermittedRestaurants = JsonSerializer.Deserialize<List<Restaurant>>(restaurantsJson);

            var currentRestaurantJson = Preferences.Get("CurrentRestaurant", string.Empty);
            if (!string.IsNullOrEmpty(currentRestaurantJson))
                CurrentRestaurant = JsonSerializer.Deserialize<Restaurant>(currentRestaurantJson);

            NotifyStateChanged();
        }

        public static void StartOfflineSession()
        {
            Clear();
            IsOffline = true;
            NotifyStateChanged();
        }

        public static void SaveSession()
        {
            Preferences.Set("AuthToken", AuthToken);
            Preferences.Set("RefreshToken", RefreshToken); // Added
            Preferences.Set("UserUid", UserUid);
            Preferences.Set("UserRole", UserRole);
            Preferences.Set("CurrentUserEmail", CurrentUserEmail);
            Preferences.Set("DefaultRestaurantId", DefaultRestaurantId);

            if (PermittedRestaurants != null)
                Preferences.Set("PermittedRestaurants", JsonSerializer.Serialize(PermittedRestaurants));

            if (CurrentRestaurant != null)
                Preferences.Set("CurrentRestaurant", JsonSerializer.Serialize(CurrentRestaurant));
        }

        public static void Clear()
        {
            UserUid = null;
            AuthToken = null;
            RefreshToken = null; // Added
            UserRole = null;
            CurrentUserEmail = null;
            PermittedRestaurants = null;
            CurrentRestaurant = null;
            IsOffline = false;
            DefaultRestaurantId = null;
            Preferences.Remove("AuthToken");

            Preferences.Remove("AuthToken");
            Preferences.Remove("RefreshToken"); // Added
            Preferences.Remove("UserUid");
            Preferences.Remove("UserRole");
            Preferences.Remove("CurrentUserEmail");
            Preferences.Remove("PermittedRestaurants");
            Preferences.Remove("CurrentRestaurant");
            Preferences.Remove("DefaultRestaurantId");

            NotifyStateChanged();
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