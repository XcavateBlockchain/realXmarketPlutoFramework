using PlutoFramework.Components.Solana.Status;
using PlutoFramework.Model;
using PlutoFramework.Model.Solana;
using PlutoFramework.Model.Xcavate;
using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Models;

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// Submits a marketplace program call the way the Solana transfer flow submits a
    /// transfer: status toast registered before any slow work, instructions built for
    /// the signing wallet, sent on the marketplace's own cluster, then tracked to
    /// confirmation. The replacement for the Substrate extrinsic pipeline
    /// (TransactionAnalyzer + extrinsic status stack) on the property pages.
    /// </summary>
    public static class XcavateMarketplaceTransactionModel
    {
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

                // The marketplace's reserve, buy, and claim instructions take a
                // rent-fronting payer that the deployed program pins to the config's
                // rent collector AND requires to have signed (devnet-verified), so a
                // purchase genuinely needs two signatures: the investor's and the rent
                // collector's. When this wallet IS the rent collector the two roles
                // collapse into one signer and the transaction completes; with any
                // other wallet the second signer can never be filled here (the app
                // holds no rent-collector key). More than one distinct signer means
                // the transaction can never be complete here - fail now, before an
                // unlock prompt and a wallet round trip, with the real reason.
                var distinctSignerCount = instructions
                    .SelectMany(instruction => instruction.Keys)
                    .Where(accountMeta => accountMeta.IsSigner)
                    .Select(accountMeta => accountMeta.PublicKey)
                    .Concat(new[] { address })
                    .Distinct()
                    .Count();

                if (distinctSignerCount > 1)
                {
                    info.Status = SolanaTransactionStatus.Error;
                    info.ErrorMessage = "This transaction needs two signatures - the investor's and the rent collector's (the rent-fronting wallet the marketplace pins to its sponsor), and this wallet is not the rent collector. Nothing was signed or submitted.";

                    return;
                }

                var account = await PlutoFrameworkSolanaAccount.ResolveAsync(description, CancellationToken.None);

                if (account is null)
                {
                    // No key, or the unlock prompt was declined. Either way the toast
                    // must not sit at Submitting forever.
                    info.Status = SolanaTransactionStatus.Error;
                    info.ErrorMessage = "No Solana account is set up in this wallet, or the unlock prompt was declined.";

                    return;
                }

                var signature = await account.SendAsync(instructions, description, CancellationToken.None, cluster);

                info.Signature = signature;
                info.Status = SolanaTransactionStatus.Pending;

                _ = SolanaTransactionTracker.TrackAsync(signature, cluster, info, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // The toast is the failure report now: its error page shows this message,
                // so no popup alongside it.
                info.Status = SolanaTransactionStatus.Error;
                info.ErrorMessage = ex.Message;
            }
        }
    }
}
