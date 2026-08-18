using System.Globalization;
using System.Numerics;
using Solnet.Wallet;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Turns a lamport balance and a bag of token accounts into the picker's rows. Pure, so
    /// the spendable rule is tested without a network.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="SolanaBalanceAssembler"/>, and different from it in exactly
    /// one way: that one sums every token account for a mint, because the balances page
    /// reports what the wallet holds. This one takes the associated token account alone,
    /// because a transfer spends from one account. Offering the sum would let Max fill an
    /// amount the transaction cannot cover.
    /// </remarks>
    public static class SolanaTransferBalanceAssembler
    {
        public static IReadOnlyList<SolanaTransferBalance> Assemble(
            ulong lamports,
            IReadOnlyList<SolanaTokenAccountAmount> tokenAccounts,
            IReadOnlyList<SolanaTokenWhitelistEntry> whitelist,
            string ownerAddress)
        {
            var rows = new List<SolanaTransferBalance>(whitelist.Count + 1)
            {
                new()
                {
                    Symbol = SolanaNativeToken.Symbol,
                    Mint = SolanaNativeToken.Mint,
                    Decimals = SolanaNativeToken.Decimals,
                    // SOL is not an SPL token; the field is carried so every row has one, and
                    // the legacy program is the harmless answer for a row that never uses it.
                    ProgramId = SolanaTokenProgram.Legacy,
                    IsNative = true,
                    SpendableBaseUnits = new BigInteger(lamports),
                },
            };

            var owner = new PublicKey(ownerAddress);

            // Indexed by address rather than by mint: two accounts can hold the same mint,
            // and only the associated one is spendable.
            var amountByAddress = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var account in tokenAccounts)
            {
                // No address means nothing can be matched. Treating it as a match would let
                // an arbitrary account stand in for the associated one.
                if (string.IsNullOrEmpty(account.Address))
                {
                    continue;
                }

                amountByAddress[account.Address] = account.RawAmount;
            }

            foreach (var entry in whitelist)
            {
                var associated = SolanaAssociatedTokenAccount.Derive(
                    owner, new PublicKey(entry.Mint), new PublicKey(entry.ProgramId));

                // Absent means no associated account yet, which is a zero balance rather
                // than a reason to drop the row: the picker lists what the app deals in.
                var spendable = amountByAddress.TryGetValue(associated.Key, out var raw)
                    ? ParseBaseUnits(raw)
                    : BigInteger.Zero;

                rows.Add(new SolanaTransferBalance
                {
                    Symbol = entry.Symbol,
                    Mint = entry.Mint,
                    Decimals = entry.Decimals,
                    ProgramId = entry.ProgramId,
                    IsNative = false,
                    SpendableBaseUnits = spendable,
                });
            }

            return rows;
        }

        /// <summary>
        /// A malformed amount is treated as nothing to send. Throwing here would take down
        /// the whole picker over one unparsable account.
        /// </summary>
        private static BigInteger ParseBaseUnits(string raw) =>
            BigInteger.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : BigInteger.Zero;
    }
}
