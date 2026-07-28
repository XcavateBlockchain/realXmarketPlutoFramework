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

        public override Task<byte[]> SignWireTransactionAsync(
            byte[] wireTransaction,
            string reason,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            return Task.FromResult(Sign(wireTransaction));
        }

        public override async Task<byte[]> SignAndSendWireTransactionAsync(
            byte[] wireTransaction,
            SolanaCluster cluster,
            string reason,
            CancellationToken token)
        {
            var signed = Sign(wireTransaction);

            var signature = await SolanaRpcModel.SendTransactionAsync(cluster, signed, token);

            // The RPC reports the signature base58-encoded, but callers relaying to a dapp
            // need the raw 64 bytes. Not PublicKey: that type rejects anything but 32.
            return SolanaBase58.Decode(signature);
        }

        /// <summary>
        /// Finds this account's slot among the transaction's required signers, signs the
        /// message, and writes the signature into that slot.
        /// </summary>
        private byte[] Sign(byte[] wireTransaction)
        {
            var parsed = SolanaTransactionFramer.Parse(wireTransaction);

            var signerIndex = SolanaTransactionFramer.FindSignerIndex(parsed.Message, key.Account.PublicKey.KeyBytes);

            return SolanaTransactionFramer.ApplySignature(parsed, signerIndex, key.Account.Sign(parsed.Message));
        }
    }
}
