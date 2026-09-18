using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// One row in the token detail page's reserved-for-properties list: the property the
    /// shares belong to, how many are reserved, and the payment token value they bind.
    /// Tapping the row opens the property detail page for <see cref="Listing"/>.
    /// </summary>
    public sealed class ReservedPropertyItem
    {
        /// <summary>The position's listing - what the row navigates to.</summary>
        public required XcavateSolanaListingNft Listing { get; init; }

        public required string NameText { get; init; }

        /// <summary>"3 tokens · AB1 2CD" - the reserved share count plus the best known location.</summary>
        public required string SubtitleText { get; init; }

        /// <summary>The position's reserved value, formatted with the payment token's symbol.</summary>
        public required string ValueText { get; init; }

        /// <summary>The property's first image URL, or the bundled placeholder when none is known.</summary>
        public required string ImageSource { get; init; }
    }
}
