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

    /// <summary>
    /// The wallet approved and signed the transaction but could not get it onto the
    /// network: its own RPC connection failed, or the blockhash had expired by
    /// submission time. Distinct from a decline - the user said yes and the delivery
    /// failed - and from a protocol fault, which would mean the two apps disagreed.
    /// </summary>
    public class MwaNotSubmittedException : MwaProtocolException
    {
        public MwaNotSubmittedException(string message) : base(message) { }
    }
}
