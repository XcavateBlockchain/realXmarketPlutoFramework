using PlutoFramework.Templates.PageTemplate;
using PlutoFramework.Model;
using PlutoFramework.Model.Messaging;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Messaging;

public partial class MessagingOverviewPage : PageTemplate
{
    public ObservableCollection<Message> Messages { get; } = new();

    private readonly string _bucketId;
    private byte[]? _bucketEncryptionKey;
    private string? _currentCursor;
    private bool _hasMoreData = true;
    private bool _isLoading = false;
    private readonly MessagingModel _messagingModel;

    public MessagingOverviewPage(MessagingModel model, string bucketId, byte[] bucketEncryptionKey)
    {
        InitializeComponent();

        _messagingModel = model;
        _bucketId = bucketId;
        _bucketEncryptionKey = bucketEncryptionKey;

        scrollView.Padding = new Thickness(scrollView.Padding.Right, scrollView.Padding.Bottom,
            scrollView.Padding.Left, scrollView.Padding.Top);

        BindingContext = this;

        LoadMessagesAsync();
    }

    private async Task LoadMessagesAsync()
    {
        if (_isLoading || !_hasMoreData) return;

        _isLoading = true;

        try
        {
            var userAddress = KeysModel.GetSubstrateKey();
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

                AddMessage(
                    msg.DecryptedContent ?? "[Unable to decrypt]",
                    messageType,
                    messageType == Message.MessageType.Incoming ? msg.Contributor : null,
                    msg.CreatedBlock.ToString(),
                    null
                );
            }

            _hasMoreData = page.HasNextPage;
            _currentCursor = page.EndCursor;
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
        Messages.Add(new Message
        {
            Text = text,
            Type = type,
            Sender = sender,
            Timestamp = timestamp,
            MsgColor = msgColor ?? Application.Current.Resources["Primary"] as Color
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