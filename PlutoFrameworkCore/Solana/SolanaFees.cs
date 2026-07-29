using System.Numerics;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// What a transfer costs, and how much of a balance Max may offer to send.
    /// </summary>
    public static class SolanaFees
    {
        /// <summary>
        /// The base fee per signature. Every transaction this app builds has exactly one
        /// signer, so this is the whole fee absent a priority fee, which none of them set.
        /// </summary>
        public const ulong LamportsPerSignature = 5_000;

        /// <summary>
        /// 0.001 SOL, held back from a Max SOL send.
        /// </summary>
        /// <remarks>
        /// 200 times the signature fee, so the transaction pays for itself and the wallet is
        /// left able to make several more.
        ///
        /// Deliberately not sized to cover associated-token-account rent (~0.00204 SOL). The
        /// user asked to send SOL; withholding twice as much again to fund a hypothetical
        /// later SPL transfer would quietly send less than they meant.
        /// </remarks>
        public const ulong MaxReserveLamports = 1_000_000;

        /// <summary>
        /// The most Max may fill in for a balance.
        /// </summary>
        /// <remarks>
        /// SPL tokens offer the whole balance: fees are paid in SOL, so reserving here would
        /// strand tokens. SOL holds back <see cref="MaxReserveLamports"/>, floored at zero so
        /// a dust balance yields nothing to send rather than a negative amount, which
        /// <see cref="SolanaAmount.ToBaseUnits"/> would throw on.
        /// </remarks>
        public static BigInteger MaxSendable(BigInteger balanceBaseUnits, bool isNative)
        {
            if (!isNative)
            {
                return balanceBaseUnits;
            }

            var sendable = balanceBaseUnits - MaxReserveLamports;

            return sendable > BigInteger.Zero ? sendable : BigInteger.Zero;
        }
    }
}
