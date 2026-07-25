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
