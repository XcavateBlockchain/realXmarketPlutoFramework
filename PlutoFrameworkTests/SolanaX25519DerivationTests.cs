extern alias bc26;

using bc26::Org.BouncyCastle.Crypto.Parameters;
using PlutoFramework.Model;
using Substrate.NetApi.Model.Types;
using System.Security.Cryptography;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// A Solana account's X25519 encryption key is derived from its seed phrase, through the
    /// routine written for Polkadot phrases, so it survives a reinstall with the same backup.
    /// That routine runs the phrase through Substrate's keyring, which is only safe because
    /// both chains use plain BIP39 - the assumption these tests exist to hold in place.
    /// </summary>
    public class SolanaX25519DerivationTests
    {
        /// <summary>
        /// The derivation KeysModel.SaveEncryptionX25519KeyAsync(string) performs, reproduced
        /// here because it lives in the MAUI project and cannot be referenced from tests.
        /// </summary>
        private static byte[] DeriveX25519(string mnemonics)
        {
            var account = MnemonicsModel.GetAccountFromMnemonics(mnemonics, KeyType.Ed25519);

            var seed = account.PrivateKey.Take(32).ToArray();

            var hashed = SHA512.HashData(seed);

            var x25519 = hashed.Take(32).ToArray();

            x25519[0] &= 248;
            x25519[31] &= 127;
            x25519[31] |= 64;

            return x25519;
        }

        /// <summary>
        /// The load-bearing case. Solana generates twelve words through its own wordlist, and
        /// if Substrate's keyring rejected that phrase every Solana-only user would fail to
        /// register a profile - the exact failure this work set out to fix.
        /// </summary>
        [Test]
        public void DerivesFromASolanaGeneratedPhrase()
        {
            var mnemonics = SolanaMnemonicsModel.GenerateMnemonics();

            Assert.That(mnemonics.Split(' '), Has.Length.EqualTo(12));

            var privateKey = DeriveX25519(mnemonics);

            Assert.That(privateKey, Has.Length.EqualTo(32));

            // Must be a usable X25519 scalar, not merely 32 bytes of something.
            Assert.DoesNotThrow(() => new X25519PrivateKeyParameters(privateKey).GeneratePublicKey());
        }

        /// <summary>
        /// Deterministic, which is the whole reason for deriving rather than generating: the
        /// user restores their phrase and gets the same encryption key back.
        /// </summary>
        [Test]
        public void IsDeterministicForTheSamePhrase()
        {
            var mnemonics = SolanaMnemonicsModel.GenerateMnemonics();

            Assert.That(DeriveX25519(mnemonics), Is.EqualTo(DeriveX25519(mnemonics)));
        }

        [Test]
        public void DiffersBetweenPhrases()
        {
            var first = DeriveX25519(SolanaMnemonicsModel.GenerateMnemonics());
            var second = DeriveX25519(SolanaMnemonicsModel.GenerateMnemonics());

            Assert.That(first, Is.Not.EqualTo(second));
        }
    }
}
