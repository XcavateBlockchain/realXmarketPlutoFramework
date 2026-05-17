using MessagingSubquery;
using Microsoft.AspNetCore.WebUtilities;
using NBitcoin.Secp256k1;
using NSec.Cryptography;
using PlutoFramework.Components.TransactionAnalyzer;
using PlutoFramework.Constants;
using PlutoFrameworkCore.AssetDidComm;
using PlutoFrameworkCore.Keys;
using PolkadotAssetHub.NetApi.Generated.Model.sp_core.crypto;
using StrawberryShake;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Primitive;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XcavatePaseo.NetApi.Generated.Model.bounded_collections.bounded_btree_map;
using XcavatePaseo.NetApi.Generated.Model.bounded_collections.bounded_vec;
using XcavatePaseo.NetApi.Generated.Model.pallet_bucket.types;
using XcavatePaseo.NetApi.Generated.Storage;

namespace PlutoFramework.Model.Messaging;

public class DecryptedMessage
{
    public required string Id { get; init; }
    public required string BucketId { get; init; }
    public int MessageId { get; init; }
    public required string Contributor { get; init; }
    public string? ContentType { get; init; }
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
    public async Task<byte[]?> GetBucketEncryptionKeyAsync(int bucketId, CancellationToken cancellationToken = default)
    {
        var result = await _client.BucketMessagesByTag.ExecuteAsync(bucketId.ToString(), "didcomm/key-sharing-v1", null, cancellationToken);
        result.EnsureNoErrors();

        var message = result.Data?.Messages?.Nodes?.FirstOrDefault();
        if (message == null) return null;

        var cid = message.Reference;
        if (string.IsNullOrWhiteSpace(cid)) return null;
        var x25519Key = await KeysModel.GetX25519KeyAsync();
        if (x25519Key == null) return null;
        using var privKey = X25519Model.ToKey(x25519Key.SecretKey);

        var data = await _storageAdapter.DownloadAsync(cid);
        if (data == null) return null;

        string? decryptedJson = JweModel.DecryptForRecipient(Encoding.UTF8.GetString(data), privKey);
        if (string.IsNullOrWhiteSpace(decryptedJson)) return null;

        try
        {
            var jsonNode = JsonNode.Parse(decryptedJson);

            var encKey = jsonNode?["keys"]?[0]?["d"]?.ToString();
            if (string.IsNullOrWhiteSpace(encKey)) return null;

            return WebEncoders.Base64UrlDecode(encKey);
        }
        catch (JsonException)
        {
            return null;
        }
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
        using var encKey = X25519Model.ToKey(bucketEncryptionKey);
        foreach (var node in nodes)
        {
            if (node == null || node.Tag == "didcomm/key-sharing-v1" || node.IpfsContent == null) continue;

            var decryptedContent = JweModel.DecryptCompact(node.IpfsContent, encKey);
            Console.WriteLine($"Decrypted content for message {node.Id}: {decryptedContent}");

            decryptedMessages.Add(new DecryptedMessage
            {
                Id = node.Id,
                BucketId = node.BucketId,
                MessageId = node.MessageId,
                Contributor = node.Contributor,
                ContentType = node.ContentType,
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

    public async Task UploadMessageAsync(int namespaceId, int bucketId, string messageContent, byte[] bucketEncryptionKey)
    {
        using var privKey = X25519Model.ToKey(bucketEncryptionKey);

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

        // Create empty metadata for the message
        var emptyVec = new BaseVec<U8>();
        var emptyBTreeMap = new XcavatePaseo.NetApi.Generated.Types.Base.BTreeMapT4();
        var emptyMap = new BoundedBTreeMapT3 { Value = emptyBTreeMap };
        var contentHashArray = new U8[32];
        for (int i = 0; i < 32; i++)
        {
            contentHashArray[i] = new U8(0);
        }

        var messageInput = new MessageInput
        {
            Reference = new BoundedVecT17 { Value = cidVec },
            Tag = new BaseOpt<BoundedVecT17>(),
            MetadataInput = new MessageMetadataInput
            {
                Description = new BoundedVecT12 { Value = emptyVec },
                ContentType = new BoundedVecT16 { Value = emptyVec },
                ContentHash = new XcavatePaseo.NetApi.Generated.Types.Base.Arr32U8 { Value = contentHashArray },
                Properties = emptyMap
            }
        };

        var nsId = new U128(namespaceId);
        var bId = new U128(bucketId);

        var method = BucketsCalls.Write(nsId, bId, messageInput);

        var client = await SubstrateClientModel.GetOrAddSubstrateClientAsync(
            EndpointEnum.XcavatePaseo,
            CancellationToken.None
        );

        var transactionAnalyzerConfirmationViewModel = DependencyService.Get<TransactionAnalyzerConfirmationViewModel>();
        await transactionAnalyzerConfirmationViewModel.LoadAsync(
            client,
            method,
            showDAppView: false,
            token: CancellationToken.None
        );
    }
}
