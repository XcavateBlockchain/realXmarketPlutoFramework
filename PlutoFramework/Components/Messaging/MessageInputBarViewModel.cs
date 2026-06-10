using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using PlutoFramework.Model;
using PlutoFramework.Model.Messaging;

namespace PlutoFramework.Components.Messaging;

public class MessageInputBarViewModel
{
    private string _message = string.Empty;
    private readonly MessagingModel _messagingModel;
    private readonly int _namespaceId;
    private readonly int _bucketId;
    private readonly byte[] _bucketEncryptionKey;

    public string Message
    {
        get => _message;
        set => _message = value;
    }

    public ICommand SendButtonPressedCommand { get; }
    public ICommand AddButtonPressedCommand { get; }

    public MessageInputBarViewModel(MessagingModel messagingModel, int namespaceId, int bucketId, byte[] bucketEncryptionKey)
    {
        _messagingModel = messagingModel;
        _namespaceId = namespaceId;
        _bucketId = bucketId;
        _bucketEncryptionKey = bucketEncryptionKey;

        SendButtonPressedCommand = new Command(async () => await SendMessageAsync());
        AddButtonPressedCommand = new Command(OnAddButtonPressed);
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(_message))
        {
            var toast = Toast.Make("Message cannot be empty");
            await toast.Show();
            return;
        }

        try
        {
            await _messagingModel.UploadMessageAsync(_namespaceId, _bucketId, _message, _bucketEncryptionKey);

            // Clear the input after successful send
            _message = string.Empty;

            var toast = Toast.Make("Message sent successfully");
            await toast.Show();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex}");
            var toast = Toast.Make($"Failed to send message: {ex.Message}");
            await toast.Show();
        }
    }

    private void OnAddButtonPressed()
    {
        // TODO: Implement add button functionality (e.g., file attachment, emoji picker, etc.)
    }
}

