using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests
{
    internal class SpvLawyerClaimEligibilityTests
    {
        public enum Rejection
        {
            LawyerMainPageNotSelected,
            LawyerRoleNotCompliant,
            LawyerNotRegistered,
            WrongRegion,
            PropertyRegionUnknown,
            ListingNotSoldOut,
            SpvNotCreated,
            SpvSeatTaken,
            DeveloperLawyerConflict,
            LegalDeadlinePassed,
            ElectionClosed,
            CandidateCapacityReached,
        }

        private static SpvLawyerClaimEligibility.State EligibleState() => new(
            LawyerMainPageSelected: true,
            HasCompliantLawyerRole: true,
            LawyerRegionId: 7,
            WalletAddress: "spv-candidate",
            ListingStatus: "SoldOut",
            SpvCreated: true,
            SpvLawyer: XcavateMarketplaceIndexerModel.VacantSeatPubkey,
            DeveloperLawyer: "developer-lawyer",
            PropertyRegionId: 7,
            LegalDeadline: 2_000,
            SpvElectionExpiry: 0,
            SpvElectionCandidateCount: 0);

        [Test]
        public void CanDisplay_AcceptsAStateSatisfyingEveryProgramPrecondition()
        {
            Assert.That(SpvLawyerClaimEligibility.CanDisplay(EligibleState(), nowUnixSeconds: 2_000), Is.True);
        }

        [TestCase(Rejection.LawyerMainPageNotSelected)]
        [TestCase(Rejection.LawyerRoleNotCompliant)]
        [TestCase(Rejection.LawyerNotRegistered)]
        [TestCase(Rejection.WrongRegion)]
        [TestCase(Rejection.PropertyRegionUnknown)]
        [TestCase(Rejection.ListingNotSoldOut)]
        [TestCase(Rejection.SpvNotCreated)]
        [TestCase(Rejection.SpvSeatTaken)]
        [TestCase(Rejection.DeveloperLawyerConflict)]
        [TestCase(Rejection.LegalDeadlinePassed)]
        [TestCase(Rejection.ElectionClosed)]
        [TestCase(Rejection.CandidateCapacityReached)]
        public void CanDisplay_RejectsAStateTheProgramWouldRefuse(Rejection rejection)
        {
            var state = EligibleState();
            state = rejection switch
            {
                Rejection.LawyerMainPageNotSelected => state with { LawyerMainPageSelected = false },
                Rejection.LawyerRoleNotCompliant => state with { HasCompliantLawyerRole = false },
                Rejection.LawyerNotRegistered => state with { LawyerRegionId = null },
                Rejection.WrongRegion => state with { LawyerRegionId = 8 },
                Rejection.PropertyRegionUnknown => state with { PropertyRegionId = null },
                Rejection.ListingNotSoldOut => state with { ListingStatus = "Listed" },
                Rejection.SpvNotCreated => state with { SpvCreated = false },
                Rejection.SpvSeatTaken => state with { SpvLawyer = "elected-lawyer" },
                Rejection.DeveloperLawyerConflict => state with { DeveloperLawyer = "spv-candidate" },
                Rejection.LegalDeadlinePassed => state with { LegalDeadline = 1_999 },
                Rejection.ElectionClosed => state with { SpvElectionExpiry = 2_000 },
                Rejection.CandidateCapacityReached => state with { SpvElectionCandidateCount = 5 },
                _ => throw new ArgumentOutOfRangeException(nameof(rejection)),
            };

            Assert.That(SpvLawyerClaimEligibility.CanDisplay(state, nowUnixSeconds: 2_000), Is.False);
        }

        [Test]
        public void CanDisplay_AcceptsAnElectionThatIsStillOpen()
        {
            var state = EligibleState() with { SpvElectionExpiry = 2_001, SpvElectionCandidateCount = 4 };

            Assert.That(SpvLawyerClaimEligibility.CanDisplay(state, nowUnixSeconds: 2_000), Is.True);
        }

        [Test]
        public void CanSubmit_RejectsAnExistingCandidacyWithoutHidingItsSubmittedState()
        {
            Assert.That(SpvLawyerClaimEligibility.CanDisplay(EligibleState(), nowUnixSeconds: 2_000), Is.True);
            Assert.That(SpvLawyerClaimEligibility.CanSubmit(EligibleState(), ownCandidacyStanding: true, nowUnixSeconds: 2_000), Is.False);
        }

        [TestCase(0UL, 0L, 1UL)]
        [TestCase(3UL, 0L, 4UL)]
        [TestCase(3UL, 2_001L, 3UL)]
        public void GetClaimRound_OpensTheNextRoundOnlyWhenNoRoundExists(
            ulong currentRound, long expiry, ulong expected)
        {
            Assert.That(SpvLawyerClaimEligibility.GetClaimRound(currentRound, expiry), Is.EqualTo(expected));
        }

        [Test]
        public void TryCreateCostsQuote_AcceptsZeroAndTheExactCollectedFeeCap()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SpvLawyerClaimEligibility.TryCreateCostsQuote(0m, 1_000_000_000, out var zero), Is.True);
                Assert.That(zero, Is.EqualTo(0UL));
                Assert.That(SpvLawyerClaimEligibility.TryCreateCostsQuote(12.5m, 12_500_000_000, out var exact), Is.True);
                Assert.That(exact, Is.EqualTo(12_500_000_000UL));
            });
        }

        [Test]
        public void TryCreateCostsQuote_RejectsAnythingAboveTheCollectedFeeCap()
        {
            Assert.That(SpvLawyerClaimEligibility.TryCreateCostsQuote(1.000000001m, 1_000_000_000, out var quote), Is.False);
            Assert.That(quote, Is.EqualTo(0UL));
        }
    }
}
