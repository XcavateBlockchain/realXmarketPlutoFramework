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
        /// Raised after the selected network changes. Balance views hold figures that are
        /// only meaningful for one cluster, so they must re-query rather than keep showing
        /// numbers from the other one.
        /// </summary>
        public static event EventHandler<SolanaCluster>? ClusterChanged;

        /// <summary>
        /// Devnet until the user picks otherwise. Changing this leaves an already connected
        /// wallet in place: its authorization was granted on one network and the wallet is
        /// the party that rejects a mismatch, so the app does not pre-emptively discard it.
        /// </summary>
        public static SolanaCluster SelectedCluster
        {
            get
            {
                var stored = SolanaClusterExtensions.FromChainId(
                    Preferences.Get(PreferencesModel.SETTINGS_SOLANA_NETWORK, SolanaNetworkOptions.Default.ToChainId()));

                // A stored network the app does not currently offer - mainnet while its
                // programs are undeployed, or an unrecognised value FromChainId read as
                // mainnet - would send every wallet request to a network nothing works
                // on. Fall back to the default and repair the stored value, so the stale
                // choice cannot quietly come back when the network becomes selectable.
                if (!SolanaNetworkOptions.Selectable.Contains(stored))
                {
                    Preferences.Set(PreferencesModel.SETTINGS_SOLANA_NETWORK, SolanaNetworkOptions.Default.ToChainId());

                    return SolanaNetworkOptions.Default;
                }

                return stored;
            }

            set
            {
                if (value == SelectedCluster)
                {
                    return;
                }

                Preferences.Set(PreferencesModel.SETTINGS_SOLANA_NETWORK, value.ToChainId());

                ClusterChanged?.Invoke(null, value);
            }
        }
    }
}
