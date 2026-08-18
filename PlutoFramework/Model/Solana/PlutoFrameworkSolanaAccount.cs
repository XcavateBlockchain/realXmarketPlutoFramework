using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;
using PlutoFramework.Model.SQLite;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using SolanaPublicKey = Solnet.Wallet.PublicKey;

namespace PlutoFramework.Model.Solana
{
    /// <summary>
    /// The app's Solana account, whichever way its key is held.
    ///
    /// Two implementations sit behind this: a locally derived BIP39 account that signs
    /// offline, and a wallet app reached over Mobile Wallet Adapter that signs remotely.
    /// Callers should not need to know which.
    ///
    /// This cannot mirror <see cref="KeysModel.GetAccountAsync"/>, which hands callers a
    /// Substrate <c>Account</c> to sign with directly. That works only because both Substrate
    /// variants keep a secret on the device. Mobile Wallet Adapter keeps none, so this type
    /// exposes operations rather than a key.
    /// </summary>
    public abstract class PlutoFrameworkSolanaAccount
    {
        /// <summary>
        /// A transaction whose only signer is its fee payer, which is this account.
        /// Multi-signature transactions are out of scope.
        /// </summary>
        protected const int REQUIRED_SIGNATURES = 1;

        /// <summary>Base58 Solana address.</summary>
        public abstract string Address { get; }

        public abstract string DisplayName { get; }

        public abstract KeyTypeEnum KeyType { get; }

        /// <summary>
        /// False under Mobile Wallet Adapter, where every signature needs a round trip
        /// through the wallet app and the user's approval. Useful for warning before an
        /// operation that will visibly leave the app.
        /// </summary>
        public abstract bool CanSignLocally { get; }

        /// <summary>
        /// The network the whole app operates on, never a per-account value. An MWA
        /// authorization records the network it was granted on separately, and is
        /// reauthorized onto this one when they disagree.
        /// </summary>
        public SolanaCluster Cluster => SolanaNetworkModel.SelectedCluster;

        /// <summary>
        /// Loads whichever Solana account is configured, or null when there is none.
        /// The two key variants are mutually exclusive, so at most one exists.
        /// </summary>
        /// <remarks>
        /// This unlocks the stored key, hence the reason. Callers that only need an address
        /// to display should use <see cref="KeysModel.GetSolanaAddressAsync"/>, which reads
        /// the stored public key without unlocking anything.
        /// </remarks>
        public static async Task<PlutoFrameworkSolanaAccount?> ResolveAsync(
            string reason = "Get access to your Solana account",
            CancellationToken token = default)
        {
            var lockedKey = (await KeysDatabase.GetAllKeysOfTypeAsync(
                KeyTypeEnum.SolanaMnemonic, KeyTypeEnum.SolanaMwa)).FirstOrDefault();

            if (lockedKey is null)
            {
                return null;
            }

            PlutoFrameworkSolanaAccount? account = lockedKey.Type switch
            {
                KeyTypeEnum.SolanaMnemonic =>
                    new MnemonicSolanaAccount(await lockedKey.ToSolanaMnemonicKeyAsync(reason)),

                KeyTypeEnum.SolanaMwa =>
                    new MwaSolanaAccount(lockedKey, await lockedKey.ToSolanaMwaKeyAsync(reason)),

                _ => null,
            };

            if (account is not null)
            {
                // A notifications wallet link that failed earlier (offline, device not yet
                // registered) retries here, riding on an account something else already
                // unlocked. Fire-and-forget, never prompts, skips itself when linked.
                _ = WalletLinkModel.TryLinkResolvedSolanaAccountAsync(account);
            }

            return account;
        }

        /// <summary>
        /// Signs an arbitrary message and returns the 64-byte signature. No network involved
        /// for either variant, though Mobile Wallet Adapter still needs the wallet app.
        /// </summary>
        public abstract Task<byte[]> SignMessageAsync(byte[] message, string reason, CancellationToken token);

        /// <summary>
        /// Builds a transaction from the given instructions, has it signed, and submits it.
        /// Returns the base58 transaction signature.
        /// </summary>
        /// <remarks>
        /// Returns as soon as the transaction is submitted. It does not wait for confirmation.
        /// </remarks>
        public async Task<string> SendAsync(
            IEnumerable<TransactionInstruction> instructions,
            string reason,
            CancellationToken token)
        {
            var instructionList = instructions.ToList();

            if (instructionList.Count == 0)
            {
                throw new ArgumentException("A transaction needs at least one instruction", nameof(instructions));
            }

            var cluster = Cluster;

            // Fetched here rather than in each subclass: both need it, and a transaction
            // without a recent blockhash is rejected regardless of who signs it.
            var blockHash = await SolanaRpcModel.GetLatestBlockHashAsync(cluster, token);

            var builder = new TransactionBuilder()
                .SetRecentBlockHash(blockHash)
                .SetFeePayer(new SolanaPublicKey(Address));

            foreach (var instruction in instructionList)
            {
                builder.AddInstruction(instruction);
            }

            return await SignAndSubmitAsync(builder, cluster, reason, token);
        }

        /// <summary>
        /// The only step that differs between a local and a remote signer. Implementations
        /// either sign and submit themselves, or hand the transaction to the wallet to do both.
        /// </summary>
        protected abstract Task<string> SignAndSubmitAsync(
            TransactionBuilder builder,
            SolanaCluster cluster,
            string reason,
            CancellationToken token);

        /// <summary>
        /// Signs an already-serialized transaction and returns it with this account's
        /// signature filled in. Signatures other signers have already applied are preserved.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="SendAsync"/>, the transaction arrives fully built — an injected
        /// dapp constructs its own and hands over the bytes. Only implemented where a local
        /// key exists: Mobile Wallet Adapter 2.0 deprecated <c>sign_transactions</c>, so a
        /// wallet app may refuse to sign without submitting.
        /// </remarks>
        public abstract Task<byte[]> SignWireTransactionAsync(
            byte[] wireTransaction,
            string reason,
            CancellationToken token);

        /// <summary>
        /// Signs an already-serialized transaction and submits it, returning the 64-byte
        /// transaction signature.
        /// </summary>
        /// <param name="cluster">
        /// The network to submit on, which a caller relaying a dapp's request takes from that
        /// request rather than from <see cref="Cluster"/>. The dapp chose its own RPC
        /// endpoint, and signing against a different network would fail on submission.
        /// </param>
        public abstract Task<byte[]> SignAndSendWireTransactionAsync(
            byte[] wireTransaction,
            SolanaCluster cluster,
            string reason,
            CancellationToken token);
    }
}
