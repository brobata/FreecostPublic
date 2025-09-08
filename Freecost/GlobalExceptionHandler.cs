using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace Freecost
{
    public static class GlobalExceptionHandler
    {
        public static void HandleException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception == null) return;

            // Ensure this runs on the main UI thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await ShowErrorAlert(exception);
            });
        }

        private static async Task ShowErrorAlert(Exception ex)
        {
            if (Application.Current?.MainPage == null) return;

            var errorDetails = $"Error: {ex.Message}\n\n" +
                               $"StackTrace:\n{ex.StackTrace}";

            bool copy = await Application.Current.MainPage.DisplayAlert(
                "An Unexpected Error Occurred",
                "An unexpected error occurred. You can copy the details to send to the developer.",
                "Copy Details",
                "OK");

            if (copy)
            {
                await Clipboard.SetTextAsync(errorDetails);
                await Application.Current.MainPage.DisplayAlert("Copied", "Error details have been copied to your clipboard.", "OK");
            }
        }
    }
}