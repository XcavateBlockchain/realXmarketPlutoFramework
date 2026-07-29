namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// The life of a submitted Solana transaction, as the status toast reports it.
    /// </summary>
    /// <remarks>
    /// Deliberately shaped like <c>ExtrinsicStatusEnum</c> so the two toast stacks read
    /// alike. Solana's "processed" level is folded into <see cref="Pending"/>: it has no
    /// Substrate counterpart, and a third pre-confirmation state buys the user nothing.
    /// </remarks>
    public enum SolanaTransactionStatus
    {
        /// <summary>Signing, or handed to the wallet. No signature yet.</summary>
        Submitting,

        /// <summary>Signature returned; the cluster reports nothing, or only "processed".</summary>
        Pending,

        ConfirmedSuccess,
        ConfirmedFailed,
        FinalizedSuccess,
        FinalizedFailed,

        /// <summary>The blockhash expired without the signature ever being seen.</summary>
        Dropped,

        /// <summary>Submission threw. The transaction never reached the network.</summary>
        Error,
    }
}
