using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using PlutoFramework.Components.DAppConnection;
using PlutoFramework.Model;

namespace PlutoFramework.Platforms.Android
{
    /// <summary>
    /// Sources:
    /// https://learn.microsoft.com/en-us/previous-versions/xamarin/android/app-fundamentals/services/foreground-services,
    /// https://www.youtube.com/watch?v=-eyKlpJI02o,
    /// https://www.youtube.com/watch?v=Q_renpfnbk4,
    /// </summary>
    [Service(ForegroundServiceType = ForegroundService.TypeRemoteMessaging)]
    class PlutonicationAndroidForegroundService : Service
    {
        CancellationTokenSource? _cts;
        public const int SERVICE_RUNNING_NOTIFICATION_ID = 96063; // Random id

        public override IBinder OnBind(Intent? intent)
        {
            return null!;
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            _cts = new CancellationTokenSource();

            var dAppConnectionViewModel = DependencyService.Get<DAppConnectionViewModel>();

            Notification notification = new AndroidNotificationHelper().GetNotification($"Connected to {dAppConnectionViewModel.Name}", "Connected securely via Plutonication");

            if ((int)global::Android.OS.Build.VERSION.SdkInt >= 34) // API 34 = UPSIDE_DOWN_CAKE
            {
                #pragma warning disable CA1416 // ForegroundService.TypeRemoteMessaging is Android API 34+; an API-level guard would change behavior on API 29-33
                StartForeground(SERVICE_RUNNING_NOTIFICATION_ID, notification, ForegroundService.TypeRemoteMessaging);
                #pragma warning restore CA1416
            }
            else
            {
                StartForeground(SERVICE_RUNNING_NOTIFICATION_ID, notification);
            }

            _ = Task.Run(() =>
            {
                Task accept = PlutonicationModel.AcceptConnectionAsync();
            }, _cts.Token);

            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            if (_cts != null)
            {
                _cts.Token.ThrowIfCancellationRequested();
                _cts.Cancel();
            }

            base.OnDestroy();
        }
    }
}
