using PlutoFramework.Components.WebView;
using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Solana.Status;

/// <summary>
/// The page a failed Solana transaction toast navigates to, instead of a popup.
/// </summary>
/// <remarks>
/// Binds straight to the toast's <see cref="SolanaTransactionInfo"/>: the page opens from a
/// failed toast and the info object outlives the navigation, and a status that still moves
/// (confirmed-failed settling into finalized-failed) updates the label for free.
/// </remarks>
public partial class SolanaTransactionErrorPage : PageTemplate
{
    private readonly SolanaTransactionInfo info;

    public SolanaTransactionErrorPage(SolanaTransactionInfo info)
    {
        InitializeComponent();

        this.info = info;

        BindingContext = info;
    }

    private async void OnExplorerClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new WebViewPage(info.ExplorerUrl));
    }
}
