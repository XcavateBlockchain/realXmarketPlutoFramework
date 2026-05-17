using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using PlutoFramework.Templates.PageTemplate;
using PlutoFramework.Model;
using PlutoFramework.Model.Messaging;

namespace PlutoFramework.Components.Messaging;

public partial class ChatsOverviewPage : PageTemplate
{
    public ObservableCollection<ChatItem> Chats { get; } = new();

    private int _currentOffset = 0;
    private const int BATCH_SIZE = 10;
    private bool _isLoading = false;
    private bool _hasMoreData = true;
    private readonly MessagingModel _messagingModel = new(new PinataStorageAdapter());
    
    private void AddChat(string bucketId, string title, string state, string time, bool isApproved)
    {
        var resources = Application.Current?.Resources;
        var positive = resources?["Positive"] as Color ?? Colors.Green;
        var negative = resources?["Negative"] as Color ?? Colors.Red;
        
        Chats.Add(new ChatItem
        {
            BucketId = bucketId,
            Title = title,
            State = state,
            Time = time,
            IsApproved = isApproved ? "Approved" :  "Rejected",
            IsApprovedColor = isApproved ? positive : negative,
            IsApprovedBgColor = (isApproved ? positive : negative).WithAlpha(0.15f)
        });
    }
    
    public ChatsOverviewPage()
    {
        InitializeComponent();
        
        BindingContext = this;

        LoadDataAsync();
    }

    private async Task OnChatTappedAsync(ChatItem chat)
    {
        if (chat == null) return;

        try
        {
            var encryptionKey = await _messagingModel.GetBucketEncryptionKeyAsync(chat.BucketId);
            if (encryptionKey != null)
            {
                var encKeyBytes = System.Text.Encoding.UTF8.GetBytes(encryptionKey);
                await Shell.Current.Navigation.PushAsync(new MessagingOverviewPage(_messagingModel, chat.BucketId, encKeyBytes));
            }
            else
            {
                var toast = Toast.Make("Could not retrieve encryption key for this bucket");
                await toast.Show();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening chat: {ex}");
            var toast = Toast.Make($"Failed to open chat: {ex.Message}");
            await toast.Show();
        }
    }

    private async Task LoadDataAsync()
    {
        if (_isLoading || !_hasMoreData) return;

        _isLoading = true;

        var address = KeysModel.GetSubstrateKey();

        try
        {
            var result = await _messagingModel.ReadBucketBatchAsync(address, BATCH_SIZE, _currentOffset);

            if (result != null)
            {
                if (result.Count < BATCH_SIZE)
                {
                    _hasMoreData = false;
                }

                foreach (var bucket in result)
                {
                    AddChat(bucket.Id ?? "", bucket.Name ?? "Unknown", "", "", true);
                }

                _currentOffset += result.Count;
            }
            else
            {
                _hasMoreData = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnRemainingItemsThresholdReached(object sender, EventArgs e)
    {
        _ = LoadDataAsync();
    }

    private void OnChatItemTapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is ChatItem chat)
        {
            _ = OnChatTappedAsync(chat);
        }
    }
}
