namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// A violation of the Mobile Wallet Adapter wire protocol: a malformed frame, a
    /// sequence number out of order, a bad handshake, or a JSON-RPC error from the wallet.
    ///
    /// Distinct from <see cref="System.Security.Cryptography.AuthenticationTagMismatchException"/>,
    /// which surfaces from AES-GCM when a frame's contents fail authentication.
    /// </summary>
    public class MwaProtocolException : Exception
    {
        public MwaProtocolException(string message) : base(message) { }

        public MwaProtocolException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// The user declined the request in their wallet, or the wallet reported the
    /// authorization as no longer valid. Distinguished from a protocol fault because it
    /// is a normal outcome that the UI should present calmly.
    /// </summary>
    public class MwaAuthorizationException : MwaProtocolException
    {
        public MwaAuthorizationException(string message) : base(message) { }
    }
}
