using PlutoFramework.Model.Solana;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore.Keys;
using XcavateProfileApiClient.Signing;

namespace PlutoFramework.Model
{
    /// <summary>
    /// Which key identifies the user: the address their public profile is registered under,
    /// and the one the app shows as theirs. One stored setting rather than a choice made per
    /// action, so the profile the menu shows and the profile an edit writes to can never
    /// disagree.
    /// </summary>
    public static class MainKeyModel
    {
        /// <summary>
        /// Raised after the main key changes. Views hold an address and a profile that belong
        /// to one chain, so they must re-resolve rather than keep showing the other one's.
        /// </summary>
        public static event EventHandler<MainKeyChain>? ChainChanged;

        /// <summary>
        /// What the user picked. Not what to act on - see <see cref="ResolvedChain"/>, which
        /// reconciles this with the keys that exist.
        /// </summary>
        public static MainKeyChain SelectedChain
        {
            get => Enum.TryParse<MainKeyChain>(
                Preferences.Get(PreferencesModel.SETTINGS_MAIN_KEY_CHAIN, string.Empty), out var chain)
                    ? chain
                    : MainKeyOptions.Default;

            set
            {
                if (value == SelectedChain)
                {
                    return;
                }

                Preferences.Set(PreferencesModel.SETTINGS_MAIN_KEY_CHAIN, value.ToString());

                ChainChanged?.Invoke(null, value);
            }
        }

        /// <summary>
        /// The chain to actually act on, or null when the user holds no keys at all.
        /// </summary>
        public static MainKeyChain? ResolvedChain => MainKeyOptions.Resolve(
            SelectedChain,
            hasSolana: KeysModel.HasSolanaKey(),
            hasSubstrate: KeysModel.HasSubstrateKey());

        /// <summary>Whether a chain can be selected, which is to say whether it has a key.</summary>
        public static bool IsAvailable(MainKeyChain chain) => chain switch
        {
            MainKeyChain.Solana => KeysModel.HasSolanaKey(),
            MainKeyChain.Polkadot => KeysModel.HasSubstrateKey(),
            _ => false,
        };

        /// <summary>
        /// The main key's address, or null when there is no key. Reads stored public keys
        /// without unlocking anything, so it is safe to call for display.
        /// </summary>
        /// <remarks>
        /// Never returns <see cref="KeysModel.GetSubstrateKey()"/>'s placeholder string:
        /// callers use null to mean logged out, and a placeholder address reads as a real one
        /// all the way down to an API query for a profile that cannot exist.
        /// </remarks>
        public static string? GetAddress() => ResolvedChain switch
        {
            MainKeyChain.Solana => KeysModel.GetSolanaAddress(),
            MainKeyChain.Polkadot => KeysModel.HasSubstrateKey() ? KeysModel.GetSubstrateKey() : null,
            _ => null,
        };

        /// <summary>
        /// A signer for the main key, or null when there is no key or the user declined to
        /// unlock it. Unlocks, so this prompts.
        /// </summary>
        public static async Task<IRequestSigner?> GetSignerAsync(
            string reason,
            CancellationToken token = default)
        {
            switch (ResolvedChain)
            {
                case MainKeyChain.Solana:
                    var solanaAccount = await PlutoFrameworkSolanaAccount.ResolveAsync(reason, token);

                    return solanaAccount is null ? null : new SolanaAccountRequestSigner(solanaAccount, reason);

                case MainKeyChain.Polkadot:
                    var account = await KeysModel.GetAccountAsync(reason);

                    return account is null ? null : new SubstrateRequestSigner(account);

                default:
                    return null;
            }
        }
    }
}
