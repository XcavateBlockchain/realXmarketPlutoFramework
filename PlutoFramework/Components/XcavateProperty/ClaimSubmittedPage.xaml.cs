using CommunityToolkit.Mvvm.Input;

namespace PlutoFramework.Components.XcavateProperty
{
    /// <summary>
    /// The confirmation a lawyer lands on after claim_spv_case confirms: their candidacy
    /// stands in the listing's SPV-lawyer election and the token holders are voting on it.
    /// </summary>
    public partial class ClaimSubmittedPage
    {
        public ClaimSubmittedPage()
        {
            InitializeComponent();

            BindingContext = this;
        }

        [RelayCommand]
        public Task MyProfileAsync() => Shell.Current.GoToAsync("//LawyerAccount");

    }
}
