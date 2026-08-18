namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// Progress reported while connecting, so the UI can explain what it is waiting for
    /// instead of showing an undifferentiated spinner.
    /// </summary>
    public enum MwaConnectStage
    {
        LaunchingWallet,
        WaitingForWallet,
        Authorizing,
    }

    /// <summary>
    /// Drives a complete local association: generate an association keypair, hand the URI
    /// to a wallet app, wait for it to connect back, then authorize.
    /// </summary>
    public static class MwaConnectFlow
    {
        /// <summary>
        /// Thrown when no installed app can handle a <c>solana-wallet:</c> URI.
        /// </summary>
        public class NoWalletInstalledException : MwaProtocolException
        {
            public NoWalletInstalledException()
                : base("No Mobile Wallet Adapter compatible wallet is installed") { }
        }

        /// <summary>
        /// Thrown on platforms where the protocol cannot work at all.
        /// </summary>
        public class PlatformNotSupportedException : MwaProtocolException
        {
            public PlatformNotSupportedException()
                : base("Mobile Wallet Adapter is only available on Android") { }
        }

        /// <summary>
        /// True when this platform can attempt an association at all.
        /// </summary>
        public static bool IsSupported => PlutoConfigurationModel.MwaIntentLauncher?.IsSupported ?? false;

        /// <summary>
        /// Connects to a wallet and authorizes an account on the given cluster.
        /// </summary>
        /// <param name="existingAuthToken">
        /// Supply a previously stored token to reauthorize it, which the wallet may accept
        /// without prompting the user again.
        /// </param>
        public static Task<MwaAuthorizationResult> ConnectAsync(
            MwaIdentity identity,
            SolanaCluster cluster,
            string? existingAuthToken,
            IProgress<MwaConnectStage>? progress,
            CancellationToken token) =>
            WithAuthorizedSessionAsync(
                identity,
                cluster,
                existingAuthToken,
                (_, authorization, _) => Task.FromResult(authorization),
                progress,
                token);

        /// <summary>
        /// Opens an association, authorizes, then runs <paramref name="operation"/> inside the
        /// same still-open session before tearing it down.
        ///
        /// A session ends when the wallet app hands control back, so a privileged call cannot
        /// reuse an earlier one. Doing the authorize and the real work in a single session
        /// means one intent and one trip through the wallet rather than two.
        /// </summary>
        /// <param name="operation">
        /// Receives the client and the fresh authorization. Its result is returned to the caller.
        /// </param>
        public static async Task<T> WithAuthorizedSessionAsync<T>(
            MwaIdentity identity,
            SolanaCluster cluster,
            string? existingAuthToken,
            Func<MwaClient, MwaAuthorizationResult, CancellationToken, Task<T>> operation,
            IProgress<MwaConnectStage>? progress,
            CancellationToken token)
        {
            var launcher = PlutoConfigurationModel.MwaIntentLauncher;

            if (launcher is null || !launcher.IsSupported)
            {
                throw new PlatformNotSupportedException();
            }

            using var association = MwaAssociationKeypair.Generate();

            // We pick the port, so the wallet cannot be listening yet. The intent has to go
            // out before there is anything to connect to.
            var port = MwaAssociationUri.GeneratePort();

            var associationUri = MwaAssociationUri.BuildLocal(association.AssociationToken, port);

            progress?.Report(MwaConnectStage.LaunchingWallet);

            if (!await launcher.LaunchAsync(associationUri))
            {
                throw new NoWalletInstalledException();
            }

            progress?.Report(MwaConnectStage.WaitingForWallet);

            await using var session = await MwaSession.EstablishAsync(association, port, token);

            progress?.Report(MwaConnectStage.Authorizing);

            var client = new MwaClient(session);

            var authorization = await client.AuthorizeAsync(identity, cluster, existingAuthToken, token);

            return await operation(client, authorization, token);
        }

        /// <summary>
        /// Revokes an authorization with the wallet. Best-effort: the local key should be
        /// deleted regardless, since a user asking to disconnect must not be left
        /// connected because their wallet app was uninstalled or declined to respond.
        /// </summary>
        public static async Task<bool> TryDeauthorizeAsync(
            MwaIdentity identity,
            SolanaMwaKeyDeauthorizeRequest request,
            CancellationToken token)
        {
            var launcher = PlutoConfigurationModel.MwaIntentLauncher;

            if (launcher is null || !launcher.IsSupported)
            {
                return false;
            }

            try
            {
                using var association = MwaAssociationKeypair.Generate();

                var port = MwaAssociationUri.GeneratePort();

                if (!await launcher.LaunchAsync(MwaAssociationUri.BuildLocal(association.AssociationToken, port)))
                {
                    return false;
                }

                await using var session = await MwaSession.EstablishAsync(association, port, token);

                await new MwaClient(session).DeauthorizeAsync(request.AuthToken, token);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The token to revoke. A named type keeps the intent obvious at the call site, where
    /// a bare string next to other strings is easy to mix up.
    /// </summary>
    public record SolanaMwaKeyDeauthorizeRequest
    {
        public required string AuthToken { get; set; }
    }
}
