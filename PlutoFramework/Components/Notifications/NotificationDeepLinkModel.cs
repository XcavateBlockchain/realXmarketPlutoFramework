using PlutoFramework.Components.Messages;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Notifications;

/// <summary>
/// The deep link carried by a tapped push notification. A tray tap can arrive before
/// any shell exists (cold start), so the target is stashed here and consumed once the
/// main shell is up - immediately for taps on a running app.
/// </summary>
public static class NotificationDeepLinkModel
{
    private const string BucketUrlFormat =
        "https://realxmessenger.xcavate.io/indexed-bucket/{0}?isHeaderVisible=false&primaryColor=%233B4F74";

    private static string? pendingBucketId;

    /// <summary>Stashes the bucket from a tap intent and tries to open it right away.</summary>
    public static void SetBucket(string? bucketId)
    {
        if (string.IsNullOrWhiteSpace(bucketId))
        {
            return;
        }

        pendingBucketId = bucketId;

        try
        {
            _ = TryOpenPendingAsync();
        }
        catch (Exception e)
        {
            // Called from Android activity callbacks - a broken deep link must
            // never take down the launch.
            Console.WriteLine($"[PlutoNotifications] Deep link handling failed: {e.Message}");
        }
    }

    /// <summary>
    /// Opens the pending deep link if the app is in a state to show it. With no shell
    /// yet (still booting behind the loading page) the link stays pending for the
    /// caller that runs after the shell is set. The drop-gate below must mirror
    /// `App.InitializeAsync`'s shell-selection predicate exactly: a user who has not
    /// finished onboarding, or has finished onboarding but holds no wallet key, is
    /// routed to `OnboardingShell`, not the messenger, so their link is dropped - not
    /// kept, or it would fire out of nowhere once the missing condition is met later.
    /// </summary>
    public static Task TryOpenPendingAsync()
    {
        var bucketId = pendingBucketId;

        if (bucketId is null || Shell.Current is null)
        {
            return Task.CompletedTask;
        }

        if (!OnboardingModel.IsOnboardingCompleted() || !(KeysModel.HasSolanaKey() || KeysModel.HasSubstrateKey()))
        {
            pendingBucketId = null;

            return Task.CompletedTask;
        }

        // Cleared before navigating so one tap can never navigate twice.
        pendingBucketId = null;

        var url = string.Format(BucketUrlFormat, Uri.EscapeDataString(bucketId));

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                await Shell.Current.Navigation.PushAsync(new MessageWebViewPage(url));
            }
            catch (Exception e)
            {
                // A lost deep link must never take down startup.
                Console.WriteLine($"[PlutoNotifications] Deep link navigation failed: {e.Message}");
            }
        });
    }
}
