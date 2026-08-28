using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    public class ShortVectorEncodingTests
    {
        /// <summary>
        /// Solnet keeps its ShortVectorEncoding internal, so this encoder is ours and needs
        /// its own coverage. The format is a base-128 varint: seven length bits per byte,
        /// high bit set while further bytes follow.
        /// </summary>
        [Test]
        public void EncodesSingleByteLengths()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaTransactionFramer.EncodeShortVectorLength(0), Is.EqualTo(new byte[] { 0x00 }));
                Assert.That(SolanaTransactionFramer.EncodeShortVectorLength(1), Is.EqualTo(new byte[] { 0x01 }));
                Assert.That(SolanaTransactionFramer.EncodeShortVectorLength(127), Is.EqualTo(new byte[] { 0x7F }));
            });
        }

        [Test]
        public void EncodesTwoByteLengths()
        {
            Assert.Multiple(() =>
            {
                // 128 -> low seven bits 0, continuation set, then 1.
                Assert.That(SolanaTransactionFramer.EncodeShortVectorLength(128), Is.EqualTo(new byte[] { 0x80, 0x01 }));
                Assert.That(SolanaTransactionFramer.EncodeShortVectorLength(255), Is.EqualTo(new byte[] { 0xFF, 0x01 }));
            });
        }

        [Test]
        public void RejectsNegativeLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SolanaTransactionFramer.EncodeShortVectorLength(-1));
        }
    }

    public class FrameUnsignedTests
    {
        private const int SIGNATURE_LENGTH = 64;

        private static byte[] SampleMessage()
        {
            var message = new byte[100];

            for (int i = 0; i < message.Length; i++)
            {
                // Non-zero throughout, so a zeroed signature slot cannot be mistaken for it.
                message[i] = (byte)(i + 1);
            }

            return message;
        }

        [Test]
        public void LengthIsPrefixPlusSignatureSlotsPlusMessage()
        {
            var message = SampleMessage();

            var framed = SolanaTransactionFramer.FrameUnsigned(message, requiredSignatures: 1);

            Assert.That(framed, Has.Length.EqualTo(1 + SIGNATURE_LENGTH + message.Length));
        }

        [Test]
        public void StartsWithShortVectorSignatureCount()
        {
            var framed = SolanaTransactionFramer.FrameUnsigned(SampleMessage(), requiredSignatures: 1);

            Assert.That(framed[0], Is.EqualTo(0x01));
        }

        /// <summary>
        /// The wallet fills these in. Anything non-zero here would be read as a signature.
        /// </summary>
        [Test]
        public void SignatureSlotsAreZeroed()
        {
            var framed = SolanaTransactionFramer.FrameUnsigned(SampleMessage(), requiredSignatures: 2);

            Assert.That(framed[1..(1 + (SIGNATURE_LENGTH * 2))], Is.All.Zero);
        }

        [Test]
        public void MessageRegionIsByteIdenticalToInput()
        {
            var message = SampleMessage();

            var framed = SolanaTransactionFramer.FrameUnsigned(message, requiredSignatures: 1);

            Assert.That(framed[(1 + SIGNATURE_LENGTH)..], Is.EqualTo(message));
        }

        [Test]
        public void HonoursMultipleRequiredSignatures()
        {
            var message = SampleMessage();

            var framed = SolanaTransactionFramer.FrameUnsigned(message, requiredSignatures: 3);

            Assert.Multiple(() =>
            {
                Assert.That(framed[0], Is.EqualTo(0x03));
                Assert.That(framed, Has.Length.EqualTo(1 + (SIGNATURE_LENGTH * 3) + message.Length));
                Assert.That(framed[(1 + (SIGNATURE_LENGTH * 3))..], Is.EqualTo(message));
            });
        }

        [Test]
        public void RejectsZeroRequiredSignatures()
        {
            // A transaction with no signers cannot be submitted, and Solnet's own
            // Serialize() would happily emit exactly that.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SolanaTransactionFramer.FrameUnsigned(SampleMessage(), requiredSignatures: 0));
        }

        [Test]
        public void RejectsEmptyMessage()
        {
            Assert.Throws<ArgumentException>(
                () => SolanaTransactionFramer.FrameUnsigned([], requiredSignatures: 1));
        }
    }

    public class GetRequiredSignaturesTests
    {
        [Test]
        public void ReadsTheHeaderCountOfALegacyMessage()
        {
            var message = WireFormat.LegacyMessage(3, WireFormat.Key(0xAA), WireFormat.Key(0xBB), WireFormat.Key(0xCC));

            Assert.That(SolanaTransactionFramer.GetRequiredSignatures(message), Is.EqualTo(3));
        }

        /// <summary>
        /// A v0 message opens with the 0x80 marker, which read as a count would say 128
        /// signers and misplace every later offset.
        /// </summary>
        [Test]
        public void ReadsPastTheVersionMarkerOfAV0Message()
        {
            var message = WireFormat.VersionedMessage(2, WireFormat.Key(0xAA), WireFormat.Key(0xBB));

            Assert.That(SolanaTransactionFramer.GetRequiredSignatures(message), Is.EqualTo(2));
        }

        /// <summary>
        /// The regression behind the size change: framing a message with this count yields a
        /// wire transaction whose slot count the node accepts, where an assumed one slot
        /// for a two-signer message read back as a malformed transaction.
        /// </summary>
        [Test]
        public void SizedFramingRoundTripsThroughParse()
        {
            var message = WireFormat.LegacyMessage(2, WireFormat.Key(0xAA), WireFormat.Key(0xBB));

            var framed = SolanaTransactionFramer.FrameUnsigned(
                message, SolanaTransactionFramer.GetRequiredSignatures(message));

            var parsed = SolanaTransactionFramer.Parse(framed);

            Assert.Multiple(() =>
            {
                Assert.That(parsed.Signatures, Has.Length.EqualTo(2));
                Assert.That(parsed.Message, Is.EqualTo(message));
            });
        }

        [Test]
        public void RejectsAnEmptyMessage()
        {
            Assert.Throws<ArgumentException>(() => SolanaTransactionFramer.GetRequiredSignatures([]));
        }

        [Test]
        public void RejectsAMessageTooShortForAHeader()
        {
            Assert.Throws<FormatException>(() => SolanaTransactionFramer.GetRequiredSignatures([0x01, 0x00]));
        }
    }

    public class ExtractSignatureTests
    {
        private const int SIGNATURE_LENGTH = 64;

        /// <summary>
        /// Mobile Wallet Adapter's sign_messages returns each payload with its signature
        /// appended, so the signature is the trailing 64 bytes rather than a separate field.
        /// </summary>
        [Test]
        public void ReturnsTrailingSixtyFourBytes()
        {
            var message = new byte[10];
            var signature = new byte[SIGNATURE_LENGTH];
            Array.Fill(signature, (byte)0xAB);

            var signedPayload = message.Concat(signature).ToArray();

            Assert.That(SolanaTransactionFramer.ExtractSignature(signedPayload), Is.EqualTo(signature));
        }

        [Test]
        public void WorksWhenPayloadIsExactlyASignature()
        {
            var signature = new byte[SIGNATURE_LENGTH];
            Array.Fill(signature, (byte)0x07);

            Assert.That(SolanaTransactionFramer.ExtractSignature(signature), Is.EqualTo(signature));
        }

        [Test]
        public void DoesNotReturnTheLeadingBytes()
        {
            var message = new byte[SIGNATURE_LENGTH];
            Array.Fill(message, (byte)0x11);

            var signature = new byte[SIGNATURE_LENGTH];
            Array.Fill(signature, (byte)0x22);

            var extracted = SolanaTransactionFramer.ExtractSignature(message.Concat(signature).ToArray());

            Assert.That(extracted, Is.All.EqualTo((byte)0x22));
        }

        [Test]
        public void RejectsPayloadShorterThanASignature()
        {
            Assert.Throws<FormatException>(() => SolanaTransactionFramer.ExtractSignature(new byte[63]));
        }
    }

    /// <summary>
    /// Builders for the Solana wire formats the framer has to read. Kept deliberately
    /// literal rather than delegating to Solnet: a test that builds its input with the
    /// same code under test proves nothing.
    /// </summary>
    internal static class WireFormat
    {
        internal const int SIGNATURE_LENGTH = 64;

        internal static byte[] Key(byte fill)
        {
            var key = new byte[32];
            Array.Fill(key, fill);
            return key;
        }

        internal static byte[] Signature(byte fill)
        {
            var signature = new byte[SIGNATURE_LENGTH];
            Array.Fill(signature, fill);
            return signature;
        }

        /// <summary>
        /// header || shortvec(numKeys) || numKeys x 32 || blockhash || shortvec(0) instructions
        /// </summary>
        internal static byte[] LegacyMessage(int numRequiredSignatures, params byte[][] accountKeys)
        {
            var bytes = new List<byte>
            {
                (byte)numRequiredSignatures,
                0,
                0,
            };

            bytes.AddRange(ShortVector(accountKeys.Length));

            foreach (var key in accountKeys)
            {
                bytes.AddRange(key);
            }

            bytes.AddRange(new byte[32]);
            bytes.Add(0);

            return [.. bytes];
        }

        /// <summary>A v0 message is a legacy message behind a 0x80 version marker.</summary>
        internal static byte[] VersionedMessage(int numRequiredSignatures, params byte[][] accountKeys) =>
            [0x80, .. LegacyMessage(numRequiredSignatures, accountKeys)];

        internal static byte[] Transaction(byte[] message, params byte[][] signatures)
        {
            var bytes = new List<byte>(ShortVector(signatures.Length));

            foreach (var signature in signatures)
            {
                bytes.AddRange(signature);
            }

            bytes.AddRange(message);

            return [.. bytes];
        }

        /// <summary>Hand-rolled so the tests do not lean on the encoder they sit beside.</summary>
        private static byte[] ShortVector(int length)
        {
            var bytes = new List<byte>();

            while (length >= 0x80)
            {
                bytes.Add((byte)((length & 0x7F) | 0x80));
                length >>= 7;
            }

            bytes.Add((byte)length);

            return [.. bytes];
        }
    }

    public class ParseTests
    {
        [Test]
        public void RoundTripsASingleSignatureTransaction()
        {
            var message = WireFormat.LegacyMessage(1, WireFormat.Key(0xAA));
            var signature = WireFormat.Signature(0x11);

            var parsed = SolanaTransactionFramer.Parse(WireFormat.Transaction(message, signature));

            Assert.Multiple(() =>
            {
                Assert.That(parsed.Signatures, Has.Length.EqualTo(1));
                Assert.That(parsed.Signatures[0], Is.EqualTo(signature));
                Assert.That(parsed.Message, Is.EqualTo(message));
            });
        }

        /// <summary>
        /// The adapter may hand over a transaction other signers have already signed, so each
        /// slot has to come back separately and in order.
        /// </summary>
        [Test]
        public void KeepsEverySignatureSlotDistinctAndInOrder()
        {
            var message = WireFormat.LegacyMessage(3, WireFormat.Key(0xAA));

            var parsed = SolanaTransactionFramer.Parse(WireFormat.Transaction(
                message,
                WireFormat.Signature(0x11),
                WireFormat.Signature(0x22),
                WireFormat.Signature(0x33)));

            Assert.Multiple(() =>
            {
                Assert.That(parsed.Signatures, Has.Length.EqualTo(3));
                Assert.That(parsed.Signatures[0], Is.All.EqualTo((byte)0x11));
                Assert.That(parsed.Signatures[1], Is.All.EqualTo((byte)0x22));
                Assert.That(parsed.Signatures[2], Is.All.EqualTo((byte)0x33));
            });
        }

        /// <summary>
        /// A count above 127 needs two shortvec bytes, and reading only one would put the
        /// message boundary in the wrong place rather than failing outright.
        /// </summary>
        [Test]
        public void ReadsAMultiByteSignatureCount()
        {
            var message = WireFormat.LegacyMessage(1, WireFormat.Key(0xAA));
            var signatures = Enumerable.Range(0, 128).Select(_ => WireFormat.Signature(0x11)).ToArray();

            var parsed = SolanaTransactionFramer.Parse(WireFormat.Transaction(message, signatures));

            Assert.Multiple(() =>
            {
                Assert.That(parsed.Signatures, Has.Length.EqualTo(128));
                Assert.That(parsed.Message, Is.EqualTo(message));
            });
        }

        [Test]
        public void RejectsAPayloadTruncatedMidSignature()
        {
            var message = WireFormat.LegacyMessage(2, WireFormat.Key(0xAA));

            var complete = WireFormat.Transaction(message, WireFormat.Signature(0x11), WireFormat.Signature(0x22));

            Assert.Throws<FormatException>(() => SolanaTransactionFramer.Parse(complete[..40]));
        }

        [Test]
        public void RejectsAPayloadWhoseSignatureCountLeavesNoMessage()
        {
            // shortvec(1) plus exactly one signature slot and nothing after it.
            var noMessage = WireFormat.Transaction([], WireFormat.Signature(0x11));

            Assert.Throws<FormatException>(() => SolanaTransactionFramer.Parse(noMessage));
        }

        [Test]
        public void RejectsAnEmptyPayload()
        {
            Assert.Throws<FormatException>(() => SolanaTransactionFramer.Parse([]));
        }
    }

    public class FindSignerIndexTests
    {
        [Test]
        public void ReturnsZeroForTheFeePayer()
        {
            var ours = WireFormat.Key(0xAA);
            var message = WireFormat.LegacyMessage(1, ours, WireFormat.Key(0xBB));

            Assert.That(SolanaTransactionFramer.FindSignerIndex(message, ours), Is.EqualTo(0));
        }

        [Test]
        public void ReturnsTheIndexOfALaterSigner()
        {
            var ours = WireFormat.Key(0xBB);
            var message = WireFormat.LegacyMessage(2, WireFormat.Key(0xAA), ours, WireFormat.Key(0xCC));

            Assert.That(SolanaTransactionFramer.FindSignerIndex(message, ours), Is.EqualTo(1));
        }

        /// <summary>
        /// Signing into a read-only account's position would produce a transaction that looks
        /// well-formed and is rejected on submission.
        /// </summary>
        [Test]
        public void ThrowsWhenTheKeyIsPresentButNotASigner()
        {
            var ours = WireFormat.Key(0xCC);
            var message = WireFormat.LegacyMessage(1, WireFormat.Key(0xAA), WireFormat.Key(0xBB), ours);

            Assert.Throws<InvalidOperationException>(() => SolanaTransactionFramer.FindSignerIndex(message, ours));
        }

        [Test]
        public void ThrowsWhenTheKeyIsAbsent()
        {
            var message = WireFormat.LegacyMessage(1, WireFormat.Key(0xAA));

            Assert.Throws<InvalidOperationException>(
                () => SolanaTransactionFramer.FindSignerIndex(message, WireFormat.Key(0xFF)));
        }

        /// <summary>
        /// A v0 message opens with 0x80. Read as a signer count that is 128, which would put
        /// every later offset in the wrong place.
        /// </summary>
        [Test]
        public void ReadsPastAVersionedMessagePrefix()
        {
            var ours = WireFormat.Key(0xBB);
            var message = WireFormat.VersionedMessage(2, WireFormat.Key(0xAA), ours);

            Assert.That(SolanaTransactionFramer.FindSignerIndex(message, ours), Is.EqualTo(1));
        }

        [Test]
        public void FindsTheFeePayerInAVersionedMessage()
        {
            var ours = WireFormat.Key(0xAA);
            var message = WireFormat.VersionedMessage(1, ours, WireFormat.Key(0xBB));

            Assert.That(SolanaTransactionFramer.FindSignerIndex(message, ours), Is.EqualTo(0));
        }

        [Test]
        public void RejectsAPublicKeyOfTheWrongLength()
        {
            var message = WireFormat.LegacyMessage(1, WireFormat.Key(0xAA));

            Assert.Throws<ArgumentException>(() => SolanaTransactionFramer.FindSignerIndex(message, new byte[31]));
        }

        [Test]
        public void RejectsATruncatedMessage()
        {
            Assert.Throws<FormatException>(
                () => SolanaTransactionFramer.FindSignerIndex([1, 0, 0, 2], WireFormat.Key(0xAA)));
        }
    }

    public class ApplySignatureTests
    {
        [Test]
        public void WritesTheSignatureIntoTheRequestedSlot()
        {
            var message = WireFormat.LegacyMessage(2, WireFormat.Key(0xAA), WireFormat.Key(0xBB));
            var parsed = SolanaTransactionFramer.Parse(WireFormat.Transaction(
                message, new byte[64], new byte[64]));

            var applied = SolanaTransactionFramer.ApplySignature(parsed, 1, WireFormat.Signature(0x99));

            var reparsed = SolanaTransactionFramer.Parse(applied);

            Assert.That(reparsed.Signatures[1], Is.All.EqualTo((byte)0x99));
        }

        /// <summary>
        /// The adapter partial-signs before handing the transaction over. Overwriting the
        /// whole signature block would silently drop a co-signer.
        /// </summary>
        [Test]
        public void PreservesSignaturesAlreadyPresent()
        {
            var message = WireFormat.LegacyMessage(2, WireFormat.Key(0xAA), WireFormat.Key(0xBB));
            var parsed = SolanaTransactionFramer.Parse(WireFormat.Transaction(
                message, new byte[64], WireFormat.Signature(0x22)));

            var applied = SolanaTransactionFramer.ApplySignature(parsed, 0, WireFormat.Signature(0x99));

            var reparsed = SolanaTransactionFramer.Parse(applied);

            Assert.Multiple(() =>
            {
                Assert.That(reparsed.Signatures[0], Is.All.EqualTo((byte)0x99));
                Assert.That(reparsed.Signatures[1], Is.All.EqualTo((byte)0x22));
            });
        }

        [Test]
        public void LeavesTheMessageRegionByteIdentical()
        {
            var message = WireFormat.LegacyMessage(1, WireFormat.Key(0xAA));
            var parsed = SolanaTransactionFramer.Parse(WireFormat.Transaction(message, new byte[64]));

            var applied = SolanaTransactionFramer.ApplySignature(parsed, 0, WireFormat.Signature(0x99));

            Assert.That(SolanaTransactionFramer.Parse(applied).Message, Is.EqualTo(message));
        }

        [Test]
        public void RejectsASignatureOfTheWrongLength()
        {
            var message = WireFormat.LegacyMessage(1, WireFormat.Key(0xAA));
            var parsed = SolanaTransactionFramer.Parse(WireFormat.Transaction(message, new byte[64]));

            Assert.Throws<ArgumentException>(() => SolanaTransactionFramer.ApplySignature(parsed, 0, new byte[63]));
        }

        [Test]
        public void RejectsAnIndexOutsideTheSignatureSlots()
        {
            var message = WireFormat.LegacyMessage(1, WireFormat.Key(0xAA));
            var parsed = SolanaTransactionFramer.Parse(WireFormat.Transaction(message, new byte[64]));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => SolanaTransactionFramer.ApplySignature(parsed, 1, WireFormat.Signature(0x99)));
        }
    }

    public class SolanaClusterMappingTests
    {
        [Test]
        public void EveryClusterMapsToASolnetCluster()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SolanaCluster.Devnet.ToSolnetCluster(), Is.EqualTo(Solnet.Rpc.Cluster.DevNet));
                Assert.That(SolanaCluster.Testnet.ToSolnetCluster(), Is.EqualTo(Solnet.Rpc.Cluster.TestNet));
                Assert.That(SolanaCluster.Mainnet.ToSolnetCluster(), Is.EqualTo(Solnet.Rpc.Cluster.MainNet));
            });
        }

        [Test]
        public void MappingIsTotal()
        {
            // A new cluster must not silently fall through to whatever the default is.
            foreach (SolanaCluster cluster in Enum.GetValues<SolanaCluster>())
            {
                Assert.DoesNotThrow(() => cluster.ToSolnetCluster());
            }
        }
    }
}
