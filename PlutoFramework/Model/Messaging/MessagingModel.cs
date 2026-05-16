using MessagingSubquery;
using PlutoFramework.Model;
using PlutoFrameworkCore.AssetDidComm;
using PlutoFrameworkCore.Keys;
using StrawberryShake;

namespace PlutoFramework.Model.Messaging
{
    public class DecryptedMessage
    {
        public required string Id { get; init; }
        public required string BucketId { get; init; }
        public int MessageId { get; init; }
        public required string Contributor { get; init; }
        public string? DecryptedContent { get; init; }
        public int CreatedBlock { get; init; }
    }

    public class DecryptedMessagesPage
    {
        public required IReadOnlyList<DecryptedMessage> Messages { get; init; }
        public string? EndCursor { get; init; }
        public bool HasNextPage { get; init; }
    }

    public class MessagingModel
    {
        private readonly IMessagingSubquery _client;

        public MessagingModel()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection
                .AddMessagingSubquery()
                .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://index-api.onfinality.io/sq/7396860564255539200/xcavate-indexer"));

            IServiceProvider services = serviceCollection.BuildServiceProvider();

            _client = services.GetRequiredService<IMessagingSubquery>();
        }

        /// <summary>
        /// Reads a batch of buckets by retrieving all buckets for the authenticated user.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>A collection of bucket information including metadata.</returns>
        public async Task<IReadOnlyList<IMyBuckets_Buckets_Nodes?>> ReadBucketBatchAsync(string address, int first = 25, int offset = 0, CancellationToken cancellationToken = default)
        {
            var result = await _client.MyBuckets.ExecuteAsync(address, first, offset, cancellationToken);

            result.EnsureNoErrors();

            return result.Data?.Buckets?.Nodes ?? [];
        }

        /// <summary>
        /// Gets all messages for a specific bucket by its ID.
        /// </summary>
        /// <param name="bucketId">The ID of the bucket to retrieve messages for.</param>
        /// <param name="after">Optional cursor for pagination support.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>A collection of messages for the bucket with pagination information.</returns>
        public async Task<IReadOnlyList<IBucketMessages_Messages_Nodes?>> GetBucketMessagesByBucketIdAsync(string bucketId, string? after = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(bucketId))
            {
                throw new ArgumentException("Bucket ID cannot be null or empty", nameof(bucketId));
            }

            var result = await _client.BucketMessages.ExecuteAsync(bucketId, after, cancellationToken);

            result.EnsureNoErrors();

            return result.Data?.Messages?.Nodes ?? [];
        }

        /// <summary>
        /// Gets detailed information about a specific bucket including its admins, contributors, and messages.
        /// </summary>
        /// <param name="bucketId">The ID of the bucket to retrieve details for.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>Detailed bucket information including all related data.</returns>
        public async Task<IBucketDetail_Bucket?> GetBucketDetailAsync(string bucketId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(bucketId))
            {
                throw new ArgumentException("Bucket ID cannot be null or empty", nameof(bucketId));
            }

            var result = await _client.BucketDetail.ExecuteAsync(bucketId, cancellationToken);

            result.EnsureNoErrors();

            return result.Data?.Bucket;
        }

        /// <summary>
        /// Gets the bucket encryption key by fetching the Pinata CID from indexer via didncomm tag and decoding the message using x25519 KeysModel.
        /// </summary>
        public async Task<string?> GetBucketEncryptionKeyAsync(string bucketId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(bucketId))
            {
                throw new ArgumentException("Bucket ID cannot be null or empty", nameof(bucketId));
            }

            var result = await _client.BucketMessagesByTag.ExecuteAsync(bucketId, "didcomm/key-sharing-v1", null, cancellationToken);

            result.EnsureNoErrors();

            var message = result.Data?.Messages?.Nodes?.FirstOrDefault();
            if (message == null)
            {
                return null;
            }

            var cid = message.ContentHash ?? message.IpfsContent ?? message.Reference;
            if (string.IsNullOrWhiteSpace(cid))
            {
                return null;
            }

            string pinataMsgUri = $"https://aquamarine-legal-boa-846.mypinata.cloud/ipfs/{cid}";

            var genericKey = await KeysModel.GetKeyOfTypeAsync(KeyTypeEnum.EncryptionX25519);
            if (genericKey == null)
            {
                return null;
            }

            var x25519Key = await genericKey.ToEncryptionX25519KeyAsync();
            byte[] privKeyBytes = x25519Key.SecretKey;

            var encKeyJson = await AssetDidCommModel.GetMessageFromUriAsync(pinataMsgUri, privKeyBytes);

            return encKeyJson;
        }

        /// <summary>
        /// Gets and decrypts a bucket message using its CID and the bucket encryption key.
        /// </summary>
        public async Task<string?> GetDecryptedMessageAsync(string cid, byte[] bucketEncryptionKey)
        {
            if (string.IsNullOrWhiteSpace(cid))
            {
                return null;
            }

            string pinataMsgUri = $"https://aquamarine-legal-boa-846.mypinata.cloud/ipfs/{cid}";

            var msg = await AssetDidCommModel.GetMessageFromUriAsync(pinataMsgUri, bucketEncryptionKey, true);

            return msg;
        }

        /// <summary>
        /// Gets and decrypts a bucket message node using the bucket encryption key.
        /// </summary>
        public Task<string?> GetDecryptedMessageAsync(IBucketMessages_Messages_Nodes message, byte[] bucketEncryptionKey)
        {
            if (message == null)
            {
                return Task.FromResult<string?>(null);
            }

            var cid = message.ContentHash ?? message.IpfsContent ?? message.Reference;

            return GetDecryptedMessageAsync(cid, bucketEncryptionKey);
        }

        /// <summary>
        /// Gets all messages for a specific bucket from the indexer and decrypts their contents.
        /// </summary>
        public async Task<DecryptedMessagesPage> GetDecryptedBucketMessagesAsync(string bucketId, byte[] bucketEncryptionKey, string? after = null, CancellationToken cancellationToken = default)
        {
            var result = await _client.BucketMessages.ExecuteAsync(bucketId, after, cancellationToken);

            result.EnsureNoErrors();

            var nodes = result.Data?.Messages?.Nodes ?? [];
            var pageInfo = result.Data?.Messages?.PageInfo;

            var decryptedMessages = new List<DecryptedMessage>();
            foreach (var node in nodes)
            {
                if (node == null) continue;

                var decryptedContent = await GetDecryptedMessageAsync(node, bucketEncryptionKey);

                decryptedMessages.Add(new DecryptedMessage
                {
                    Id = node.Id,
                    BucketId = node.BucketId,
                    MessageId = node.MessageId,
                    Contributor = node.Contributor,
                    DecryptedContent = decryptedContent,
                    CreatedBlock = node.CreatedBlock
                });
            }

            return new DecryptedMessagesPage
            {
                Messages = decryptedMessages,
                EndCursor = pageInfo?.EndCursor,
                HasNextPage = pageInfo?.HasNextPage ?? false
            };
        }
    }
}
