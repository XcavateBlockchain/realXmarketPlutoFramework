using Chaos.NaCl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;
using Substrate.NET.Schnorrkel;
using Substrate.NetApi;
using Substrate.NetApi.Model.Types;

namespace PlutoFramework.Components.WebView
{
    public partial class WebSignRawPopupViewModel : ObservableObject, IPopup, ISetToDefault
    {
        public TaskCompletionSource<byte[]>? SignatureTask { get; set; } = null;

        /// <summary>
        /// Produces the signature over the raw message bytes. Left null for Substrate, which
        /// uses the built-in path below; the injected Solana wallet sets it so the same sheet
        /// serves both chains rather than a second one being built to look identical.
        /// </summary>
        public Func<byte[], Task<byte[]>>? Signer { get; set; } = null;

        [ObservableProperty]
        private string signButtonText = "Sign";

        [ObservableProperty]
        private ButtonStateEnum signButtonState = ButtonStateEnum.Enabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MessageString))]
        [NotifyPropertyChangedFor(nameof(MessageDecodedString))]
        private Plutonication.RawMessage? message = null;

        public string MessageString => Message?.data ?? "IDK";

        public string MessageDecodedString => Message is not null ? System.Text.Encoding.UTF8.GetString(Utils.HexToByteArray(Message.data)) : "";

        [ObservableProperty]
        private bool isVisible;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ErrorIsVisible))]
        private string errorText = "";

        public bool ErrorIsVisible => !string.IsNullOrEmpty(ErrorText);

        public WebSignRawPopupViewModel()
        {
            SetToDefault();
        }

        [RelayCommand]
        public async Task SignAsync()
        {
            SignButtonText = "Signing";
            SignButtonState = ButtonStateEnum.Disabled;

            try
            {
                byte[] msg = Utils.HexToByteArray(Message?.data);

                byte[] signature = Signer is not null
                    ? await Signer(msg)
                    : await SignWithSubstrateAccountAsync(msg);

                if (SignatureTask == null)
                {
                    return;
                }

                SignatureTask.TrySetResult(signature);

                // Hide this layout
                IsVisible = false;
            }
            catch (Exception ex)
            {
                // Left visible on purpose: the request is still open, so the user can retry
                // or reject rather than being dropped back into a page that is still waiting.
                ErrorText = ex.Message;
            }
            finally
            {
                SignButtonText = "Sign";
                SignButtonState = ButtonStateEnum.Enabled;
            }
        }

        /// <summary>
        /// The default signer: the app's Substrate account, hashing anything over the 256-byte
        /// limit the way the Polkadot extension does. Also used by the bridge's Profile API
        /// auto-sign path, so a message signs identically with or without the sheet.
        /// </summary>
        internal static async Task<byte[]> SignWithSubstrateAccountAsync(byte[] msg)
        {
            var account = await Model.KeysModel.GetAccountAsync()
                ?? throw new Exception("No Substrate account is available to sign with.");

            return SignWithAccount(account, msg);
        }

        /// <summary>
        /// <see cref="SignWithSubstrateAccountAsync(byte[])"/> without the password/biometric
        /// unlock: the key is read straight from secure storage. The messenger WebView's
        /// bridge uses it for its GraphQL calls, which the whitelisted dashboard signs on
        /// every state-changing request and which must never hold that page for a prompt.
        /// </summary>
        internal static async Task<byte[]> SignWithSubstrateAccountNoAuthAsync(byte[] msg)
        {
            var account = await Model.KeysModel.GetAccountNoAuthAsync()
                ?? throw new Exception("No Substrate account is available to sign with.");

            return SignWithAccount(account, msg);
        }

        /// <summary>
        /// The actual signing step, shared by the auth and no-auth signers so both produce
        /// an identical signature for a given account.
        /// </summary>
        private static byte[] SignWithAccount(Substrate.NetApi.Model.Types.Account account, byte[] msg)
        {
            if (msg.Length > 256) msg = HashExtension.Blake2(msg, 256);

            return account.KeyType switch
            {
                KeyType.Ed25519 => Ed25519.Sign(msg, account.PrivateKey),
                KeyType.Sr25519 => Sr25519v091.SignSimple(account.Bytes, account.PrivateKey, msg),
                _ => throw new Exception($"Unknown key type found '{account.KeyType}'."),
            };
        }

        [RelayCommand]
        public void Reject()
        {
            // Settle the request. Without this the dapp's promise never resolves and its page
            // waits forever on something the user has already dismissed. TrySet rather than
            // Set because this view model is a singleton and may still hold a finished task.
            SignatureTask?.TrySetException(
                new OperationCanceledException("The signature request was rejected."));

            IsVisible = false;
        }

        public void SetToDefault()
        {
            ErrorText = "";
            IsVisible = false;
            Message = null;
            Signer = null;
            SignButtonText = "Sign";
            SignButtonState = ButtonStateEnum.Enabled;
        }
    }
}

