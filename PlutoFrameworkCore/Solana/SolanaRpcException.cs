namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// A Solana RPC call failed or returned an error result.
    ///
    /// Solnet reports failure through <c>RequestResult</c> rather than by throwing, so every
    /// call has to be checked and turned into this. A failed blockhash fetch treated as
    /// success would produce a transaction that dies later for an unrelated-looking reason.
    /// </summary>
    public class SolanaRpcException : Exception
    {
        public SolanaRpcException(string message) : base(message) { }
    }
}
