using PlutoFramework.Components.Solana.Status;
using PlutoFramework.Model;
using PlutoFramework.Model.Solana;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using SolanaPublicKey = Solnet.Wallet.PublicKey;

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// Submits a marketplace program call the way the Solana transfer flow submits a
    /// transfer: status toast registered before any slow work, instructions built for
    /// the signing wallet, sent on the marketplace's own cluster, then tracked to
    /// confirmation. The replacement for the Substrate extrinsic pipeline
    /// (TransactionAnalyzer + extrinsic status stack) on the property pages. When the
    /// program additionally requires the rent collector's signature (reserve, buy and claim),
    /// that signature comes from the profile API - which holds the rent collector key -
    /// and the investor signs and submits the same compiled message.
    /// </summary>
    public static class XcavateMarketplaceTransactionModel
    {
        /// <summary>
        /// Raised on the main thread the first time a transaction submitted through
        /// <see cref="SubmitAsync"/> reaches a confirmed-success state. Scoped to the
        /// marketplace program's transactions - unlike
        /// <see cref="SolanaTransactionTracker.TransactionConfirmed"/>, which also fires
        /// for plain transfers - so the property pages re-read only when their data can
        /// actually have changed on-chain.
        /// </summary>
        /// <remarks>
        /// Static, so every subscriber must unsubscribe or it keeps their view model alive
        /// - the same trap <c>SolanaNetworkModel.ClusterChanged</c> documents. The two
        /// session-wide singleton list view models (investor main page, marketplace) never
        /// unsubscribe because nothing ever disposes them anyway.
        /// </remarks>
        public static event EventHandler? TransactionConfirmed;

        /// <summary>
        /// Builds and submits one marketplace transaction.
        /// <paramref name="buildInstructionsAsync"/> receives the signing wallet's
        /// address - the investor/confirmer the program instructions are keyed by.
        /// </summary>
        public static async Task SubmitAsync(
            string description,
            Func<string, CancellationToken, Task<List<TransactionInstruction>>> buildInstructionsAsync)
        {
            // Deliberately the marketplace's cluster, not the app-wide selection: the
            // listing being acted on came from this deployment, whatever network the
            // user picked for their wallet.
            var cluster = XcavateMarketplaceCallsModel.MarketplaceCluster;

            var stack = DependencyService.Get<SolanaTransactionStatusStackViewModel>();

            // Registered before anything slow, so the user sees the action acknowledged
            // the moment they tap rather than after an unlock prompt and a round trip.
            var info = stack.Register(description, cluster);

            try
            {
                var address = KeysModel.GetSolanaAddress();

                if (string.IsNullOrEmpty(address))
                {
                    info.Status = SolanaTransactionStatus.Error;
                    info.ErrorMessage = "No Solana account is set up in this wallet.";

                    return;
                }

                // Built before the key is unlocked: a build failure (no position, closed
                // listing, marketplace not deployed) should not cost an unlock prompt.
                var instructions = await buildInstructionsAsync(address, CancellationToken.None);

                // Reserve, buy and claim all take a rent-fronting payer that the
                // deployed program pins to the config's rent collector AND requires
                // to have signed (devnet-verified), so each genuinely needs two
                // signatures: the investor's and the rent collector's. When this
                // wallet IS the rent collector the two roles collapse into one
                // signer; otherwise the rent collector's half comes from the profile
                // API, which holds that key (this app holds none).
                var signerKeys = instructions
                    .SelectMany(instruction => instruction.Keys)
                    .Where(accountMeta => accountMeta.IsSigner)
                    .Select(accountMeta => accountMeta.PublicKey)
                    .Concat(new[] { address })
                    .Distinct()
                    .ToList();

                var account = await PlutoFrameworkSolanaAccount.ResolveAsync(description, CancellationToken.None);

                if (account is null)
                {
                    // No key, or the unlock prompt was declined. Either way the toast
                    // must not sit at Submitting forever.
                    info.Status = SolanaTransactionStatus.Error;
                    info.ErrorMessage = "No Solana account is set up in this wallet, or the unlock prompt was declined.";

                    return;
                }

                string signature;

                if (signerKeys.Count == 1)
                {
                    // The one-signer path (this wallet IS the rent collector): byte
                    // for byte the way it has always worked.
                    signature = await account.SendAsync(instructions, description, CancellationToken.None, cluster);
                }
                else if (signerKeys.Count == 2)
                {
                    signature = await SubmitTwoSignerAsync(account, cluster, instructions, description);
                }
                else
                {
                    // More than two distinct signers can never be satisfied by this
                    // flow (this wallet plus the rent collector) - fail now, before
                    // an unlock prompt and a wallet round trip, with the real reason.
                    info.Status = SolanaTransactionStatus.Error;
                    info.ErrorMessage = $"This transaction needs {signerKeys.Count} signatures and this flow can provide at most two. Nothing was signed or submitted.";

                    return;
                }

                info.Signature = signature;
                info.Status = SolanaTransactionStatus.Pending;

                // The per-transaction callback raises the marketplace-scoped event: a
                // reserve, buy or claim that lands changes every listing figure on screen,
                // while a plain transfer (which only the tracker's own event reports) must
                // not cost the property pages a re-query.
                _ = SolanaTransactionTracker.TrackAsync(
                    signature, cluster, info, CancellationToken.None,
                    onConfirmed: _ => TransactionConfirmed?.Invoke(null, EventArgs.Empty));
            }
            catch (Exception ex)
            {
                // The toast is the failure report now: its error page shows this message,
                // so no popup alongside it.
                info.Status = SolanaTransactionStatus.Error;
                info.ErrorMessage = ex.Message;
            }
        }

        /// <summary>
        /// The reserve, buy and claim path: the program pins the rent-fronting payer to the rent
        /// collector AND requires its signature (devnet-verified), and this wallet is
        /// not the rent collector. The rent collector's half comes from the profile
        /// API - which holds the key - pre-applied to the wire transaction, then this
        /// wallet signs and submits the same bytes, so both signatures land on one
        /// message.
        /// </summary>
        private static async Task<string> SubmitTwoSignerAsync(
            PlutoFrameworkSolanaAccount account,
            SolanaCluster cluster,
            List<TransactionInstruction> instructions,
            string description)
        {
            // The investor's key always signs, so the other required signer - exactly
            // one, the caller verified the count - is the rent collector.
            var investor = account.Address;

            var rentCollector = instructions
                .SelectMany(instruction => instruction.Keys)
                .Where(accountMeta => accountMeta.IsSigner)
                .Select(accountMeta => accountMeta.PublicKey)
                .Concat(new[] { investor })
                .Distinct()
                .Single(key => key != investor);

            // The rent collector fronts the rent, so it is the fee payer - the program
            // rejects a buy or claim whose payer is anything else.
            var blockHash = await SolanaRpcModel.GetLatestBlockHashAsync(cluster, CancellationToken.None);

            var builder = new TransactionBuilder()
                .SetRecentBlockHash(blockHash)
                .SetFeePayer(new SolanaPublicKey(rentCollector));

            foreach (var instruction in instructions)
            {
                builder.AddInstruction(instruction);
            }

            var compiledMessage = builder.CompileMessage();

            // Asked for before the wallet is prompted: a server failure costs the user
            // no unlock round trip, and the API signs exactly these bytes, so the
            // message this wallet then signs is the one both signatures land on.
            var rentCollectorSignature = await RentCollectorSignatureClient.GetRentCollectorSignatureAsync(
                account, compiledMessage, description, CancellationToken.None);

            var framed = SolanaTransactionFramer.FrameUnsigned(
                compiledMessage, SolanaTransactionFramer.GetRequiredSignatures(compiledMessage));

            var parsed = SolanaTransactionFramer.Parse(framed);

            var rentCollectorSlot = SolanaTransactionFramer.FindSignerIndex(
                parsed.Message, SolanaBase58.Decode(rentCollector.ToString()));

            var withRentCollector = SolanaTransactionFramer.ApplySignature(
                parsed, rentCollectorSlot, rentCollectorSignature);

            if (account.KeyType == KeyTypeEnum.SolanaMnemonic)
            {
                // The key lives on this device: sign the investor's slot locally, then
                // submit over RPC. The rent collector's slot stays pre-applied.
                var signed = await account.SignWireTransactionAsync(
                    withRentCollector, description, CancellationToken.None);

                return await SolanaRpcModel.SendTransactionAsync(cluster, signed, CancellationToken.None);
            }

            // A Mobile Wallet Adapter wallet signs and submits in the wallet app, which
            // fills the investor's slot and keeps the rent collector's pre-applied one.
            var signature = await account.SignAndSendWireTransactionAsync(
                withRentCollector, cluster, description, CancellationToken.None);

            return SolanaBase58.Encode(signature);
        }
    }
}
