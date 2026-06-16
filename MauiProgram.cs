using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UXDivers.Popups.Maui;
namespace MyPosAccurate2026;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            // Initialize the .NET MAUI Community Toolkit by adding the below line of code
            .UseMauiCommunityToolkit()
            .UseUXDiversPopups()
            .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif
#if ANDROID
            // Registrasi khusus untuk Android
            builder.Services.AddSingleton<IBluetoothService, MyPosAccurate2026.Platforms.Android.AndroidBlueToothService>();
#endif

        return builder.Build();
	}
}
