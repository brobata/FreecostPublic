using System;
using System.Collections.Generic;

namespace Freecost
{
    public static class SessionService
    {
        public static string? UserUid { get; set; }
        public static string? AuthToken { get; set; } // Add this line
        private static string? _userRole;
        public static string? UserRole
        {
            get => _userRole;
            set
            {
                if (_userRole != value)
                {
                    _userRole = value;
                    OnRoleChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }
        public static bool IsAdmin => _userRole?.Equals("admin", StringComparison.OrdinalIgnoreCase) ?? false;
        public static event EventHandler? OnRoleChanged;
        public static List<Restaurant>? PermittedRestaurants { get; set; }

        private static Restaurant? _currentRestaurant;
        public static Restaurant? CurrentRestaurant
        {
            get => _currentRestaurant;
            set
            {
                _currentRestaurant = value;
                OnRestaurantChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static event EventHandler? OnRestaurantChanged;

        public static void Clear()
        {
            UserUid = null;
            AuthToken = null; // Add this line
            UserRole = null;
            PermittedRestaurants = null;
            CurrentRestaurant = null;
        }
    }
}