using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Status
{
    /// <summary>
    /// The Solana transaction toasts, as one app-wide stack.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>ExtrinsicStatusStackViewModel</c>: a dictionary is the source of truth and
    /// the observable collection is rebuilt from it, so a status change on one entry cannot
    /// leave the rendered list disagreeing with the store.
    ///
    /// In-memory only, like the Substrate stack. A tracked transaction is lost on restart;
    /// the explorer link is the durable record.
    /// </remarks>
    public partial class SolanaTransactionStatusStackViewModel : ObservableObject
    {
        private readonly Dictionary<string, SolanaTransactionInfo> transactions = new(StringComparer.Ordinal);

        [ObservableProperty]
        private ObservableCollection<SolanaTransactionInfo> transactionInfos = [];

        [ObservableProperty]
        private bool isVisible;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LayoutBounds))]
        private int heightRequest;

        /// <summary>
        /// How far down the Substrate stack pushes this one. Read from
        /// <c>ExtrinsicStatusStackViewModel</c> by the layout, which owns that subscription.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LayoutBounds))]
        private int topOffset;

        public Rect LayoutBounds => new(0.5, 0, 1, HeightRequest);

        /// <summary>
        /// Adds a toast at <see cref="SolanaTransactionStatus.Submitting"/> and returns it, so
        /// the caller can fill in the signature and let the tracker drive the rest.
        /// </summary>
        public SolanaTransactionInfo Register(string description, SolanaCluster cluster)
        {
            var info = new SolanaTransactionInfo
            {
                // Not the signature: there is none yet, and the user must see something the
                // moment they tap rather than after the unlock prompt and a round trip.
                Id = Guid.NewGuid().ToString("N"),
                Description = description,
                Cluster = cluster,
            };

            transactions[info.Id] = info;

            Update();

            return info;
        }

        public void Remove(string id)
        {
            if (transactions.Remove(id))
            {
                Update();
            }
        }

        public bool Contains(string id) => transactions.ContainsKey(id);

        public void Update()
        {
            TransactionInfos = new ObservableCollection<SolanaTransactionInfo>(transactions.Values);

            IsVisible = TransactionInfos.Count > 0;

            HeightRequest = Math.Max((75 * TransactionInfos.Count) - 15, 0);
        }
    }
}
