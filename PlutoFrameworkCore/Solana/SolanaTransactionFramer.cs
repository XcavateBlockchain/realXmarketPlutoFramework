namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Byte-level Solana transaction wire format, for the parts Solnet does not expose.
    ///
    /// Mobile Wallet Adapter's sign_and_send_transactions wants a fully-formed wire-format
    /// transaction with an empty signature slot per required signer, which the wallet then
    /// fills in. Solnet's <c>TransactionBuilder.Serialize()</c> emits a zero-length signature
    /// vector when nothing has signed, which is a different and unsubmittable thing, so the
    /// payload is framed here instead.
    /// </summary>
    public static class SolanaTransactionFramer
    {
        /// <summary>Length of an Ed25519 signature.</summary>
        private const int SIGNATURE_LENGTH = 64;

        /// <summary>Seven length bits per byte in the short-vector encoding.</summary>
        private const int SHORT_VECTOR_BITS_PER_BYTE = 7;

        private const int SHORT_VECTOR_VALUE_MASK = 0x7F;

        private const int SHORT_VECTOR_CONTINUATION_BIT = 0x80;

        /// <summary>
        /// Solana's short-vector length prefix: a base-128 varint carrying seven bits of
        /// length per byte, with the high bit set while further bytes follow.
        /// </summary>
        /// <remarks>
        /// Reimplemented because Solnet declares its ShortVectorEncoding internal.
        /// </remarks>
        public static byte[] EncodeShortVectorLength(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            var encoded = new List<byte>();

            var remaining = length;

            while (true)
            {
                var chunk = remaining & SHORT_VECTOR_VALUE_MASK;

                remaining >>= SHORT_VECTOR_BITS_PER_BYTE;

                if (remaining == 0)
                {
                    encoded.Add((byte)chunk);

                    break;
                }

                encoded.Add((byte)(chunk | SHORT_VECTOR_CONTINUATION_BIT));
            }

            return [.. encoded];
        }

        /// <summary>
        /// Frames a compiled transaction message as an unsigned wire-format transaction:
        /// <c>shortvec(requiredSignatures) || requiredSignatures x 64 zero bytes || message</c>.
        /// </summary>
        public static byte[] FrameUnsigned(byte[] compiledMessage, int requiredSignatures)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(requiredSignatures, 1);

            if (compiledMessage.Length == 0)
            {
                throw new ArgumentException("Compiled message is empty", nameof(compiledMessage));
            }

            var lengthPrefix = EncodeShortVectorLength(requiredSignatures);

            var framed = new byte[lengthPrefix.Length + (requiredSignatures * SIGNATURE_LENGTH) + compiledMessage.Length];

            lengthPrefix.CopyTo(framed, 0);

            // The signature slots stay zeroed for the wallet to fill in.
            compiledMessage.CopyTo(framed, lengthPrefix.Length + (requiredSignatures * SIGNATURE_LENGTH));

            return framed;
        }

        /// <summary>
        /// Pulls the signature out of a Mobile Wallet Adapter signed payload, which is the
        /// original message with its signature appended.
        /// </summary>
        public static byte[] ExtractSignature(byte[] signedPayload)
        {
            if (signedPayload.Length < SIGNATURE_LENGTH)
            {
                throw new FormatException(
                    $"Signed payload is {signedPayload.Length} bytes, too short to contain a {SIGNATURE_LENGTH}-byte signature");
            }

            return signedPayload[^SIGNATURE_LENGTH..];
        }
    }
}
