using PlutoFramework.Components.Solana.Status;
using PlutoFramework.Model;
using PlutoFramework.Model.Solana;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using SolanaPublicKey = Solnet.Wallet.PublicKey;

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// Submits a marketplace program call the way the Solana transfer flow submits a
    /// transfer: status popup registered before any slow work, instructions built for
    /// the signing wallet, sent on the selected cluster - the one the listing came from,
    /// then tracked to confirmation. The replacement for the Substrate extrinsic pipeline
    /// (TransactionAnalyzer + extrinsic status stack) on the property pages. When the
    /// program additionally requires the rent collector's signature (reserve, buy and claim),
    /// that signature comes from the profile API - which holds the rent collector key -
    /// and the investor signs the same compiled message, which this app then submits.
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
        public static event EventHandler<MarketplaceTransactionConfirmedEventArgs>? TransactionConfirmed;

        public static event EventHandler<MarketplaceTransactionFailedEventArgs>? TransactionFailed;

        /// <summary>
        /// Raised when a submission fails with an exception - including the build
        /// callback's own rejections (insufficient balance, stale listing), which carry
        /// user-presentable messages. The status popup remains the global report; this
        /// lets the flow that started the submission surface the same reason in place.
        /// </summary>
        /// <remarks>
        /// Same static-event lifetime warning as <see cref="TransactionConfirmed"/>. The
        /// early-return failures (no account, undeployed cluster, too many signers) do
        /// NOT raise it: they are configuration problems the status popup covers, not
        /// form-correctable rejections.
        /// </remarks>
        public static event EventHandler<MarketplaceSubmissionFailedEventArgs>? SubmissionFailed;

        /// <summary>
        /// Builds and submits one marketplace transaction.
        /// <paramref name="buildInstructionsAsync"/> receives the signing wallet's
        /// address - the investor/confirmer the program instructions are keyed by - and
        /// the cluster the listing came from, resolved once up front.
        /// </summary>
        /// <returns>
        /// True once a signed transaction was actually sent; false on every pre-submit
        /// exit (undeployed cluster, missing account, a declined unlock prompt, an
        /// unsatisfiable signer count) and on a failed build or submission. Callers that
        /// gate follow-up UI on a real submission (the SPV-case claim's confirmation
        /// navigation) switch on this instead of guessing from the events - the early
        /// exits deliberately raise no SubmissionFailed.
        /// </returns>
        public static async Task<bool> SubmitAsync(
            string description,
            Func<string, SolanaCluster, CancellationToken, Task<List<TransactionInstruction>>> buildInstructionsAsync,
            Action<string>? onSubmitted = null)
        {
            // The marketplace follows the selected network: the listing being acted on came
            // from this cluster's deployment, and so must the transaction. Where nothing is
            // deployed there is nothing to build against - fail before any wallet trip.
            var cluster = SolanaNetworkModel.SelectedCluster;

            var statusPopup = DependencyService.Get<SolanaTransactionPopupViewModel>();

            // Registered before anything slow, so the user sees the action acknowledged
            // the moment they tap rather than after an unlock prompt and a round trip.
            var info = statusPopup.Register(description, cluster);

            if (!XcavateDeploymentModel.IsDeployed(cluster))
            {
                info.Status = SolanaTransactionStatus.Error;
                info.ErrorMessage = XcavateDeploymentModel.NotDeployedMessage(cluster);

                return false;
            }

            // Hoisted out of the try so the failure handler can resolve which program a
            // rejected instruction targeted; null when the build itself failed.
            List<TransactionInstruction>? instructions = null;

            try
            {
                var address = KeysModel.GetSolanaAddress();

                if (string.IsNullOrEmpty(address))
                {
                    info.Status = SolanaTransactionStatus.Error;
                    info.ErrorMessage = "No Solana account is set up in this wallet.";

                    return false;
                }

                // Built before the key is unlocked: a build failure (no position, closed
                // listing) should not cost an unlock prompt.
                instructions = await buildInstructionsAsync(address, cluster, CancellationToken.None);

                // The program behind each instruction, in order: a "custom program error"
                // code is only meaningful against the program that raised it (6013 exists
                // in three of the bundled IDLs), and both the submission failure below and
                // the tracker's on-chain failure decode through this.
                info.InstructionProgramIds = instructions
                    .Select(instruction => new SolanaPublicKey(instruction.ProgramId).Key)
                    .ToList();

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
                    // No key, or the unlock prompt was declined. Either way the popup
                    // must not sit at Submitting forever.
                    info.Status = SolanaTransactionStatus.Error;
                    info.ErrorMessage = "No Solana account is set up in this wallet, or the unlock prompt was declined.";

                    return false;
                }

                if (signerKeys.Count > 2)
                {
                    // More than two distinct signers can never be satisfied by this
                    // flow (this wallet plus the rent collector) - fail now, before
                    // an unlock prompt and a wallet round trip, with the real reason.
                    info.Status = SolanaTransactionStatus.Error;
                    info.ErrorMessage = $"This transaction needs {signerKeys.Count} signatures and this flow can provide at most two. Nothing was signed or submitted.";

                    return false;
                }

                string signature;

                // A blockhash lives ~150 slots (a minute or two), and a slow approval in
                // the wallet app can outlive it - the wallet then refuses with "blockhash
                // expired". That is the one failure a straight retry repairs: the next
                // attempt fetches a fresh blockhash (and, in the two-signer flow, a fresh
                // rent collector signature over it), so the user just approves again.
                const int maxAttempts = 2;

                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        signature = signerKeys.Count == 1
                            // The one-signer path (this wallet IS the rent collector): byte
                            // for byte the way it has always worked.
                            ? await account.SendAsync(instructions, description, CancellationToken.None, cluster)
                            : await SubmitTwoSignerAsync(account, cluster, instructions, description);

                        break;
                    }
                    catch (Exception ex) when (attempt < maxAttempts && SolanaBlockhashExpiry.IsExpiredError(ex.Message))
                    {
                    }
                }

                info.Signature = signature;
                info.Status = SolanaTransactionStatus.Pending;

                // The per-transaction callback raises the marketplace-scoped event: a
                // reserve, buy or claim that lands changes every listing figure on screen,
                // while a plain transfer (which only the tracker's own event reports) must
                // not cost the property pages a re-query. The args carry the signature so
                // follow-up writes (the purchased status message) can name the
                // transaction they report.
                onSubmitted?.Invoke(signature);

                _ = SolanaTransactionTracker.TrackAsync(
                    signature, cluster, info, CancellationToken.None,
                    onConfirmed: _ => TransactionConfirmed?.Invoke(
                        null, new MarketplaceTransactionConfirmedEventArgs(signature, cluster)),
                    onFailed: _ => TransactionFailed?.Invoke(
                        null, new MarketplaceTransactionFailedEventArgs(signature, cluster)));

                return true;
            }
            catch (Exception ex)
            {
                // The status popup is the failure report now: it shows this message,
                // so nothing alongside it. A program rejection is decoded through the
                // bundled IDLs into the program author's own words; anything the metadata
                // cannot explain keeps the node's raw reason.
                info.Status = SolanaTransactionStatus.Error;
                info.ErrorMessage = SolanaSubmissionErrorDescriber.Describe(
                    ex.Message, SolanaProgramErrorCatalogs.Get(cluster), info.InstructionProgramIds);

                // The origin flow hears about it too, with the raw exception: a build
                // rejection (balance, staleness) is form-correctable, and the form it
                // came from is where the user can correct it.
                SubmissionFailed?.Invoke(null, new MarketplaceSubmissionFailedEventArgs(ex));

                return false;
            }
        }

        /// <summary>
        /// The reserve, buy and claim path: the program pins the rent-fronting payer to the rent
        /// collector AND requires its signature (devnet-verified), and this wallet is
        /// not the rent collector. The rent collector's half comes from the profile
        /// API - which holds the key - pre-applied to the wire transaction, then this
        /// wallet signs the same bytes and this app submits, so both signatures land on
        /// one message.
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
            var (blockHash, _) = await SolanaRpcModel.GetLatestBlockHashAsync(cluster, CancellationToken.None);

            var builder = new TransactionBuilder()
                .SetRecentBlockHash(blockHash)
                .SetFeePayer(new SolanaPublicKey(rentCollector));

            foreach (var instruction in instructions)
            {
                builder.AddInstruction(instruction);
            }

            var compiledMessage = builder.CompileMessage();

            // Asked for before the wallet is prompted: a server failure costs the user
            // no wallet round trip, and the API signs exactly these bytes, so the
            // message this wallet then signs is the one both signatures land on. The
            // request itself is not signed - the endpoint only needs the investor's
            // address to know whose on-chain signature the message must require.
            var rentCollectorSignature = await RentCollectorSignatureClient.GetRentCollectorSignatureAsync(
                investor, compiledMessage, CancellationToken.None);

            var withRentCollector = ApplyRentCollectorSignature(compiledMessage, rentCollector, rentCollectorSignature);

            // The investor's slot is signed - locally, or by the wallet app over MWA -
            // and this app submits over RPC. The wallet never submits on this path: a
            // wallet whose own send fails can still return a signature for a transaction
            // it never broadcast (observed with Phantom 26.30.2: its preflight failed,
            // the signature came back, nothing reached the chain), and the tracker would
            // then wait on that signature forever. Submitting app-side surfaces the
            // node's real rejection and lets the blockhash-expiry retry above repair an
            // expired blockhash.
            var signed = await account.SignWireTransactionAsync(
                withRentCollector, description, CancellationToken.None);

            return await SolanaRpcModel.SendTransactionAsync(cluster, signed, CancellationToken.None);
        }

        /// <summary>
        /// The wire-format transaction for <paramref name="compiledMessage"/> with the rent
        /// collector's signature pre-applied to its signer slot; the investor's slot stays
        /// empty for whichever key signs next.
        /// </summary>
        private static byte[] ApplyRentCollectorSignature(
            byte[] compiledMessage,
            string rentCollector,
            byte[] rentCollectorSignature)
        {
            var framed = SolanaTransactionFramer.FrameUnsigned(
                compiledMessage, SolanaTransactionFramer.GetRequiredSignatures(compiledMessage));

            var parsed = SolanaTransactionFramer.Parse(framed);

            var rentCollectorSlot = SolanaTransactionFramer.FindSignerIndex(
                parsed.Message, SolanaBase58.Decode(rentCollector));

            return SolanaTransactionFramer.ApplySignature(
                parsed, rentCollectorSlot, rentCollectorSignature);
        }
    }

    /// <summary>
    /// What a confirmed marketplace transaction is: its submission signature and the
    /// cluster it landed on. The signature is what the buy flow's purchased-status
    /// message reports as its <c>tx</c>.
    /// </summary>
    public sealed class MarketplaceTransactionConfirmedEventArgs(string signature, SolanaCluster cluster) : EventArgs
    {
        /// <summary>The confirmed transaction's base58 signature.</summary>
        public string Signature { get; } = signature;

        /// <summary>The cluster the transaction confirmed on.</summary>
        public SolanaCluster Cluster { get; } = cluster;
    }

    public sealed class MarketplaceTransactionFailedEventArgs(string signature, SolanaCluster cluster) : EventArgs
    {
        public string Signature { get; } = signature;
        public SolanaCluster Cluster { get; } = cluster;
    }

    /// <summary>
    /// A failed marketplace submission: the raw exception, so the origin flow can decide
    /// which failures its form can correct (the build's <see cref="InvalidOperationException"/>s)
    /// and which only the status popup should report.
    /// </summary>
    public sealed class MarketplaceSubmissionFailedEventArgs(Exception exception) : EventArgs
    {
        /// <summary>The exception that failed the build, signing or submission.</summary>
        public Exception Exception { get; } = exception;
    }
}
