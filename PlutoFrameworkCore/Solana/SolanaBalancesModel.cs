namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// The balances page's single entry point: address plus cluster in, display rows out.
    /// </summary>
    /// <remarks>
    /// Deliberately thin. Everything decidable without a network lives in
    /// <see cref="SolanaBalanceAssembler"/> and <see cref="SolanaPriceModel"/>, where it is
    /// tested; this method only fetches and maps.
    /// </remarks>
    public static class SolanaBalancesModel
    {
        public static async Task<IReadOnlyList<SolanaTokenBalance>> GetBalancesAsync(
            string address, SolanaCluster cluster, CancellationToken token)
        {
            var whitelist = SolanaTokenWhitelist.ForCluster(cluster);

            var lamports = await SolanaRpcModel.GetLamportBalanceAsync(cluster, address, token);

            var accounts = new List<SolanaTokenAccountAmount>();

            // One call per distinct program: legacy SPL accounts and Token-2022 accounts are
            // returned by different program ids, never together.
            foreach (var programId in whitelist
                .Select(entry => entry.ProgramId)
                .Distinct(StringComparer.Ordinal))
            {
                var tokenAccounts = await SolanaRpcModel.GetTokenAccountsAsync(
                    cluster, address, programId, token);

                foreach (var tokenAccount in tokenAccounts)
                {
                    var info = tokenAccount.Account?.Data?.Parsed?.Info;

                    if (info?.TokenAmount is null || string.IsNullOrEmpty(info.Mint))
                    {
                        continue;
                    }

                    accounts.Add(new SolanaTokenAccountAmount
                    {
                        Mint = info.Mint,
                        RawAmount = info.TokenAmount.Amount,
                        Decimals = info.TokenAmount.Decimals,
                    });
                }
            }

            var prices = await SolanaPriceModel.GetUsdPricesAsync(whitelist, token);

            return SolanaBalanceAssembler.Assemble(lamports, accounts, whitelist, prices);
        }
    }
}
