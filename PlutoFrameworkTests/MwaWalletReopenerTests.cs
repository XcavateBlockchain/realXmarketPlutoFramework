using PlutoFrameworkCore.Solana.Mwa;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The Open Wallet button on the waiting popups re-fires the association intent
    /// through <see cref="MwaWalletReopener"/>. Before the connect flow arms it (and on
    /// platforms without a launcher) the button has nothing to fire, so reopening must
    /// report false rather than throw - the popup turns that into a status line.
    /// </summary>
    public class MwaWalletReopenerTests
    {
        [Test]
        public async Task UnarmedReopenIsAHarmlessFalse()
        {
            Assert.That(await new MwaWalletReopener().ReopenAsync(), Is.False);
        }

        [Test]
        public async Task ArmedReopenInvokesTheLaunchDelegate()
        {
            var reopener = new MwaWalletReopener();

            var launches = 0;

            reopener.Arm(() =>
            {
                launches++;
                return Task.FromResult(true);
            });

            // Every tap re-fires: the user may dismiss the wallet app more than once.
            Assert.That(await reopener.ReopenAsync(), Is.True);
            Assert.That(await reopener.ReopenAsync(), Is.True);
            Assert.That(launches, Is.EqualTo(2));
        }

        [Test]
        public async Task AFailedRelaunchReportsFalse()
        {
            var reopener = new MwaWalletReopener();

            // Same meaning as the first launch: nothing installed handles the URI.
            reopener.Arm(() => Task.FromResult(false));

            Assert.That(await reopener.ReopenAsync(), Is.False);
        }
    }
}
