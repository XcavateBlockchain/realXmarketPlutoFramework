using Solnet.Rpc;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Models;
using System.Collections.Concurrent;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Solana RPC access, scoped to what sending a transaction needs: a recent blockhash and
    /// a way to submit signed bytes.
    /// </summary>
    public static class SolanaRpcModel
    {
        /// <summary>
        /// One client per cluster, reused. Solnet's clients wrap an HttpClient, so creating
        /// one per call would leak sockets.
        /// </summary>
        private static readonly ConcurrentDictionary<SolanaCluster, IRpcClient> Clients = new();

        public static IRpcClient GetClient(SolanaCluster cluster) =>
            Clients.GetOrAdd(cluster, key => ClientFactory.GetClient(key.ToSolnetCluster()));

        /// <summary>
        /// A recent blockhash, which every transaction must carry to be accepted.
        /// </summary>
        public static async Task<string> GetLatestBlockHashAsync(SolanaCluster cluster, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var result = await GetClient(cluster).GetLatestBlockHashAsync();

            var blockHash = Unwrap(result, $"fetch a recent blockhash on {cluster.GetName()}")
                .Value?.Blockhash;

            if (string.IsNullOrEmpty(blockHash))
            {
                throw new SolanaRpcException($"{cluster.GetName()} returned an empty blockhash");
            }

            return blockHash;
        }

        /// <summary>
        /// Submits a signed transaction and returns its base58 signature.
        /// </summary>
        public static async Task<string> SendTransactionAsync(
            SolanaCluster cluster,
            byte[] signedTransaction,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var result = await GetClient(cluster).SendTransactionAsync(signedTransaction);

            var signature = Unwrap(result, $"submit the transaction on {cluster.GetName()}");

            if (string.IsNullOrEmpty(signature))
            {
                throw new SolanaRpcException($"{cluster.GetName()} accepted the transaction but returned no signature");
            }

            return signature;
        }

        /// <summary>
        /// The account's SOL balance, in lamports.
        /// </summary>
        public static async Task<ulong> GetLamportBalanceAsync(
            SolanaCluster cluster, string address, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var result = await GetClient(cluster).GetBalanceAsync(address);

            return Unwrap(result, $"fetch the SOL balance on {cluster.GetName()}").Value;
        }

        /// <summary>
        /// Every token account the address owns under one token program.
        /// </summary>
        /// <remarks>
        /// Filtered by program rather than by mint: the RPC accepts exactly one filter, and
        /// one call per whitelisted mint would multiply round trips against a rate-limited
        /// public endpoint.
        /// </remarks>
        public static async Task<List<TokenAccount>> GetTokenAccountsAsync(
            SolanaCluster cluster, string address, string programId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var result = await GetClient(cluster).GetTokenAccountsByOwnerAsync(
                address, tokenMintPubKey: null, tokenProgramId: programId);

            return Unwrap(result, $"fetch token accounts on {cluster.GetName()}").Value ?? [];
        }

        /// <summary>
        /// Returns the result, or throws with whatever reason the node gave.
        /// </summary>
        private static T Unwrap<T>(RequestResult<T> result, string attempted)
        {
            if (!result.WasSuccessful)
            {
                var reason = string.IsNullOrWhiteSpace(result.Reason) ? "no reason given" : result.Reason;

                throw new SolanaRpcException($"Could not {attempted}: {reason}");
            }

            return result.Result;
        }
    }
}
