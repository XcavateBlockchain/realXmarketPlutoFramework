using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFramework.Components.Solana
{
    /// <summary>
    /// The waiting popup for Mobile Wallet Adapter message signing. Same shape as the
    /// transaction variant, plus the message itself: the user should be able to compare
    /// what this app asked to sign with what the wallet app shows.
    /// </summary>
    public partial class MwaSignMessagePopupViewModel : MwaSigningPopupViewModel
    {
        /// <summary>
        /// Long enough to cover any realistic payload. A huge one (a fat GraphQL document,
        /// say) is truncated rather than stalling the UI thread on layout.
        /// </summary>
        private const int MaxDisplayedMessageLength = 4000;

        /// <summary>The message being signed, decoded for display - never the raw bytes.</summary>
        [ObservableProperty]
        private string messageText = "";

        public override async Task<T> ShowWhileAsync<T>(
            byte[] message,
            string reason,
            Func<IProgress<MwaConnectStage>, MwaWalletReopener, CancellationToken, Task<T>> operation,
            CancellationToken token)
        {
            var text = FormatMessage(message);

            // Set ahead of the base's own main-thread block, so both land on the dispatcher
            // in order and the message is there before the card becomes visible.
            await MainThread.InvokeOnMainThreadAsync(() => MessageText = text);

            return await ShowWhileAsync(reason, operation, token);
        }

        /// <summary>
        /// Messages are almost always UTF-8 text (profile API payloads, notification links,
        /// dapp messages). Binary would render as garbage, so it is shown as base64 instead -
        /// the same fallback the wallet apps themselves use.
        /// </summary>
        internal static string FormatMessage(byte[] message)
        {
            string text;

            try
            {
                text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(message);
            }
            catch (DecoderFallbackException)
            {
                return Convert.ToBase64String(message);
            }

            // Control characters beyond ordinary whitespace mean the payload is binary.
            if (text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            {
                return Convert.ToBase64String(message);
            }

            return text.Length <= MaxDisplayedMessageLength
                ? text
                : text[..MaxDisplayedMessageLength] + "\n..";
        }

        public override void SetToDefault()
        {
            base.SetToDefault();
            MessageText = "";
        }
    }
}
