namespace PlutoFramework.Components
{
    /// <summary>
    /// Z-order layers of the popups the page template stacks over the page content.
    /// A layer that must always sit above another is defined relative to it, so
    /// renumbering the lower one cannot silently bury the upper one.
    /// </summary>
    public static class PopupLayers
    {
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
