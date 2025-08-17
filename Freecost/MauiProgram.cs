using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using OfficeOpenXml;
using CommunityToolkit.Maui;

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
                        appWindow.Closing += (sender, args) =>
                        {
                            // We need to manually exit the app process on closing.
                            // This is a workaround for a MAUI issue.
                            System.Diagnostics.Process.GetCurrentProcess().Kill();
                        };
                    }
                }));
#endif
            });

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
