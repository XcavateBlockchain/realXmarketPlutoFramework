using CommunityToolkit.Mvvm.ComponentModel;
using PlutoFramework.Model.Sumsub;

namespace PlutoFramework.Components.Sumsub
{
    public partial class SumsubApprovedView : ContentView
    {
        public SumsubApprovedView()
        {
            InitializeComponent();
            BindingContext = new SumsubApprovedViewModel();
        }

        public void Bind(SumsubStatusData viewData)
        {
            if (BindingContext is SumsubApprovedViewModel vm)
                vm.Populate(viewData);
        }
    }

    /// <summary>
    /// Displays a verified/approved status indicator.
    /// </summary>
    public partial class SumsubApprovedViewModel : ObservableObject
    {
        [ObservableProperty] private string levelName = "Verified";
        [ObservableProperty] private bool hasTimestamp;
        [ObservableProperty] private string timestampText = "";

        public void Populate(SumsubStatusData viewData)
        {
            if (viewData.StatusType != SumsubStatusType.Approved)
                throw new InvalidOperationException("Expected approved status data");

            if (!string.IsNullOrEmpty(viewData.LevelName))
            {
                var role = viewData.LevelName.Split('-').LastOrDefault() ?? viewData.LevelName;
                LevelName = $"{role.Replace("-", " ")} role verified";
            }

            TimestampText = $"Verified on {viewData.Timestamp:dddd, dd MMMM yyyy}";
            HasTimestamp = true;
        }
    }
}