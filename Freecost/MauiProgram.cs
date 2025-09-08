using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using OfficeOpenXml;
using CommunityToolkit.Maui;
using Plugin.Firebase.Auth;
using Plugin.Firebase.Core.Platforms.iOS;
using Plugin.Firebase.Core.Platforms.Android;


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
            .RegisterFirebaseServices() // Add this
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
                        overlappedPresenter.Maximize();
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
                        var mainScreen = UIScreen.MainScreen.Bounds;
                        nativeWindow.Frame = new CGRect(0, 0, mainScreen.Width, mainScreen.Height);
                    }
                    return true;
                }));
#pragma warning restore CA1422 // Validate platform compatibility
#elif IOS
                events.AddiOS(iOS => iOS.FinishedLaunching((app, launchOptions) => {
                    CrossFirebase.Initialize(app, launchOptions, CreateCrossFirebaseSettings());
                    return false;
                }));
#elif ANDROID
                events.AddAndroid(android => android.OnCreate((activity, bundle) =>
                    CrossFirebase.Initialize(activity, bundle, CreateCrossFirebaseSettings())));
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static CrossFirebaseSettings CreateCrossFirebaseSettings()
    {
        return new CrossFirebaseSettings(
            isAuthEnabled: true,
            isFirestoreEnabled: true);
    }

    private static MauiAppBuilder RegisterFirebaseServices(this MauiAppBuilder builder)
    {
        builder.ConfigureLifecycleEvents(events => {
#if IOS
            events.AddiOS(iOS => iOS.FinishedLaunching((app, launchOptions) => {
                CrossFirebase.Initialize(app, launchOptions, CreateCrossFirebaseSettings());
                return false;
            }));
#elif ANDROID
            events.AddAndroid(android => android.OnCreate((activity, _, __) =>
                CrossFirebase.Initialize(activity, CreateCrossFirebaseSettings())));
#endif
        });

        builder.Services.AddSingleton(_ => CrossFirebaseAuth.Current);
        return builder;
    }
}