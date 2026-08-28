namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// A wire-format transaction split into its parts: one slot per required signer, and the
    /// message those signatures are over.
    /// </summary>
    /// <remarks>
    /// A class rather than a record because the members are byte arrays, for which record
    /// value equality would compare references and quietly mislead.
    /// </remarks>
    public sealed class ParsedSolanaTransaction
    {
        /// <summary>
        /// One 64-byte slot per required signer, in the order the message declares them.
        /// A slot that nobody has signed yet is all zeroes.
        /// </summary>
        public required byte[][] Signatures { get; init; }

        /// <summary>The compiled message, legacy or versioned. Treated as opaque.</summary>
        public required byte[] Message { get; init; }
    }

    /// <summary>
    /// Byte-level Solana transaction wire format, for the parts Solnet does not expose.
    ///
    /// Mobile Wallet Adapter's sign_and_send_transactions wants a fully-formed wire-format
    /// transaction with an empty signature slot per required signer, which the wallet then
    /// fills in. Solnet's <c>TransactionBuilder.Serialize()</c> emits a zero-length signature
    /// vector when nothing has signed, which is a different and unsubmittable thing, so the
    /// payload is framed here instead.
    ///
    /// The same byte-level view is what lets an injected dapp's transaction be signed at all:
    /// a signature is over the message bytes whatever the transaction version, so the message
    /// never has to be understood — only located. Solnet's deserializer handles legacy
    /// transactions only, and would rule out v0 for no reason.
    /// </summary>
    public static class SolanaTransactionFramer
    {
        /// <summary>Length of an Ed25519 signature.</summary>
        private const int SIGNATURE_LENGTH = 64;

        /// <summary>Length of an Ed25519 public key.</summary>
        private const int PUBLIC_KEY_LENGTH = 32;

        /// <summary>
        /// numRequiredSignatures, numReadonlySignedAccounts, numReadonlyUnsignedAccounts.
        /// </summary>
        private const int MESSAGE_HEADER_LENGTH = 3;

        /// <summary>
        /// A message whose first byte has this bit set is versioned, and that byte is the
        /// version marker rather than the signer count.
        /// </summary>
        private const int MESSAGE_VERSION_PREFIX_BIT = 0x80;

        /// <summary>Seven length bits per byte in the short-vector encoding.</summary>
        private const int SHORT_VECTOR_BITS_PER_BYTE = 7;

        private const int SHORT_VECTOR_VALUE_MASK = 0x7F;

        private const int SHORT_VECTOR_CONTINUATION_BIT = 0x80;

        /// <summary>
        /// Highest shift a length may reach before it would overflow an int, which bounds how
        /// far a malformed continuation run can drag the decoder.
        /// </summary>
        private const int SHORT_VECTOR_MAX_SHIFT = 28;

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
        /// Splits a serialized transaction into its signature slots and its message:
        /// <c>shortvec(numSignatures) || numSignatures x 64 || message</c>.
        /// </summary>
        /// <remarks>
        /// The transaction a dapp hands over may already carry signatures from other signers,
        /// so the slots come back individually rather than as one block to be overwritten.
        /// </remarks>
        public static ParsedSolanaTransaction Parse(byte[] wireTransaction)
        {
            var signatureCount = DecodeShortVectorLength(wireTransaction, 0, out var prefixLength);

            var available = wireTransaction.Length - prefixLength;

            // Bounds the count against what is actually there before multiplying it out,
            // which also rules out the overflow a malformed length could otherwise cause.
            if (signatureCount > available / SIGNATURE_LENGTH)
            {
                throw new FormatException(
                    $"Transaction declares {signatureCount} signatures but only {available} bytes follow the length prefix");
            }

            var messageStart = prefixLength + (signatureCount * SIGNATURE_LENGTH);

            if (wireTransaction.Length <= messageStart)
            {
                throw new FormatException("Transaction contains no message after its signature slots");
            }

            var signatures = new byte[signatureCount][];

            for (var i = 0; i < signatureCount; i++)
            {
                var start = prefixLength + (i * SIGNATURE_LENGTH);

                signatures[i] = wireTransaction[start..(start + SIGNATURE_LENGTH)];
            }

            return new ParsedSolanaTransaction
            {
                Signatures = signatures,
                Message = wireTransaction[messageStart..],
            };
        }

        /// <summary>
        /// The number of signature slots a compiled message requires: its header's
        /// numRequiredSignatures, the exact count a wire-format transaction built from it
        /// must carry.
        /// </summary>
        /// <remarks>
        /// A node rejects a transaction whose signature slots disagree with the header
        /// count as a malformed transaction, with a "failed to sanitize accounts offsets"
        /// error that has nothing to do with the message's actual account offsets, so the
        /// slots must be sized from this value rather than assumed.
        /// </remarks>
        public static int GetRequiredSignatures(byte[] compiledMessage)
        {
            if (compiledMessage.Length == 0)
            {
                throw new ArgumentException("Compiled message is empty", nameof(compiledMessage));
            }

            // A versioned message opens with a marker byte, a legacy one straight into the
            // header. Read as a signer count, a v0 marker would say 128 and misplace
            // every offset after it.
            var offset = (compiledMessage[0] & MESSAGE_VERSION_PREFIX_BIT) != 0 ? 1 : 0;

            if (compiledMessage.Length < offset + MESSAGE_HEADER_LENGTH)
            {
                throw new FormatException("Message is too short to contain a header");
            }

            return compiledMessage[offset];
        }

        /// <summary>
        /// The index of <paramref name="publicKey"/> among a message's required signers, which
        /// is the signature slot it must sign into.
        /// </summary>
        /// <remarks>
        /// A message's account keys open with its signers, fee payer first, so the index into
        /// the account-key array is also the index into the signature slots. Signing into the
        /// wrong slot yields a transaction that looks well-formed and is rejected on
        /// submission, so a key that is not a required signer is an error rather than a guess.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The key is absent from the message, or present but not a required signer.
        /// </exception>
        public static int FindSignerIndex(byte[] message, byte[] publicKey)
        {
            if (publicKey.Length != PUBLIC_KEY_LENGTH)
            {
                throw new ArgumentException(
                    $"A Solana public key is {PUBLIC_KEY_LENGTH} bytes, not {publicKey.Length}", nameof(publicKey));
            }

            var requiredSignatures = GetRequiredSignatures(message);

            var offset = (message[0] & MESSAGE_VERSION_PREFIX_BIT) != 0 ? 1 : 0;

            offset += MESSAGE_HEADER_LENGTH;

            var keyCount = DecodeShortVectorLength(message, offset, out var prefixLength);

            offset += prefixLength;

            if (keyCount > (message.Length - offset) / PUBLIC_KEY_LENGTH)
            {
                throw new FormatException(
                    $"Message declares {keyCount} account keys but only {message.Length - offset} bytes follow");
            }

            for (var i = 0; i < keyCount; i++)
            {
                var start = offset + (i * PUBLIC_KEY_LENGTH);

                if (!message.AsSpan(start, PUBLIC_KEY_LENGTH).SequenceEqual(publicKey))
                {
                    continue;
                }

                if (i >= requiredSignatures)
                {
                    throw new InvalidOperationException(
                        $"The account is in the transaction at index {i} but only its first {requiredSignatures} accounts are signers");
                }

                return i;
            }

            throw new InvalidOperationException("The account does not appear in the transaction's account keys");
        }

        /// <summary>
        /// Writes a signature into one slot and reassembles the transaction, leaving every
        /// other slot as it arrived.
        /// </summary>
        /// <remarks>
        /// Signatures already present are preserved: a dapp may have applied additional
        /// signers before handing the transaction over, and dropping those would produce a
        /// transaction that fails submission for an unrelated-looking reason.
        /// </remarks>
        public static byte[] ApplySignature(ParsedSolanaTransaction transaction, int signerIndex, byte[] signature)
        {
            if (signature.Length != SIGNATURE_LENGTH)
            {
                throw new ArgumentException(
                    $"An Ed25519 signature is {SIGNATURE_LENGTH} bytes, not {signature.Length}", nameof(signature));
            }

            ArgumentOutOfRangeException.ThrowIfNegative(signerIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(signerIndex, transaction.Signatures.Length);

            var lengthPrefix = EncodeShortVectorLength(transaction.Signatures.Length);

            var signatureBlockLength = transaction.Signatures.Length * SIGNATURE_LENGTH;

            var framed = new byte[lengthPrefix.Length + signatureBlockLength + transaction.Message.Length];

            lengthPrefix.CopyTo(framed, 0);

            for (var i = 0; i < transaction.Signatures.Length; i++)
            {
                var slot = i == signerIndex ? signature : transaction.Signatures[i];

                slot.CopyTo(framed, lengthPrefix.Length + (i * SIGNATURE_LENGTH));
            }

            transaction.Message.CopyTo(framed, lengthPrefix.Length + signatureBlockLength);

            return framed;
        }

        /// <summary>
        /// Reads a short-vector length prefix, reporting how many bytes it occupied so the
        /// caller can continue from the right place.
        /// </summary>
        private static int DecodeShortVectorLength(byte[] data, int offset, out int bytesRead)
        {
            var value = 0;
            var shift = 0;

            bytesRead = 0;

            while (true)
            {
                if (offset + bytesRead >= data.Length)
                {
                    throw new FormatException("Short-vector length runs past the end of the data");
                }

                var current = data[offset + bytesRead];

                bytesRead++;

                value |= (current & SHORT_VECTOR_VALUE_MASK) << shift;

                if ((current & SHORT_VECTOR_CONTINUATION_BIT) == 0)
                {
                    return value;
                }

                shift += SHORT_VECTOR_BITS_PER_BYTE;

                if (shift > SHORT_VECTOR_MAX_SHIFT)
                {
                    throw new FormatException("Short-vector length is too large to be valid");
                }
            }
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
