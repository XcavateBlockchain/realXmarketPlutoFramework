namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// One open position the investor holds in a marketplace listing: the property (read
    /// through the listing, shaped like <see cref="XcavateSolanaListingNft"/>'s siblings so
    /// the existing views render it) plus how many shares the investor has actually bought
    /// and how many they have only reserved.
    /// <para>
    /// <c>OngoingObjectListingDetails.ShareOwners</c> on the nested listing already carries
    /// the investor's committed total (bought plus reserved), the same figure the detail
    /// page shows, so a wrapped record's <c>TokensBought</c> agrees with it.
    /// </para>
    /// </summary>
    public sealed record XcavateSolanaInvestorProperty
    {
        public required XcavateSolanaListingNft Listing { get; init; }

        /// <summary>Shares actually bought (the position's on-chain <c>share_amount</c>).</summary>
        public required uint BoughtShares { get; init; }

        /// <summary>Shares reserved but not yet bought (the position's on-chain <c>reserved_share_amount</c>).</summary>
        public required uint ReservedShares { get; init; }

        /// <summary>
        /// The symbol of the token this position's shares are priced and paid in.
        /// Every listing sells for tGBP today, so the indexer mapping leaves the
        /// default; when the marketplace sells a listing for another token, this is
        /// what keeps the position's value out of the tGBP buckets.
        /// </summary>
        public string PaymentTokenSymbol { get; init; } = XcavateReserveBalanceModel.TgBpSymbol;

        /// <summary>Bought plus reserved: the shares the investor has committed money to.</summary>
        public uint CommittedShares => (uint)Math.Clamp(BoughtShares + ReservedShares, 0, uint.MaxValue);
    }
}
