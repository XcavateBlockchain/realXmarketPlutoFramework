using Solnet.Wallet;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Generic program-derived-address derivation, the seed-list counterpart to
    /// <see cref="SolanaAssociatedTokenAccount.Derive"/> (which hardcodes the ATA seed
    /// shape). Anchor programs address their state accounts this way.
    /// </summary>
    public static class SolanaProgramAddress
    {
        /// <summary>
        /// The PDA for <paramref name="seeds"/> under <paramref name="programId"/>.
        /// Seed order and byte-for-byte content are part of the address - a wrong seed
        /// yields a valid-looking account that simply never exists on chain.
        /// </summary>
        public static PublicKey Derive(PublicKey programId, params byte[][] seeds)
        {
            if (!PublicKey.TryFindProgramAddress(seeds.ToList(), programId, out var address, out _))
            {
                // Every practical seed list has a valid bump; failing here means the
                // inputs were not keys or seeds at all.
                throw new InvalidOperationException(
                    $"Could not derive a program address under {programId}");
            }

            return address;
        }
    }
}
