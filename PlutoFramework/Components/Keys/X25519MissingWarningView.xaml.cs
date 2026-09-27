using PlutoFramework.Model;

namespace PlutoFramework.Components.Keys;

/// <summary>
/// The thin orange strip appended below the top navigation bars, shown while the account
/// has no X25519 encryption key - the key the messaging dashboard decrypts with.
/// </summary>
public partial class X25519MissingWarningView : ContentView
{
    public const double ViewHeight = 20;

    /// <summary>
    /// The height a hosting bar must add while the banner is showing - zero when a key exists.
    /// </summary>
    public static double ExtraHeight => IsActive ? ViewHeight : 0;

    public static bool IsActive => X25519WarningModel.IsActive;

    public X25519MissingWarningView()
    {
        InitializeComponent();

        UpdateVisibility();

        X25519WarningModel.AvailabilityChanged += OnAvailabilityChanged;

        // The cache only moves through RefreshAsync, so a view created after the last key
        // operation has to ask for the current answer itself.
        _ = X25519WarningModel.RefreshAsync();
    }

    private void OnAvailabilityChanged(object? sender, EventArgs e)
    {
        // An orphaned view - one left behind when its page was replaced - stays subscribed to
        // the static event forever. Same guard as SolanaDevnetWarningView.OnClusterChanged.
        if (Handler is null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(UpdateVisibility);
    }

    private void UpdateVisibility() => IsVisible = IsActive;
}
