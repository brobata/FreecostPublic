using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using OfficeOpenXml;
using CommunityToolkit.Maui;

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#elif MACCATALYST
using UIKit;
using CoreGraphics;
#endif

namespace Freecost;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        ExcelPackage.License.SetNonCommercialOrganization("Freecost");
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows => windows.OnWindowCreated(window =>
                {
                    window.ExtendsContentIntoTitleBar = false;
                    var handle = WindowNative.GetWindowHandle(window);
                    var id = Win32Interop.GetWindowIdFromWindow(handle);
                    var appWindow = AppWindow.GetFromWindowId(id);

                    if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
                    {
                        overlappedPresenter.Maximize(); // This will maximize the window
                    }

                    appWindow.Closing += (sender, args) =>
                    {
                        System.Diagnostics.Process.GetCurrentProcess().Kill();
                    };
                }));
#elif MACCATALYST
#pragma warning disable CA1422 // Validate platform compatibility
                events.AddiOS(ios => ios.FinishedLaunching((app, options) => {
                    var nativeWindow = app.Windows.FirstOrDefault();
                    if (nativeWindow != null)
                    {
                        // This will make the window a size that is appropriate for the screen
                        var mainScreen = UIScreen.MainScreen.Bounds;
                        nativeWindow.Frame = new CGRect(0, 0, mainScreen.Width, mainScreen.Height);
                    }
                    return true;
                }));
#pragma warning restore CA1422 // Validate platform compatibility
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}