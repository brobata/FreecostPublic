using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Plugin.Firebase.Auth;

namespace Freecost
{
    public static class AuthService
    {
        public static async Task<bool> RefreshAuthTokenIfNeededAsync()
        {
            var auth = CrossFirebaseAuth.Current;
            if (auth.CurrentUser == null)
            {
                GoToLogin();
                return false;
            }

            try
            {
                var tokenResult = await auth.CurrentUser.GetIdTokenResultAsync(true); // Force refresh
                SessionService.AuthToken = tokenResult.Token;
                SessionService.RefreshToken = auth.CurrentUser.RefreshToken;
                SessionService.SaveSession();
                return true;
            }
            catch (Exception)
            {
                GoToLogin();
                return false;
            }
        }

        private static void GoToLogin()
        {
            SessionService.Clear();
            if (Application.Current != null)
            {
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            }
        }
    }
}

