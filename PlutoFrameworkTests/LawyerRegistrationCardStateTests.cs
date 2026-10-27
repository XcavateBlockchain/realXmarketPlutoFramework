using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests
{
    internal class LawyerRegistrationCardStateTests
    {
        [Test]
        public void Create_Unregistered_OffersRegionSelection()
        {
            var state = LawyerRegistrationCardState.Create(null, null, statusLoaded: true, loadFailed: false, busy: false);

            Assert.Multiple(() =>
            {
                Assert.That(state.Kind, Is.EqualTo(LawyerRegistrationCardKind.Unregistered));
                Assert.That(state.Text, Is.EqualTo("Select the region you work in"));
                Assert.That(state.CanRegister, Is.True);
            });
        }

        [Test]
        public void Create_BusyUnregistered_BlocksDuplicateRegistration()
        {
            var state = LawyerRegistrationCardState.Create(null, null, statusLoaded: true, loadFailed: false, busy: true);

            Assert.That(state.CanRegister, Is.False);
        }

        [Test]
        public void Create_InitialUnknown_DoesNotOfferRegistration()
        {
            var state = LawyerRegistrationCardState.Create(null, null, statusLoaded: false, loadFailed: false, busy: false);

            Assert.Multiple(() =>
            {
                Assert.That(state.Kind, Is.EqualTo(LawyerRegistrationCardKind.Unavailable));
                Assert.That(state.Text, Is.EqualTo("Checking registration status…"));
                Assert.That(state.CanRegister, Is.False);
            });
        }

        [Test]
        public void Create_Registered_ShowsTheSingleRegionAndBlocksRegistration()
        {
            var state = LawyerRegistrationCardState.Create(7, null, statusLoaded: true, loadFailed: false, busy: false);

            Assert.Multiple(() =>
            {
                Assert.That(state.Kind, Is.EqualTo(LawyerRegistrationCardKind.Registered));
                Assert.That(state.Text, Is.EqualTo("Registered in Region 7"));
                Assert.That(state.CanRegister, Is.False);
            });
        }

        [Test]
        public void Create_Pending_ShowsSyncingEvenWhileTheIndexerIsUnavailable()
        {
            var state = LawyerRegistrationCardState.Create(null, 3, statusLoaded: true, loadFailed: true, busy: false);

            Assert.Multiple(() =>
            {
                Assert.That(state.Kind, Is.EqualTo(LawyerRegistrationCardKind.Syncing));
                Assert.That(state.Text, Is.EqualTo("Registration confirmed · Syncing…"));
                Assert.That(state.CanRegister, Is.False);
            });
        }

        [Test]
        public void Create_UnknownAfterLoadFailure_OffersRetryInsteadOfRegistration()
        {
            var state = LawyerRegistrationCardState.Create(null, null, statusLoaded: true, loadFailed: true, busy: false);

            Assert.Multiple(() =>
            {
                Assert.That(state.Kind, Is.EqualTo(LawyerRegistrationCardKind.Unavailable));
                Assert.That(state.Text, Is.EqualTo("Registration status unavailable. Pull down to try again."));
                Assert.That(state.CanRegister, Is.False);
            });
        }
    }
}
