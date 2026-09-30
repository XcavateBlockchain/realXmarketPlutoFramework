namespace PlutoFramework.Components
{
    /// <summary>
    /// Z-order layers of the popups the page template stacks over the page content.
    /// A layer that must always sit above another is defined relative to it, so
    /// renumbering the lower one cannot silently bury the upper one.
    /// </summary>
    public static class PopupLayers
    {
        /// <summary>
        /// The Solana transaction status popup. Above the ordinary popup layer (10): a
        /// submission is often the last step of a flow whose own popup just closed, and
        /// the status must never open underneath a stale one. Below the loading overlay
        /// and the Mobile Wallet Adapter signing popup, which the submission itself raises.
        /// </summary>
        public const int TransactionStatus = 15;

        /// <summary>The full-screen loading overlay.</summary>
        public const int Loading = 20;

        /// <summary>
        /// Mobile Wallet Adapter signing popups (transaction and message). Above the
        /// loading overlay: a signature can be requested while something is loading, and
        /// a popup buried under the overlay would leave the request looking frozen.
        /// </summary>
        public const int MwaSigning = Loading + 1;
    }
}
