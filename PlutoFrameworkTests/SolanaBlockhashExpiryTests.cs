using PlutoFrameworkCore.Solana;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The marketplace submit pipeline retries only when the blockhash expired between
    /// building and signing - any other failure must surface without a second wallet
    /// prompt, so the predicate's wording is pinned here.
    /// </summary>
    internal class SolanaBlockhashExpiryTests
    {
        [Test]
        public void IsExpiredError_TrueForTheWalletsAndTheNodesExpiryWordings()
        {
            // Solflare's user-facing message when its simulation finds the blockhash gone.
            Assert.That(SolanaBlockhashExpiry.IsExpiredError(
                "Blockhash expired because too much time passed between transaction creation and signing. Please try signing the transaction again"), Is.True);

            // The RPC node's wording on submission.
            Assert.That(SolanaBlockhashExpiry.IsExpiredError("Blockhash not found"), Is.True);
        }

        [Test]
        public void IsExpiredError_FalseForUnrelatedFailures()
        {
            Assert.That(SolanaBlockhashExpiry.IsExpiredError("User declined the request"), Is.False);
            Assert.That(SolanaBlockhashExpiry.IsExpiredError("insufficient funds"), Is.False);
            Assert.That(SolanaBlockhashExpiry.IsExpiredError(null), Is.False);
            Assert.That(SolanaBlockhashExpiry.IsExpiredError(""), Is.False);
        }
    }
}
