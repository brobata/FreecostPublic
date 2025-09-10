using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Freecost
{
    public static class AuthService
    {
        private static readonly HttpClient client = new HttpClient();
        private const string WebApiKey = "AIzaSyCmS_d7lw9z-mdtNNoz8JafWsI1iprKRM0"; // Your Firebase Web API Key

        public static async Task<bool> RefreshAuthTokenIfNeededAsync()
        {
            if (string.IsNullOrEmpty(SessionService.RefreshToken))
            {
                // No refresh token available, user needs to log in.
                await GoToLogin();
                return false;
            }

            string refreshUrl = $"https://securetoken.googleapis.com/v1/token?key={WebApiKey}";

            try
            {
                var requestData = new { grant_type = "refresh_token", refresh_token = SessionService.RefreshToken };
                var jsonContent = JsonSerializer.Serialize(requestData);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(refreshUrl, httpContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var newAuthData = JsonDocument.Parse(responseJson).RootElement;

                    // Update the session with the new tokens
                    SessionService.AuthToken = newAuthData.GetProperty("id_token").GetString();
                    SessionService.RefreshToken = newAuthData.GetProperty("refresh_token").GetString();
                    SessionService.SaveSession(); // Save the new, valid tokens
                    return true; // Indicate success
                }
                else
                {
                    // The refresh token is invalid or expired. Clear session and force re-login.
                    await GoToLogin();
                    return false; // Indicate failure
                }
            }
            catch (Exception)
            {
                // Network error or other issue, force re-login.
                await GoToLogin();
                return false; // Indicate failure
            }
        }

        private static async Task GoToLogin()
        {
            await SessionService.HandleSessionExpirationAsync();
            if (Application.Current != null)
            {
                // Ensure this runs on the main UI thread
                Application.Current.Dispatcher.Dispatch(async () =>
                {
                    if (Application.Current.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert("Session Expired", "Your session has expired. You have been logged out and are now in offline mode.", "OK");
                    }
                });
            }
        }
    }
}

