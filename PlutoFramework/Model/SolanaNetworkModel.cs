using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Model
{
    /// <summary>
    /// The Solana network the whole app operates on. One stored setting rather than a choice
    /// made per action, so an address, an authorization and a transaction can never disagree
    /// about which network they belong to.
    /// </summary>
    public static class SolanaNetworkModel
    {
        /// <summary>
        /// The networks offered in Settings, in display order.
        /// </summary>
        public static SolanaCluster[] SelectableClusters => SolanaNetworkOptions.Selectable;

        /// <summary>
        /// Mainnet until the user picks otherwise. Changing this leaves an already connected
        /// wallet in place: its authorization was granted on one network and the wallet is
        /// the party that rejects a mismatch, so the app does not pre-emptively discard it.
        /// </summary>
        public static SolanaCluster SelectedCluster
        {
            get => SolanaClusterExtensions.FromChainId(
                Preferences.Get(PreferencesModel.SETTINGS_SOLANA_NETWORK, SolanaNetworkOptions.Default.ToChainId()));

            set => Preferences.Set(PreferencesModel.SETTINGS_SOLANA_NETWORK, value.ToChainId());
        }
    }
}
