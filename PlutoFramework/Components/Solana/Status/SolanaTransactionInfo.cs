using CommunityToolkit.Mvvm.ComponentModel;
using PlutoFrameworkCore.Constants;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Status
{
    /// <summary>
    /// One tracked transaction, as the toast stack shows it.
    /// </summary>
    /// <remarks>
    /// The Solana counterpart of <c>ExtrinsicInfo</c>, which cannot be reused: it carries a
    /// <c>Substrate.NetApi</c> hash, an <c>Endpoint</c> whose icon drives the toast, and a
    /// <c>TaskCompletionSource&lt;EventsListViewModel&gt;</c> that its tap handler awaits.
    /// A Solana row has none of those, and filling them with stand-ins would leave the toast
    /// awaiting a source nothing ever completes.
    /// </remarks>
    public partial class SolanaTransactionInfo : ObservableObject
    {
        /// <summary>
        /// Identifies the toast in the stack. Not the signature: the toast exists from the
        /// moment the user taps Transfer, and no signature is known until submission returns.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>What the transaction does, e.g. "Transfer 0.5 SOL".</summary>
        public required string Description { get; init; }

        public required SolanaCluster Cluster { get; init; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasExplorerLink))]
        private string? signature;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        [NotifyPropertyChangedFor(nameof(IsFailure))]
        private SolanaTransactionStatus status = SolanaTransactionStatus.Submitting;

        /// <summary>
        /// Why the transaction failed, in words the error page shows. Null while nothing
        /// has failed: the page is only reachable from a failed toast.
        /// </summary>
        /// <remarks>
        /// Filled where the failure is known — the submitter for a submission that threw
        /// or never got signed, the tracker for a failure the cluster reported.
        /// </remarks>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
        private string? errorMessage;

        /// <summary>
        /// Null until submission returns one, and permanently null when submission failed —
        /// so the explorer link is hidden rather than pointing at nothing.
        /// </summary>
        public bool HasExplorerLink => !string.IsNullOrEmpty(Signature);

        public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// True for every status the toast offers the error page for. A submission that
        /// threw and a transaction the cluster rejected are both worth a look.
        /// </summary>
        public bool IsFailure => Status is
            SolanaTransactionStatus.Error
            or SolanaTransactionStatus.Dropped
            or SolanaTransactionStatus.ConfirmedFailed
            or SolanaTransactionStatus.FinalizedFailed;

        public string ExplorerUrl => Solscan.TransactionUrl(Signature ?? string.Empty, Cluster);

        /// <summary>
        /// Parallel to the Substrate toast's wording, so the two stacks read alike.
        /// "Confirmed" stands where that one says "In block".
        /// </summary>
        public string StatusText => Status switch
        {
            SolanaTransactionStatus.Submitting => "Submitting",
            SolanaTransactionStatus.Pending => "Pending",
            SolanaTransactionStatus.ConfirmedSuccess => "Confirmed - Success",
            SolanaTransactionStatus.ConfirmedFailed => "Confirmed - Failed",
            SolanaTransactionStatus.FinalizedSuccess => "Finalized - Success",
            SolanaTransactionStatus.FinalizedFailed => "Finalized - Failed",
            SolanaTransactionStatus.Dropped => "Dropped",
            _ => "Error",
        };

        public Color StatusColor => Status switch
        {
            SolanaTransactionStatus.Submitting => Colors.Gray,
            SolanaTransactionStatus.Pending => Colors.Orange,
            SolanaTransactionStatus.ConfirmedSuccess => Colors.Green,
            SolanaTransactionStatus.FinalizedSuccess => Colors.Green,
            _ => Colors.DarkRed,
        };
    }
}
