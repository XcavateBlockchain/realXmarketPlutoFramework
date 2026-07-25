using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;
using Solnet.Rpc.Builders;

namespace PlutoFramework.Model.Solana
{
    /// <summary>
    /// A Solana account whose key lives on this device. Signs offline, then submits over RPC.
    /// </summary>
    public sealed class MnemonicSolanaAccount : PlutoFrameworkSolanaAccount
    {
        private readonly SolanaMnemonicKey key;

        internal MnemonicSolanaAccount(SolanaMnemonicKey key)
        {
            this.key = key;
        }

        public override string Address => key.Address;

        public override string DisplayName => KeyTypeEnum.SolanaMnemonic.GetName();

        public override KeyTypeEnum KeyType => KeyTypeEnum.SolanaMnemonic;

        public override bool CanSignLocally => true;

        /// <summary>
        /// The phrase is cluster-agnostic, so this account works on whichever network the app
        /// is set to without any reauthorization step.
        /// </summary>
        public override Task<byte[]> SignMessageAsync(byte[] message, string reason, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            return Task.FromResult(key.Account.Sign(message));
        }

        protected override async Task<string> SignAndSubmitAsync(
            TransactionBuilder builder,
            SolanaCluster cluster,
            string reason,
            CancellationToken token)
        {
            // Build signs the compiled message and serializes the result in one step.
            var signedTransaction = builder.Build(key.Account);

            return await SolanaRpcModel.SendTransactionAsync(cluster, signedTransaction, token);
        }
    }
}
