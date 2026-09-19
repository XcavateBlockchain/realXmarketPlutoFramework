using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana;

/// <summary>
/// The thin orange strip appended below the top navigation bars, naming the network the app
/// is talking to. Devnet only: Mainnet is the network a user expects to be on, so it gets no
/// banner, and Testnet is not selectable (<see cref="SolanaNetworkOptions.Selectable"/>).
/// </summary>
public partial class SolanaDevnetWarningView : ContentView
{
    public const double ViewHeight = 20;

    /// <summary>
    /// The height a hosting bar must add while the banner is showing - zero on Mainnet.
    /// </summary>
    public static double ExtraHeight => IsActive ? ViewHeight : 0;

    public static bool IsActive => SolanaNetworkModel.SelectedCluster == SolanaCluster.Devnet;

    public SolanaDevnetWarningView()
    {
        InitializeComponent();

        UpdateVisibility();

        SolanaNetworkModel.ClusterChanged += OnClusterChanged;
    }

    private void OnClusterChanged(object? sender, SolanaCluster cluster)
    {
        // An orphaned view - one left behind when its page was replaced - stays subscribed to
        // the static event forever. Same guard as SolanaBalanceCellView.OnClusterChanged.
        if (Handler is null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(UpdateVisibility);
    }

    private void UpdateVisibility() => IsVisible = IsActive;
}
