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
            // Framed by hand rather than via Build(key): Build emits a single signature
            // slot no matter how many the message's header requires, and a node rejects
            // a slot count that disagrees with the header as a malformed ("failed to
            // sanitize accounts offsets") transaction. The framer sizes the slots to the
            // header and signs only this account's slot; a message that needs other
            // signers then fails on submission with the real reason - a missing
            // signature, not a malformed-transaction error.
            var compiled = builder.CompileMessage();

            var framed = SolanaTransactionFramer.FrameUnsigned(
                compiled, SolanaTransactionFramer.GetRequiredSignatures(compiled));

            var parsed = SolanaTransactionFramer.Parse(framed);

            var signerIndex = SolanaTransactionFramer.FindSignerIndex(parsed.Message, key.Account.PublicKey.KeyBytes);

            var signedTransaction = SolanaTransactionFramer.ApplySignature(
                parsed, signerIndex, key.Account.Sign(parsed.Message));

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
