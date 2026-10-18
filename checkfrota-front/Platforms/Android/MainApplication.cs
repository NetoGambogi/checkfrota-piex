using Android.App;
using Android.Runtime;
using checkfrota_front.Platforms.Android.Services;

namespace checkfrota_front
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
            NotificacoesAndroid.CriarCanal(this);
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
