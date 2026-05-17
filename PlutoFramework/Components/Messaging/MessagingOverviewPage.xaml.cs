using PlutoFramework.Templates.PageTemplate;
using PlutoFramework.Model;
using PlutoFramework.Model.Messaging;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Messaging;

public partial class MessagingOverviewPage : PageTemplate
{
    public ObservableCollection<Message> Messages { get; } = new();

    private readonly int _namespaceId;
    private readonly int _bucketId;
    private byte[]? _bucketEncryptionKey;
    private string? _currentCursor;
    private bool _hasMoreData = true;
    private bool _isLoading = false;
    private readonly MessagingModel _messagingModel;

    public MessagingOverviewPage(MessagingModel model, int namespaceId, int bucketId, byte[] bucketEncryptionKey)
    {
        InitializeComponent();

        _messagingModel = model;
        _namespaceId = namespaceId;
        _bucketId = bucketId;
        _bucketEncryptionKey = bucketEncryptionKey;

        BindingContext = this;

        LoadMessagesAsync();

        // Set the MessageInputBarView properties after InitializeComponent
        SetupMessageInputBar();
    }

    private void SetupMessageInputBar()
    {
        var messageInputBar = this.FindByName<MessageInputBarView>("MessageInputBar");
        if (messageInputBar != null)
        {
            messageInputBar.MessagingModel = _messagingModel;
            messageInputBar.NamespaceId = _namespaceId;
            messageInputBar.BucketId = _bucketId;
            messageInputBar.BucketEncryptionKey = _bucketEncryptionKey;
        }
    }

    private async Task LoadMessagesAsync()
    {
        if (_isLoading || !_hasMoreData) return;

        _isLoading = true;

        try
        {
            var userAddress = Substrate.NetApi.Utils.GetAddressFrom(Substrate.NetApi.Utils.GetPublicKeyFrom(KeysModel.GetSubstrateKey()), 0);
            if (userAddress == null) return;

            var page = await _messagingModel.GetDecryptedBucketMessagesAsync(
                _bucketId,
                _bucketEncryptionKey!,
                _currentCursor
            );

            foreach (var msg in page.Messages)
            {
                var messageType = msg.Contributor == userAddress
                    ? Message.MessageType.Outgoing
                    : Message.MessageType.Incoming;

                Console.WriteLine($"Loaded message: {msg.Id}, Type: {messageType}, ContentType: {msg.ContentType}");

                if (msg.ContentType == "text/plain;charset=utf-8")
                {
                    AddMessage(
                        msg.DecryptedContent ?? "[Unable to decrypt]",
                        messageType,
                        messageType == Message.MessageType.Incoming ? msg.Contributor : null,
                        msg.CreatedBlock.ToString(),
                        null
                    );
                }
                else
                {
                    AddMessage(
                        msg.ContentType == null ? $"[Unsupported content type: {msg.ContentType}]" : "No content type",
                        messageType,
                        messageType == Message.MessageType.Incoming ? msg.Contributor : null,
                        msg.CreatedBlock.ToString(),
                        Colors.Red
                    );
                }

                _hasMoreData = page.HasNextPage;
                _currentCursor = page.EndCursor;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading messages: {ex}");
            AddStatus($"Error loading messages: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void AddMessage(string text, Message.MessageType type, string? sender, string? timestamp, Color? msgColor)
    {
        const int MaxLength = 500;

        if (!string.IsNullOrEmpty(text) && text.Length > MaxLength)
        {
            text = text.Substring(0, MaxLength) + "...";
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Messages.Add(new Message
            {
                Text = text,
                Type = type,
                Sender = sender,
                Timestamp = timestamp,
                MsgColor = msgColor ?? Application.Current.Resources["Primary"] as Color
            });
        });
    }

    private void AddIncoming(string sender, string text, string timestamp, Color? msgColor)
    {
        AddMessage(text, Message.MessageType.Incoming, sender, timestamp, msgColor);
    }

    private void AddOutgoing(string text, string timestamp)
    {
        AddMessage(text, Message.MessageType.Outgoing, null, timestamp, null);
    }

    private void AddStatus(string text)
    {
        AddMessage(text, Message.MessageType.Status, null, null, null);
    }

    private void OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
        _ = LoadMessagesAsync();
    }
}