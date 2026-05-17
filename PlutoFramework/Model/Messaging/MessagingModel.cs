using MessagingSubquery;
using NBitcoin.Secp256k1;
using NSec.Cryptography;
using PlutoFrameworkCore.AssetDidComm;
using PlutoFrameworkCore.Keys;
using StrawberryShake;
using XcavatePaseo.NetApi.Generated.Storage;
using Substrate.NetApi.Model.Types.Primitive;
using Substrate.NetApi.Model.Types.Base;
using XcavatePaseo.NetApi.Generated.Model.pallet_bucket.types;
using XcavatePaseo.NetApi.Generated.Model.bounded_collections.bounded_vec;
using XcavatePaseo.NetApi.Generated.Model.bounded_collections.bounded_btree_map;
using XcavatePaseo.NetApi.Generated.Types.Base;

namespace PlutoFramework.Model.Messaging;

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
    private readonly IStorageAdapter _storageAdapter;

    public MessagingModel(IStorageAdapter adapter)
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection
            .AddMessagingSubquery()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://index-api.onfinality.io/sq/7396860564255539200/xcavate-indexer"));

        IServiceProvider services = serviceCollection.BuildServiceProvider();

        _client = services.GetRequiredService<IMessagingSubquery>();

        _storageAdapter = adapter;
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
    public async Task<IReadOnlyList<IBucketMessages_Messages_Nodes?>> GetBucketMessagesByBucketIdAsync(int bucketId, string? after = null, CancellationToken cancellationToken = default)
    {
        var result = await _client.BucketMessages.ExecuteAsync(bucketId.ToString(), after, cancellationToken);
        result.EnsureNoErrors();

        return result.Data?.Messages?.Nodes ?? [];
    }

    /// <summary>
    /// Gets detailed information about a specific bucket including its admins, contributors, and messages.
    /// </summary>
    /// <param name="bucketId">The ID of the bucket to retrieve details for.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>Detailed bucket information including all related data.</returns>
    public async Task<IBucketDetail_Bucket?> GetBucketDetailAsync(int bucketId, CancellationToken cancellationToken = default)
    {
        var result = await _client.BucketDetail.ExecuteAsync(bucketId.ToString(), cancellationToken);
        result.EnsureNoErrors();

        return result.Data?.Bucket;
    }

    /// <summary>
    /// Gets the bucket encryption key by fetching the Pinata CID from indexer via didncomm tag and decoding the message using x25519 KeysModel.
    /// </summary>
    public async Task<string?> GetBucketEncryptionKeyAsync(int bucketId, CancellationToken cancellationToken = default)
    {
        var result = await _client.BucketMessagesByTag.ExecuteAsync(bucketId.ToString(), "didcomm/key-sharing-v1", null, cancellationToken);
        result.EnsureNoErrors();

        var message = result.Data?.Messages?.Nodes?.FirstOrDefault();
        if (message == null) return null;

        var cid = message.Reference;
        if (string.IsNullOrWhiteSpace(cid)) return null;

        var genericKey = await KeysModel.GetKeyOfTypeAsync(KeyTypeEnum.EncryptionX25519);
        if (genericKey == null) return null;

        var x25519Key = await genericKey.ToEncryptionX25519KeyAsync();
        using var privKey = Key.Import(
            KeyAgreementAlgorithm.X25519,
            x25519Key.SecretKey,
            KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            }
        );

        var data = await _storageAdapter.DownloadAsync(cid);
        if (data == null) return null;

        return JweModel.DecryptForRecipient(data.ToString()!, privKey);
    }

    /// <summary>
    /// Gets all messages for a specific bucket from the indexer and decrypts their contents.
    /// </summary>
    public async Task<DecryptedMessagesPage> GetDecryptedBucketMessagesAsync(int bucketId, byte[] bucketEncryptionKey, string? after = null, CancellationToken cancellationToken = default)
    {
        var result = await _client.BucketMessages.ExecuteAsync(bucketId.ToString(), after, cancellationToken);
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

    /// <summary>
    /// Gets and decrypts a bucket message node using the bucket encryption key.
    /// </summary>
    public async Task<string?> GetDecryptedMessageAsync(IBucketMessages_Messages_Nodes message, byte[] bucketEncryptionKey)
    {
        if (message == null) return null;

        var cid = message.Reference;
        if (cid == null) return null;

        using var privKey = Key.Import(
            KeyAgreementAlgorithm.X25519,
            bucketEncryptionKey,
            KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            }
        );

        var data = await _storageAdapter.DownloadAsync(cid);
        if (data == null) return null;

        return JweModel.DecryptCompact(data.ToString()!, privKey);
    }

    public async Task UploadMessageAsync(int namespaceId, int bucketId, string messageContent, byte[] bucketEncryptionKey)
    {
        using var privKey = Key.Import(
            KeyAgreementAlgorithm.X25519,
            bucketEncryptionKey,
            KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters
            {
                ExportPolicy = KeyExportPolicies.AllowPlaintextExport
            }
        );

        var encryptedMessage = JweModel.EncryptCompact(messageContent, privKey);
        var cid = await _storageAdapter.UploadAsync(encryptedMessage);

        // Create BoundedVecT17 from CID string (convert to UTF8 bytes)
        var cidByteArray = System.Text.Encoding.UTF8.GetBytes(cid);
        var cidU8Array = new U8[cidByteArray.Length];
        for (int i = 0; i < cidByteArray.Length; i++)
        {
            cidU8Array[i] = new U8(cidByteArray[i]);
        }
        var cidVec = new BaseVec<U8>(cidU8Array);

        //// Create empty metadata for the message
        //var emptyVec = new BaseVec<U8>();
        //var emptyBTreeMap = new BTreeMapT4();
        //var emptyMap = new BoundedBTreeMapT3 { Value = emptyBTreeMap };
        //var contentHashArray = new U8[32];
        //for (int i = 0; i < 32; i++)
        //{
        //    contentHashArray[i] = new U8(0);
        //}

        var messageInput = new MessageInput
        {
            Reference = new BoundedVecT17 { Value = cidVec },
            //Tag = new BaseOpt<BoundedVecT17>(),
            //MetadataInput = new MessageMetadataInput
            //{
            //    Description = new BoundedVecT12 { Value = emptyVec },
            //    ContentType = new BoundedVecT16 { Value = emptyVec },
            //    ContentHash = new Arr32U8 { Value = contentHashArray },
            //    Properties = emptyMap
            //}
        };

        var nsId = new U128(namespaceId);
        var bId = new U128(bucketId);

        BucketsCalls.Write(nsId, bId, messageInput);

        // TODO: Create and sign the extrinsic with the pallet bucket write call
        // The actual extrinsic creation and signing would depend on the chain connection setup
        // This would typically involve:
        // 1. Creating the Call object with write parameters (nsId, bId, messageInput)
        // 2. Wrapping it in an extrinsic
        // 3. Signing with the user's account
        // 4. Submitting to the chain via RPC
    }
}
