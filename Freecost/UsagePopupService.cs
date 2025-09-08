using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace Freecost
{
    public static class UsagePopupService
    {
        private const string AppOpenCountKey = "AppOpenCount";
        private const string PayPalUrl = "https://paypal.me/FreeCostApp";

        public static async Task CheckAndShowPopupAsync()
        {
            // Get the current open count, increment it, and save it back.
            int openCount = Preferences.Get(AppOpenCountKey, 0);
            openCount++;
            Preferences.Set(AppOpenCountKey, openCount);

            // Determine if the popup should be shown based on the open count.
            bool shouldShowPopup = openCount == 1 || openCount == 5 || (openCount > 5 && (openCount - 5) % 10 == 0);

            if (shouldShowPopup)
            {
                // Wait for 5 seconds before showing the popup
                await Task.Delay(5000);

                string? restaurantId = SessionService.CurrentRestaurant?.Id;
                if (restaurantId == null)
                {
                    return; // Don't show if no restaurant is selected
                }

                // Load the data to get the counts.
                var recipes = await LocalStorageService.LoadAsync<Recipe>(restaurantId);
                var entrees = await LocalStorageService.LoadAsync<Entree>(restaurantId);

                int recipeCount = recipes.Count;
                int entreeCount = entrees.Count;

                // Construct the message for the popup.
                string message = $"Thank you for using Freecost!\n\n" +
                                 $"You've saved:\n" +
                                 $"- {recipeCount} recipes\n" +
                                 $"- {entreeCount} entrees\n\n" +
                                 $"If you find this app helpful, please consider supporting its development.";

                // Show the alert and wait for the user's choice.
                if (Shell.Current?.CurrentPage != null)
                {
                    bool buyCoffee = await Shell.Current.CurrentPage.DisplayAlert(
                        "Support Freecost",
                        message,
                        "Buy me a coffee",
                        "Maybe later");

                    if (buyCoffee)
                    {
                        try
                        {
                            Uri uri = new Uri(PayPalUrl);
                            await Browser.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
                        }
                        catch (Exception)
                        {
                            // Handle potential errors, like no browser being available.
                            await Shell.Current.CurrentPage.DisplayAlert("Error", "Could not open the link.", "OK");
                        }
                    }
                }
            }
        }
    }
}