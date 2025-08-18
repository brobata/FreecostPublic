using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using OfficeOpenXml;
using CommunityToolkit.Maui;

#if MACCATALYST
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
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

                    if (appWindow is not null)
                    {
                        const int WindowWidth = 800;
                        const int WindowHeight = 700;
                        appWindow.Resize(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight));

                        appWindow.Closing += (sender, args) =>
                        {
                            // We need to manually exit the app process on closing.
                            // This is a workaround for a MAUI issue.
                            System.Diagnostics.Process.GetCurrentProcess().Kill();
                        };
                    }
                }));
#elif MACCATALYST
#pragma warning disable CA1422 // Validate platform compatibility
                events.AddiOS(ios => ios.FinishedLaunching((app, options) => {
                    var nativeWindow = app.Windows.FirstOrDefault();
                    if (nativeWindow != null)
                    {
                        const int newWidth = 900;
                        const int newHeight = 650;

                        var mainScreen = UIScreen.MainScreen.Bounds;
                        var newX = (mainScreen.Width - newWidth) / 2;
                        var newY = (mainScreen.Height - newHeight) / 2;

                        nativeWindow.Frame = new CGRect(newX, newY, newWidth, newHeight);
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