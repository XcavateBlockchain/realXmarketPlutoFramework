using System.Numerics;
using Solnet.Wallet;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// The transfer flow's entry point: what can be sent, and the instructions to send it.
    /// </summary>
    /// <remarks>
    /// Deliberately thin, like <see cref="SolanaBalancesModel"/>. Everything decidable without
    /// a network lives in <see cref="SolanaTransferBalanceAssembler"/> and
    /// <see cref="SolanaTransferPlanner"/>, where it is tested; this only fetches and maps.
    /// </remarks>
    public static class SolanaTransferModel
    {
        /// <summary>
        /// Every token the picker offers, with the amount a transfer can actually spend.
        /// </summary>
        public static async Task<IReadOnlyList<SolanaTransferBalance>> GetTransferableBalancesAsync(
            string ownerAddress, SolanaCluster cluster, CancellationToken token)
        {
            var whitelist = SolanaTokenWhitelist.ForCluster(cluster);

            var lamports = await SolanaRpcModel.GetLamportBalanceAsync(cluster, ownerAddress, token);

            var accounts = new List<SolanaTokenAccountAmount>();

            // One call per distinct program, as the balances path does: legacy SPL accounts
            // and Token-2022 accounts are returned by different program ids, never together.
            foreach (var programId in whitelist
                .Select(entry => entry.ProgramId)
                .Distinct(StringComparer.Ordinal))
            {
                var tokenAccounts = await SolanaRpcModel.GetTokenAccountsAsync(
                    cluster, ownerAddress, programId, token);

                foreach (var tokenAccount in tokenAccounts)
                {
                    var info = tokenAccount.Account?.Data?.Parsed?.Info;

                    if (info?.TokenAmount is null || string.IsNullOrEmpty(info.Mint))
                    {
                        continue;
                    }

                    accounts.Add(new SolanaTokenAccountAmount
                    {
                        // The account's own address, which the balances path discards. It is
                        // what tells the associated token account from any other account
                        // holding the same mint.
                        Address = tokenAccount.PublicKey,
                        Mint = info.Mint,
                        RawAmount = info.TokenAmount.Amount,
                        Decimals = info.TokenAmount.Decimals,
                    });
                }
            }

            return SolanaTransferBalanceAssembler.Assemble(lamports, accounts, whitelist, ownerAddress);
        }

        /// <summary>
        /// The instructions for one transfer, including creating the recipient's token
        /// account when they have none.
        /// </summary>
        public static async Task<SolanaTransferPlan> BuildPlanAsync(
            string senderAddress,
            string recipientAddress,
            SolanaTransferBalance token,
            BigInteger baseUnits,
            SolanaCluster cluster,
            CancellationToken cancellationToken)
        {
            var recipientAccountExists = token.IsNative
                // A SOL transfer touches no token account, so there is nothing to probe and
                // no reason to spend a round trip finding out.
                || await RecipientHasTokenAccountAsync(
                    recipientAddress, token, cluster, cancellationToken);

            return SolanaTransferPlanner.Build(
                senderAddress, recipientAddress, token, baseUnits, recipientAccountExists);
        }

        private static async Task<bool> RecipientHasTokenAccountAsync(
            string recipientAddress,
            SolanaTransferBalance token,
            SolanaCluster cluster,
            CancellationToken cancellationToken)
        {
            if (!SolanaAddressValidator.IsValidAddress(recipientAddress))
            {
                // Let the planner produce the error, so the message is the same wherever an
                // invalid address enters.
                return true;
            }

            var associated = SolanaAssociatedTokenAccount.Derive(
                new PublicKey(recipientAddress),
                new PublicKey(token.Mint),
                new PublicKey(token.ProgramId));

            var info = await SolanaRpcModel.GetAccountInfoAsync(
                cluster, associated.Key, cancellationToken);

            return info is not null;
        }
    }
}
