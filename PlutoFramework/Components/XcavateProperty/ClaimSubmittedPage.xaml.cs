using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

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

        /// <summary>
        /// Back to the property page. There is no on-chain cancel for a candidacy - it
        /// lapses when its election round ends - so this is navigation only.
        /// </summary>
        [RelayCommand]
        public Task CancelClaimAsync() => NavigationModel.PopAsync();
    }
}
