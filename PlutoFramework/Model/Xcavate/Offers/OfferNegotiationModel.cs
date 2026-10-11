using PlutoFramework.Components.Account;
using PlutoFramework.Components.Keys;
using PlutoFramework.Components.Messages;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore.AssetDidComm;
using PlutoFrameworkCore.Xcavate;
using PlutoFrameworkCore.Xcavate.Offers;

namespace PlutoFramework.Model.Xcavate.Offers
{
    /// <summary>
    /// The outcome of <see cref="OfferNegotiationModel.CreateOfferAsync"/>: on success the
    /// messenger is already opening on the new offer bucket; on failure
    /// <see cref="ErrorMessage"/> is the popup-presentable reason (null when a dedicated
    /// popup - no account, no encryption key - already named the problem).
    /// </summary>
    public sealed record OfferCreationResult(bool Success, string? ErrorMessage);

    /// <summary>
    /// The make-offer flow's orchestration: turns one share listing plus the buyer's
    /// price and amount into the offer bucket the negotiation then lives in. One call
    /// generates the bucket's X25519 key, seeds the key-sharing and offer messages
    /// (<see cref="OfferProtocol"/> has the exact payload contract) and asks the profile
    /// API for the bucket - a single signing prompt, since
    /// <see cref="RealXmarketOfferClient.SetupOfferBucketAsync"/> composes the whole
    /// setup server-side - then opens the messenger on it.
    /// </summary>
    public static class OfferNegotiationModel
    {
        /// <summary>
        /// The messenger route that shows one bucket, mirroring the indexed-bucket deep
        /// link format in <c>NotificationDeepLinkModel</c>.
        /// </summary>
        private const string BucketUrlFormat =
            "https://" + MessengerDashboard.Host + "/indexed-bucket/{0}?isHeaderVisible=false&primaryColor=%233B4F74";

        /// <summary>
        /// A fresh X25519 keypair for one offer bucket: the public key as 64 lowercase
        /// hex characters (the SetupOfferBucket mutation's bucketEncryptionKey shape) and
        /// the secret JWK as JSON, ready to embed in the key-sharing document. Generated
        /// through the same BouncyCastle-backed <see cref="X25519Model"/> the account's
        /// own encryption keys use - no new crypto dependency.
        /// </summary>
        public static (string PublicKeyHex, string SecretJwkJson) GenerateBucketKeyPair()
        {
            var (publicKeyHex, secretJwk) = OfferProtocol.GenerateBucketKeyPair();

            return (publicKeyHex, secretJwk.ToJsonString());
        }

        /// <summary>The status message's reference: "status:&lt;shareListingId&gt;:&lt;tx-or-guid&gt;".</summary>
        public static string StatusReference(long shareListingId, string? tx) =>
            $"status:{shareListingId.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{(string.IsNullOrWhiteSpace(tx) ? Guid.NewGuid().ToString() : tx)}";

        /// <summary>
        /// Writes the terminal "purchased" status into the offer bucket after the buy
        /// confirms, so the negotiation view closes for both parties. Strictly
        /// best-effort - the purchase is the source of truth; a failed status write is
        /// logged, never surfaced.
        /// </summary>
        public static async Task WritePurchasedStatusAsync(
            long namespaceId,
            long bucketId,
            XcavateShareListingContext context,
            string buyer,
            string? tx,
            CancellationToken token = default)
        {
            try
            {
                var statusPayload = OfferProtocol.BuildPurchasedStatusPayload(
                    context.ShareListingId, context.AssetId, buyer, tx);

                await DependencyService.Get<RealXmarketOfferClient>().WriteMessageAsync(
                    namespaceId,
                    bucketId,
                    new OfferMessageInput
                    {
                        Reference = StatusReference(context.ShareListingId, tx),
                        Tag = OfferProtocol.StatusTag,
                        IpfsContent = statusPayload,
                        Metadata = new OfferMessageMetadataInput
                        {
                            Description = null,
                            ContentType = OfferProtocol.JsonContentType,
                            ContentHash = OfferProtocol.ContentHash(statusPayload),
                            Properties = null,
                        },
                    },
                    token);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to write the purchased status message (the purchase itself is unaffected): ");
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Creates the offer bucket for <paramref name="context"/> and opens the messenger
        /// on it. <paramref name="pricePerToken"/> is the buyer's offer in pounds per
        /// token; <paramref name="amount"/> the whole tokens wanted. The seller's viewer
        /// key is deliberately NOT resolved here - the server looks it up from the
        /// seller's profile, so the flow works for a seller the app has never met.
        /// </summary>
        /// <remarks>
        /// Gated like <see cref="MessengerAccessModel"/>: without a wallet the account
        /// popup is raised, without the X25519 encryption key the create/import popup is -
        /// the dashboard could not decrypt the bucket for this user otherwise. The
        /// signing prompt being declined, an unresolved viewer key and network failures
        /// all come back as <see cref="OfferCreationResult.ErrorMessage"/>; nothing
        /// throws for a user-recoverable cause.
        /// </remarks>
        public static async Task<OfferCreationResult> CreateOfferAsync(
            XcavateShareListingContext context,
            string propertyName,
            decimal pricePerToken,
            uint amount,
            CancellationToken token = default)
        {
            var buyer = KeysModel.GetSolanaAddress();

            if (string.IsNullOrEmpty(buyer))
            {
                DependencyService.Get<NoAccountPopupViewModel>().IsVisible = true;

                return new OfferCreationResult(false, null);
            }

            if (!await KeysModel.HasEncryptionX25519KeyAsync())
            {
                DependencyService.Get<X25519KeyRequiredPopupViewModel>().IsVisible = true;

                return new OfferCreationResult(false, null);
            }

            // The buyer's own viewer key goes with the request: the server stores profile
            // keys as base64url while its viewer-key resolver reads hex, so leaving the
            // resolution to the server would fail for the app's own users. The key read
            // needs no unlock - the public half derives from the stored secret.
            var encryptionKey = await KeysModel.GetX25519KeyNoAuthAsync();

            if (encryptionKey is null)
            {
                DependencyService.Get<X25519KeyRequiredPopupViewModel>().IsVisible = true;

                return new OfferCreationResult(false, null);
            }

            ulong quote;
            try
            {
                quote = XcavateMarketplaceCallsModel.QuoteFromDecimal(pricePerToken);
            }
            catch (ArgumentOutOfRangeException)
            {
                return new OfferCreationResult(
                    false, "Price per token must be positive and have at most nine decimal places");
            }

            if (quote < XcavateMarketplaceCallsModel.MinSharePriceQuote)
            {
                return new OfferCreationResult(
                    false,
                    $"Price per token must be at least {XcavateQuoteFormatter.Format(XcavateMarketplaceCallsModel.MinSharePriceQuote)}");
            }

            var (bucketKeyHex, secretJwk) = OfferProtocol.GenerateBucketKeyPair();

            var keySharingId = Guid.NewGuid().ToString();

            var keySharingPayload = OfferProtocol.BuildKeySharingPayload(
                keySharingId, buyer, context.Seller, secretJwk);

            var offerPayload = OfferProtocol.BuildOfferPayload(
                context.ShareListingId, context.AssetId, quote, amount, buyer);

            var request = new SetupOfferBucketRequest
            {
                PropertyName = propertyName,
                AssetId = context.AssetId,
                ShareListingId = context.ShareListingId,
                Cluster = SolanaNetworkModel.SelectedCluster,
                Seller = context.Seller,
                BucketEncryptionKey = bucketKeyHex,
                BuyerViewerKey = Convert.ToHexString(encryptionKey.PublicKey).ToLowerInvariant(),
                // Null on purpose: resolved from the seller's profile server-side.
                SellerViewerKey = null,
                KeySharingMessage = new OfferMessageInput
                {
                    Reference = OfferProtocol.KeySharingReference(keySharingId),
                    Tag = null,
                    IpfsContent = keySharingPayload,
                    Metadata = new OfferMessageMetadataInput
                    {
                        Description = null,
                        ContentType = OfferProtocol.JsonContentType,
                        ContentHash = OfferProtocol.ContentHash(keySharingPayload),
                        Properties = null,
                    },
                },
                OfferMessage = new OfferMessageInput
                {
                    Reference = OfferProtocol.OfferReference(context.ShareListingId, buyer),
                    Tag = null,
                    IpfsContent = offerPayload,
                    Metadata = new OfferMessageMetadataInput
                    {
                        Description = null,
                        ContentType = OfferProtocol.JsonContentType,
                        ContentHash = OfferProtocol.ContentHash(offerPayload),
                        Properties = null,
                    },
                },
            };

            long bucketId;
            try
            {
                var (_, newBucketId, _) = await DependencyService.Get<RealXmarketOfferClient>()
                    .SetupOfferBucketAsync(request, token);

                bucketId = newBucketId;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to set up the offer bucket: ");
                Console.WriteLine(ex);

                return new OfferCreationResult(false, OfferSetupErrorMessage(ex));
            }

            // The gate re-runs the wallet/key checks, which pass by construction here;
            // what it adds is the page navigation itself.
            await MessengerAccessModel.TryOpenMessagesAsync(
                string.Format(BucketUrlFormat, bucketId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            return new OfferCreationResult(true, null);
        }

        /// <summary>
        /// The server codes worth translating into guidance; everything else surfaces
        /// with the server's own message attached.
        /// </summary>
        private static string OfferSetupErrorMessage(Exception ex)
        {
            if (ex.Message.Contains("VIEWER_KEY_UNRESOLVED"))
            {
                return "The seller has no encryption key registered yet, so the offer channel cannot be opened. Try again once the seller has used the messenger.";
            }

            if (ex.Message.Contains("SELLER_REQUIRED"))
            {
                return "The listing's seller could not be determined. Reload the property and try again.";
            }

            if (ex.Message.Contains("signing prompt was declined"))
            {
                return "The offer needs your signature to be set up - approve the signing prompt to continue.";
            }

            return $"Could not create the offer: {ex.Message}";
        }
    }
}
