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
        public void Create_Registered_ShowsTheSingleRegionAndOffersUnregistration()
        {
            var state = LawyerRegistrationCardState.Create(7, null, statusLoaded: true, loadFailed: false, busy: false);

            Assert.Multiple(() =>
            {
                Assert.That(state.Kind, Is.EqualTo(LawyerRegistrationCardKind.Registered));
                Assert.That(state.Text, Is.EqualTo("Registered in Region 7"));
                Assert.That(state.CanRegister, Is.False);
                Assert.That(state.CanUnregister, Is.True);
            });
        }

        [Test]
        public void Create_BusyRegistered_BlocksUnregistration()
        {
            var state = LawyerRegistrationCardState.Create(7, null, statusLoaded: true, loadFailed: false, busy: true);

            Assert.Multiple(() =>
            {
                Assert.That(state.CanRegister, Is.False);
                Assert.That(state.CanUnregister, Is.False);
            });
        }

        [Test]
        public void Create_ConfirmedUnregistration_ShowsSyncingRegardlessOfIndexerState()
        {
            var registered = LawyerRegistrationCardState.Create(
                7, null, statusLoaded: true, loadFailed: true, busy: false, unregistrationConfirmed: true);
            var removed = LawyerRegistrationCardState.Create(
                null, null, statusLoaded: true, loadFailed: true, busy: false, unregistrationConfirmed: true);

            Assert.Multiple(() =>
            {
                Assert.That(registered.Kind, Is.EqualTo(LawyerRegistrationCardKind.Syncing));
                Assert.That(registered.Text, Is.EqualTo("Unregistration confirmed · Syncing…"));
                Assert.That(registered.CanRegister, Is.False);
                Assert.That(registered.CanUnregister, Is.False);
                Assert.That(removed.Kind, Is.EqualTo(LawyerRegistrationCardKind.Syncing));
                Assert.That(removed.Text, Is.EqualTo("Unregistration confirmed · Syncing…"));
                Assert.That(removed.CanRegister, Is.False);
                Assert.That(removed.CanUnregister, Is.False);
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
