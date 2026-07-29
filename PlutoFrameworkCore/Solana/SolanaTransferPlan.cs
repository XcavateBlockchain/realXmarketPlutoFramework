using Solnet.Rpc.Models;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// The instructions one transfer needs, ready to hand to
    /// <c>PlutoFrameworkSolanaAccount.SendAsync</c>.
    /// </summary>
    public sealed record SolanaTransferPlan
    {
        public required IReadOnlyList<TransactionInstruction> Instructions { get; init; }

        /// <summary>
        /// Whether this plan creates an associated token account for the recipient, which the
        /// sender pays rent for (~0.00204 SOL).
        /// </summary>
        /// <remarks>
        /// Not shown before confirming — that was a product decision. It is carried so the
        /// failure path can name the cause when the sender's SOL cannot cover it. Hiding a
        /// cost is a decision; hiding a failure is a bug.
        /// </remarks>
        public required bool CreatesRecipientAccount { get; init; }
    }
}
