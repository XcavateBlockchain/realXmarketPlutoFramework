namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// The waiting popup for Mobile Wallet Adapter transaction signing. Names the
    /// transaction from the caller's reason string; everything else - the spinner, the
    /// Cancel and Open Wallet buttons, the session teardown - is the shared base.
    /// </summary>
    public partial class MwaSignPopupViewModel : MwaSigningPopupViewModel
    {
        public override string Description => string.IsNullOrWhiteSpace(Reason)
            ? "Approve the transaction request."
            : $"Approve the {Reason} transaction request.";
    }
}
