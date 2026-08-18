namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// Opens a <c>solana-wallet:</c> association URI, letting the OS offer the wallet apps
    /// that can handle it.
    ///
    /// Injected through <see cref="PlutoConfigurationModel"/> the same way secure storage
    /// is, keeping the protocol code platform-agnostic.
    /// </summary>
    public interface IMwaIntentLauncher
    {
        /// <summary>
        /// False where the platform cannot support Mobile Wallet Adapter at all. The
        /// protocol is specified only for Android: it depends on intents for wallet
        /// discovery and on Digital Asset Links for verifying app identity. The
        /// specification lists iOS support as planned for a future version.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Launches the association URI. Returns false when no installed app can handle
        /// it, which means the user has no compatible wallet rather than that anything
        /// went wrong.
        /// </summary>
        Task<bool> LaunchAsync(string associationUri);
    }
}
