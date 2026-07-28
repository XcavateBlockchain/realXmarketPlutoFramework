using PlutoFramework.Model.Solana;
using PlutoFrameworkCore.Solana;
using System.Text;
using XcavateProfileApiClient.Signing;

namespace PlutoFramework.Model.Xcavate.Profile
{
    /// <summary>
    /// Signs profile API requests with the app's Solana account, whichever way its key is held.
    /// </summary>
    /// <remarks>
    /// The package ships its own <c>SolanaRequestSigner</c>, but its only constructor takes a
    /// <c>Solnet.Wallet.Account</c> and a Mobile Wallet Adapter wallet never surrenders a
    /// private key to build one. <see cref="PlutoFrameworkSolanaAccount"/> already hides that
    /// difference behind an async signing call, which is exactly the shape
    /// <see cref="IRequestSigner.SignAsync"/> wants.
    /// </remarks>
    internal sealed class SolanaAccountRequestSigner(PlutoFrameworkSolanaAccount account, string reason)
        : IRequestSigner
    {
        public string Address => account.Address;

        /// <summary>
        /// The raw UTF-8 payload, signed unhashed. Wallets render what they are handed as text
        /// in the approval prompt, so a digest would show the user binary garbage - and the
        /// server verifies these bytes, not a hash of them.
        /// </summary>
        /// <remarks>
        /// No cancellation token: <see cref="IRequestSigner"/> does not carry one. Under MWA
        /// this waits on the wallet app, which the user dismisses to cancel.
        /// </remarks>
        public Task<byte[]> SignAsync(string payload) =>
            account.SignMessageAsync(Encoding.UTF8.GetBytes(payload), reason, CancellationToken.None);

        public string EncodeSignature(byte[] signature) => SolanaBase58.Encode(signature);
    }
}
