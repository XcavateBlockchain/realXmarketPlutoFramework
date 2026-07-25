using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFramework.Model
{
    /// <summary>
    /// Opens <c>solana-wallet:</c> association URIs.
    ///
    /// Android only. The Mobile Wallet Adapter specification is defined in terms of
    /// Android intents for wallet discovery and Digital Asset Links for verifying app
    /// identity, and lists iOS support as planned for a future version. On iOS this
    /// reports itself unsupported so the UI can say so plainly.
    /// </summary>
    public class MwaIntentLauncher : IMwaIntentLauncher
    {
#if ANDROID
        public bool IsSupported => true;

        public Task<bool> LaunchAsync(string associationUri)
        {
            var intent = new Android.Content.Intent(
                Android.Content.Intent.ActionView,
                Android.Net.Uri.Parse(associationUri));

            var activity = Platform.CurrentActivity;

            try
            {
                if (activity is not null)
                {
                    activity.StartActivity(intent);
                }
                else
                {
                    // No foreground activity to attach to, so the intent needs its own task.
                    intent.AddFlags(Android.Content.ActivityFlags.NewTask);

                    Android.App.Application.Context.StartActivity(intent);
                }

                return Task.FromResult(true);
            }
            catch (Android.Content.ActivityNotFoundException)
            {
                // Nothing installed handles the scheme. The user has no compatible wallet,
                // which is a normal situation rather than a failure.
                return Task.FromResult(false);
            }
        }
#else
        public bool IsSupported => false;

        public Task<bool> LaunchAsync(string associationUri) => Task.FromResult(false);
#endif
    }
}
