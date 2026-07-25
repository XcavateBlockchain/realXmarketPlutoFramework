using PlutoFrameworkCore.Keys;
using PlutoFrameworkCore.Solana;
using PlutoFrameworkCore.Solana.Mwa;
using Solnet.Rpc.Builders;

namespace PlutoFramework.Model.Solana
{
    /// <summary>
    /// A Solana account held by a separate wallet app, reached over Mobile Wallet Adapter.
    ///
    /// Every operation is a fresh association: a session ends when the wallet hands control
    /// back, so nothing can be reused between calls. Each operation therefore authorizes and
    /// then does its real work inside that one session, which keeps it to a single trip
    /// through the wallet app.
    /// </summary>
    public sealed class MwaSolanaAccount : PlutoFrameworkSolanaAccount
    {
        private readonly GenericLockedKey lockedKey;

        private SolanaMwaKey key;

        internal MwaSolanaAccount(GenericLockedKey lockedKey, SolanaMwaKey key)
        {
            this.lockedKey = lockedKey;
            this.key = key;
        }

        public override string Address => key.Address;

        public override string DisplayName => key.DisplayName;

        public override KeyTypeEnum KeyType => KeyTypeEnum.SolanaMwa;

        public override bool CanSignLocally => false;

        /// <summary>
        /// The network this authorization was granted on, which may lag the app-wide
        /// <see cref="PlutoFrameworkSolanaAccount.Cluster"/> if the user changed it since.
        /// Signing reauthorizes onto the app-wide network rather than using this.
        /// </summary>
        public SolanaCluster AuthorizedCluster => key.Cluster;

        public override Task<byte[]> SignMessageAsync(byte[] message, string reason, CancellationToken token) =>
            RunAuthorizedAsync(
                async (client, operationToken) =>
                {
                    var signedPayloads = await client.SignMessagesAsync(Address, [message], operationToken);

                    if (signedPayloads.Count == 0)
                    {
                        throw new MwaProtocolException("The wallet returned no signed payload");
                    }

                    // sign_messages returns each message with its signature appended.
                    return SolanaTransactionFramer.ExtractSignature(signedPayloads[0]);
                },
                token);

        protected override Task<string> SignAndSubmitAsync(
            TransactionBuilder builder,
            SolanaCluster cluster,
            string reason,
            CancellationToken token) =>
            RunAuthorizedAsync(
                async (client, operationToken) =>
                {
                    // The wallet needs a wire-format transaction with an empty signature slot
                    // to fill in, which is not what Solnet's Serialize() produces unsigned.
                    var payload = SolanaTransactionFramer.FrameUnsigned(builder.CompileMessage(), REQUIRED_SIGNATURES);

                    var signatures = await client.SignAndSendTransactionsAsync([payload], operationToken);

                    if (signatures.Count == 0)
                    {
                        throw new MwaProtocolException("The wallet returned no transaction signature");
                    }

                    // The wallet submitted it; this app makes no RPC call for the send.
                    // Not PublicKey: that type rejects anything but 32 bytes, and a
                    // signature is 64.
                    return SolanaBase58.Encode(signatures[0]);
                },
                token);

        /// <summary>
        /// Opens a session, authorizes on the app-wide network using the stored token, keeps
        /// any refreshed authorization, then runs the operation in that same session.
        ///
        /// Passing the stored token lets the wallet reauthorize without reprompting. When the
        /// stored network differs from the app-wide one, this is also what moves the
        /// authorization across, rather than failing and asking the user to reconnect.
        /// </summary>
        private Task<T> RunAuthorizedAsync<T>(
            Func<MwaClient, CancellationToken, Task<T>> operation,
            CancellationToken token) =>
            MwaConnectFlow.WithAuthorizedSessionAsync(
                SolanaMwaModel.BuildIdentity(),
                Cluster,
                key.AuthToken,
                async (client, authorization, operationToken) =>
                {
                    await PersistIfRefreshedAsync(authorization);

                    return await operation(client, operationToken);
                },
                progress: null,
                token);

        /// <summary>
        /// Writes the authorization back when the wallet issued a new token, moved us to a
        /// different network, or switched account. Skipped when nothing changed, to avoid a
        /// secure-storage write on every signature.
        /// </summary>
        private async Task PersistIfRefreshedAsync(MwaAuthorizationResult authorization)
        {
            if (authorization.AuthToken == key.AuthToken &&
                authorization.Chain == key.Chain &&
                authorization.Address == key.Address)
            {
                return;
            }

            var refreshed = new SolanaMwaKey
            {
                AuthToken = authorization.AuthToken,
                Address = authorization.Address,
                Chain = authorization.Chain,
                WalletUriBase = authorization.WalletUriBase ?? key.WalletUriBase,
                AccountLabel = authorization.AccountLabel ?? key.AccountLabel,
            };

            // A changed address means a different account, and the database keys rows by
            // address, so the old row has to go rather than leaving two Solana keys behind.
            if (refreshed.Address != key.Address)
            {
                await lockedKey.RemoveAsync();
            }

            await KeysModel.SaveSolanaMwaKeyAsync(refreshed);

            key = refreshed;
        }
    }
}
