using SolanaAccount = Solnet.Wallet.Account;

namespace PlutoFrameworkCore.Keys
{
    /// <summary>
    /// Parallel to <see cref="IAccountKey"/>, which cannot be reused here because it
    /// exposes a Substrate <c>Account</c>.
    /// </summary>
    public interface ISolanaAccountKey
    {
        public SolanaAccount Account { get; }

        public string Address { get; }
    }
}
