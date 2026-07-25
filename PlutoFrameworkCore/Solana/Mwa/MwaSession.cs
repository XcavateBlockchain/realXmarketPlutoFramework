using System.Net.WebSockets;

namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// An established, encrypted Mobile Wallet Adapter transport.
    ///
    /// The wallet runs the WebSocket server and we connect to it as a client, so the
    /// connection cannot be attempted until after the association intent has been fired.
    /// </summary>
    public sealed class MwaSession : IAsyncDisposable
    {
        /// <summary>
        /// Binary subprotocol. The base64 variant exists for transports that cannot carry
        /// binary frames, which does not apply here.
        /// </summary>
        private const string SUBPROTOCOL = "com.solana.mobilewalletadapter.v1";

        /// <summary>
        /// The specification has the wallet listening for at least 10 seconds and the dapp
        /// retrying for at least 30 before giving up and telling the user.
        /// </summary>
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

        private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromMilliseconds(250);

        private const int MAXIMUM_FRAME_LENGTH = 4 * 1024 * 1024;

        private readonly ClientWebSocket socket;
        private readonly MwaSessionCipher cipher;

        private MwaSession(ClientWebSocket socket, MwaSessionCipher cipher)
        {
            this.socket = socket;
            this.cipher = cipher;
        }

        /// <summary>
        /// HELLO_REQ is <c>&lt;Qd&gt;&lt;Sa&gt;</c>: our ephemeral ECDH keypoint, followed by an
        /// ECDSA-SHA256 signature over it made with the association key. 129 bytes.
        /// </summary>
        public static byte[] BuildHelloRequest(MwaAssociationKeypair association, MwaEphemeralKeypair ephemeral)
        {
            var keyPoint = ephemeral.PublicKeyPoint;
            var signature = association.SignPayload(keyPoint);

            var helloRequest = new byte[keyPoint.Length + signature.Length];

            keyPoint.CopyTo(helloRequest, 0);
            signature.CopyTo(helloRequest, keyPoint.Length);

            return helloRequest;
        }

        /// <summary>
        /// Connects to the wallet's local WebSocket server, retrying until it binds, then
        /// performs the HELLO exchange and returns a session ready for JSON-RPC.
        /// </summary>
        public static async Task<MwaSession> EstablishAsync(
            MwaAssociationKeypair association,
            int port,
            CancellationToken token)
        {
            var socket = await ConnectWithRetryAsync(port, token);

            try
            {
                using var ephemeral = MwaEphemeralKeypair.Generate();

                await SendRawAsync(socket, BuildHelloRequest(association, ephemeral), token);

                var helloResponse = await ReceiveRawAsync(socket, token);

                if (helloResponse.Length < MwaKeyPoint.ENCODED_LENGTH)
                {
                    throw new MwaProtocolException(
                        $"HELLO_RSP is {helloResponse.Length} bytes, too short to contain a keypoint");
                }

                var walletKeyPoint = helloResponse[..MwaKeyPoint.ENCODED_LENGTH];

                var cipher = MwaSessionCipher.Derive(ephemeral, walletKeyPoint, association.PublicKeyPoint);

                // Mobile Wallet Adapter 2.0 appends an encrypted session-properties message.
                // 1.0 wallets send the keypoint alone, so an absent remainder is not an error.
                var sessionProperties = helloResponse[MwaKeyPoint.ENCODED_LENGTH..];

                if (sessionProperties.Length > 0)
                {
                    // Consuming this advances the inbound sequence to 1, which every
                    // subsequent frame is validated against.
                    cipher.Decrypt(sessionProperties);
                }

                return new MwaSession(socket, cipher);
            }
            catch
            {
                socket.Dispose();

                throw;
            }
        }

        private static async Task<ClientWebSocket> ConnectWithRetryAsync(int port, CancellationToken token)
        {
            var endpoint = MwaAssociationUri.BuildLocalWebSocket(port);

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(ConnectTimeout);

            Exception? lastFailure = null;

            while (!deadline.IsCancellationRequested)
            {
                var socket = new ClientWebSocket();
                socket.Options.AddSubProtocol(SUBPROTOCOL);

                try
                {
                    await socket.ConnectAsync(endpoint, deadline.Token);

                    return socket;
                }
                catch (Exception ex)
                {
                    socket.Dispose();

                    lastFailure = ex;

                    // The wallet may not have bound the port yet, so keep trying until the
                    // deadline rather than failing on the first refusal.
                    try
                    {
                        await Task.Delay(ConnectRetryDelay, deadline.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            token.ThrowIfCancellationRequested();

            throw new MwaProtocolException(
                $"No wallet accepted a connection on port {port} within {ConnectTimeout.TotalSeconds:0} seconds",
                lastFailure ?? new TimeoutException());
        }

        public async Task SendAsync(byte[] payload, CancellationToken token) =>
            await SendRawAsync(socket, cipher.Encrypt(payload), token);

        public async Task<byte[]> ReceiveAsync(CancellationToken token) =>
            cipher.Decrypt(await ReceiveRawAsync(socket, token));

        private static Task SendRawAsync(ClientWebSocket socket, byte[] payload, CancellationToken token) =>
            socket.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, token);

        /// <summary>
        /// Reads one complete WebSocket message, reassembling continuation frames.
        /// </summary>
        private static async Task<byte[]> ReceiveRawAsync(ClientWebSocket socket, CancellationToken token)
        {
            using var message = new MemoryStream();

            var buffer = new byte[8192];

            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new MwaProtocolException(
                        $"The wallet closed the session: {result.CloseStatus} {result.CloseStatusDescription}");
                }

                message.Write(buffer, 0, result.Count);

                if (message.Length > MAXIMUM_FRAME_LENGTH)
                {
                    throw new MwaProtocolException($"Frame exceeded {MAXIMUM_FRAME_LENGTH} bytes");
                }

                if (result.EndOfMessage)
                {
                    return message.ToArray();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, closeTimeout.Token);
                }
            }
            catch
            {
                // The session is being torn down; a failure to close cleanly is not useful
                // to report and must not mask whatever prompted the teardown.
            }
            finally
            {
                cipher.Dispose();
                socket.Dispose();
            }
        }
    }
}
