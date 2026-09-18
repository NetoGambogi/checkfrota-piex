using checkfrota_front.Services;
using checkfrota_front.ViewModels;
using Microsoft.Extensions.Logging;

namespace checkfrota_front
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddHttpClient<ApiAuthService>();

#if ANDROID
            builder.Services.AddSingleton<IGoogleAuthService, checkfrota_front.Platforms.Android.Services.GoogleAuthService>();
#elif WINDOWS
            builder.Services.AddSingleton<IGoogleAuthService, checkfrota_front.Platforms.Windows.Services.GoogleAuthService>();
#endif

            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<Views.LoginPage>();

            builder.Services.AddTransient<PendingApprovalViewModel>();
            builder.Services.AddTransient<Views.PendingApprovalPage>();

            builder.Services.AddTransient<AdminHomeViewModel>();
            builder.Services.AddTransient<Views.AdminHomePage>();

            builder.Services.AddTransient<MotoristaHomeViewModel>();
            builder.Services.AddTransient<Views.MotoristaHomePage>();

            builder.Services.AddTransient<FrotaHomeViewModel>();
            builder.Services.AddTransient<Views.FrotaHomePage>();

            builder.Services.AddTransient<FinanceiroHomeViewModel>();
            builder.Services.AddTransient<Views.FinanceiroHomePage>();

            return builder.Build();
        }
    }
}
