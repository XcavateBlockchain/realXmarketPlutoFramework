namespace PlutoFramework.Model.Xcavate
{
    /// <summary>
    /// The secondary-market context a property card was tapped from: which share listing
    /// of the property the user is looking at, so the property detail page can offer the
    /// secondary-market actions (make an offer, buy the listed shares) against it instead
    /// of only the primary sale. Carried from the noticeboard card through
    /// <c>XCavatePropertyModel.NavigateToPropertyDetailPageAsync</c> onto
    /// <c>PropertyDetailViewModel.ShareListingContext</c>; null everywhere else (the
    /// marketplace and investor feeds), where the page is a plain primary-sale view.
    /// </summary>
    /// <param name="ShareListingId">The on-chain share-listing id - a separate id space from the primary listings.</param>
    /// <param name="AssetId">The tokenised property's asset id (equal to its primary listing id).</param>
    /// <param name="Seller">The wallet offering the shares.</param>
    /// <param name="SharePriceQuote">The asking price per share, at the 9-decimal price scale (<see cref="XcavateMarketplaceIndexerModel.SharePriceDecimals"/>).</param>
    /// <param name="Amount">Shares this listing still has for sale.</param>
    public sealed record XcavateShareListingContext(
        long ShareListingId,
        long AssetId,
        string Seller,
        ulong SharePriceQuote,
        uint Amount);
}
