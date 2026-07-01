using CommunityToolkit.Mvvm.ComponentModel;
using PlutoFramework.Model.Sumsub;

namespace PlutoFramework.Components.Sumsub
{
    public partial class SumsubRejectedView : ContentView
    {
        public SumsubRejectedView()
        {
            InitializeComponent();
            BindingContext = new SumsubRejectedViewModel();
        }

        public void Bind(SumsubStatusData viewData)
        {
            if (BindingContext is SumsubRejectedViewModel vm)
                vm.Populate(viewData);
        }
    }

    /// <summary>
    /// Displays a rejected verification status with reason and timestamp.
    /// </summary>
    public partial class SumsubRejectedViewModel : ObservableObject
    {
        [ObservableProperty] private bool showReason;
        [ObservableProperty] private string reason = "";
        [ObservableProperty] private bool hasTimestamp;
        [ObservableProperty] private string timestampText = "";
        [ObservableProperty] private bool hasAttempts;
        [ObservableProperty] private string attemptsText = "";

        public void Populate(SumsubStatusData viewData)
        {
            if (viewData.StatusType != SumsubStatusType.Rejected)
                throw new InvalidOperationException("Expected rejected status data");

            ShowReason = !string.IsNullOrEmpty(viewData.Reason);
            Reason = viewData.Reason;

            TimestampText = $"Failed on {viewData.Timestamp:dddd, dd MMMM yyyy, HH:mm}";
            HasTimestamp = true;

            if (viewData.AttemptCount > 1)
            {
                AttemptsText = $"Previous attempt{(viewData.AttemptCount > 1 ? "s" : "")}: {viewData.AttemptCount}";
                HasAttempts = true;
            }
        }
    }
}
