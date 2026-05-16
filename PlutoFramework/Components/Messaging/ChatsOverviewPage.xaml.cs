using System.Collections.ObjectModel;
using PlutoFramework.Templates.PageTemplate;
using PlutoFramework.Model;
using PlutoFramework.Model.Messaging;
using System.Threading.Tasks;

namespace PlutoFramework.Components.Messaging;

public partial class ChatsOverviewPage : PageTemplate
{
    public ObservableCollection<ChatItem> Chats { get; } = new();

    private int _currentOffset = 0;
    private const int BATCH_SIZE = 10;
    private bool _isLoading = false;
    private bool _hasMoreData = true;
    private readonly MessagingModel _messagingModel = new MessagingModel();
    
    private void AddChat(string title, string state, string time, bool isApproved)
    {
        var resources = Application.Current?.Resources;
        var positive = resources?["Positive"] as Color ?? Colors.Green;
        var negative = resources?["Negative"] as Color ?? Colors.Red;
        
        Chats.Add(new ChatItem
        {
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
                    AddChat(bucket.Name ?? "Unknown", "", "", true); // Use empty string for state
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
}