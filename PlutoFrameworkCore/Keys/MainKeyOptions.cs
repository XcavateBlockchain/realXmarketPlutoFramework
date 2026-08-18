namespace PlutoFrameworkCore.Keys
{
    /// <summary>
    /// The chain whose key identifies the user: the address their public profile is
    /// registered under, and the one the app shows as theirs.
    /// </summary>
    public enum MainKeyChain
    {
        Solana,
        Polkadot,
    }

    /// <summary>
    /// Which key the app treats as the user's main one, and the default it uses until they
    /// choose. Kept out of the UI layer so the resolution rule is testable and the default is
    /// a single stated fact rather than a literal repeated at every call site.
    /// </summary>
    public static class MainKeyOptions
    {
        /// <summary>
        /// Solana. New accounts are Solana-only, so a user who never opens Settings must end
        /// up on the key they actually hold.
        /// </summary>
        public const MainKeyChain Default = MainKeyChain.Solana;

        /// <summary>In display order.</summary>
        public static readonly MainKeyChain[] Selectable =
            [MainKeyChain.Solana, MainKeyChain.Polkadot];

        /// <summary>
        /// The stored preference reconciled with the keys that exist. Null when there are no
        /// keys at all.
        /// </summary>
        /// <remarks>
        /// The preference alone is not enough to act on. Users onboarded before the Solana
        /// switch hold only a Substrate key, and the Solana default would otherwise strand
        /// them on a chain they have no key for; falling back keeps them working without
        /// making them visit Settings first. The preference is left untouched either way, so
        /// adding the missing key later restores the choice they actually made.
        /// </remarks>
        public static MainKeyChain? Resolve(MainKeyChain preferred, bool hasSolana, bool hasSubstrate)
        {
            var available = preferred switch
            {
                MainKeyChain.Solana => hasSolana,
                MainKeyChain.Polkadot => hasSubstrate,
                _ => false,
            };

            if (available)
            {
                return preferred;
            }

            if (hasSolana)
            {
                return MainKeyChain.Solana;
            }

            if (hasSubstrate)
            {
                return MainKeyChain.Polkadot;
            }

            return null;
        }
    }
}
