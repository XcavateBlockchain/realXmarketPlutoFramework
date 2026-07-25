using PlutoFrameworkCore.Solana;
using System.Text.Json.Serialization;

namespace PlutoFrameworkCore.Keys
{
    /// <summary>
    /// A Solana account held by a separate wallet app, reached over Mobile Wallet Adapter.
    ///
    /// There is no private key here. What this holds is an authorization token, which is
    /// itself a secret, so the whole record is serialized into secure storage as the
    /// key's secret rather than being spread across the database.
    ///
    /// Deliberately not an <see cref="ISolanaAccountKey"/>: it cannot produce a signing
    /// account locally. Signing goes through the Mobile Wallet Adapter client instead.
    /// </summary>
    public record SolanaMwaKey
    {
        public required string AuthToken { get; set; }

        /// <summary>
        /// Base58 Solana address of the authorized account.
        /// </summary>
        public required string Address { get; set; }

        /// <summary>
        /// The Mobile Wallet Adapter chain identifier this authorization is valid for,
        /// for example "solana:devnet". An auth token does not carry across clusters.
        /// </summary>
        public required string Chain { get; set; }

        /// <summary>
        /// Optional base URI the wallet asked us to use for subsequent associations.
        /// </summary>
        public string? WalletUriBase { get; set; }

        /// <summary>
        /// Optional human label the wallet supplied for the account.
        /// </summary>
        public string? AccountLabel { get; set; }

        [JsonIgnore]
        public SolanaCluster Cluster => SolanaClusterExtensions.FromChainId(Chain);

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(AccountLabel) ? "Solana wallet" : AccountLabel;
    }
}
