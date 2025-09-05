using System;
using System.Collections.Generic;
using System.Linq;

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#elif MACCATALYST
using UIKit;
using CoreGraphics;
#endif

namespace Freecost;

public partial class LocationSelectionPage : ContentPage
{
    public LocationSelectionPage()
    {
        InitializeComponent();
        LocationPicker.ItemsSource = SessionService.PermittedRestaurants;
        LocationPicker.ItemDisplayBinding = new Binding("Name");
        if (SessionService.PermittedRestaurants?.Count > 0)
        {
            LocationPicker.SelectedIndex = 0;
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (LocationPicker.SelectedItem is Restaurant selectedRestaurant)
        {
            SessionService.CurrentRestaurant = selectedRestaurant;
            if (Application.Current != null)
            {
#if WINDOWS
                var window = Application.Current.Windows[0];
                var nativeWindow = window.Handler.PlatformView;
                var windowHandle = WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
                {
                    overlappedPresenter.Maximize();
                }
#elif MACCATALYST
                var window = Application.Current.Windows.FirstOrDefault();
                if (window?.Handler?.PlatformView is UIKit.UIWindow nativeWindow)
                {
                    var mainScreen = UIKit.UIScreen.MainScreen.Bounds;
                    nativeWindow.Frame = mainScreen;
                }
#endif
                Application.Current.MainPage = new MainShell();
                await UsagePopupService.CheckAndShowPopupAsync();
            }
        }
    }
}
