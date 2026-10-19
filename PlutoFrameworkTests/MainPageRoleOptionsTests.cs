using PlutoFrameworkCore.Xcavate;

namespace PlutoFrameworkTests
{
    public class MainPageRoleOptionsTests
    {
        [Test]
        public void Selectable_ContainsAllFourRoles()
        {
            Assert.That(MainPageRoleOptions.Selectable, Is.EquivalentTo(new[]
            {
                MainPageRole.Developer,
                MainPageRole.Investor,
                MainPageRole.Lawyer,
                MainPageRole.LettingAgent,
            }));
        }

        [TestCase(MainPageRole.Developer)]
        [TestCase(MainPageRole.Investor)]
        [TestCase(MainPageRole.Lawyer)]
        [TestCase(MainPageRole.LettingAgent)]
        public void Resolve_WithoutOverride_UsesProfileRole(MainPageRole profileRole)
        {
            Assert.That(
                MainPageRoleOptions.Resolve(overrideRole: null, profileRole),
                Is.EqualTo(profileRole));
        }

        [Test]
        public void Resolve_ExplicitInvestorOverride_WinsOverLawyerProfile()
        {
            Assert.That(
                MainPageRoleOptions.Resolve(MainPageRole.Investor, MainPageRole.Lawyer),
                Is.EqualTo(MainPageRole.Investor));
        }

        [Test]
        public void Resolve_WithoutOverrideOrProfile_DefaultsToInvestor()
        {
            Assert.That(
                MainPageRoleOptions.Resolve(overrideRole: null, profileRole: null),
                Is.EqualTo(MainPageRole.Investor));
        }

        [TestCase(MainPageRole.Developer, "//DeveloperAccount")]
        [TestCase(MainPageRole.Investor, "//Account")]
        [TestCase(MainPageRole.Lawyer, "//LawyerAccount")]
        [TestCase(MainPageRole.LettingAgent, "//Account")]
        public void ResolveRoute_RoleLandingPage(MainPageRole role, string expectedRoute)
        {
            Assert.That(MainPageRoleOptions.ResolveRoute(role), Is.EqualTo(expectedRoute));
        }

        [TestCase(MainPageRole.Developer)]
        [TestCase(MainPageRole.Investor)]
        [TestCase(MainPageRole.Lawyer)]
        [TestCase(MainPageRole.LettingAgent)]
        public void TryParse_StoredRole_ReturnsRole(MainPageRole expectedRole)
        {
            Assert.That(MainPageRoleOptions.TryParse(expectedRole.ToString(), out var role), Is.True);
            Assert.That(role, Is.EqualTo(expectedRole));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("None")]
        [TestCase("999")]
        public void TryParse_InvalidStoredRole_ReturnsFalse(string? storedRole)
        {
            Assert.That(MainPageRoleOptions.TryParse(storedRole, out _), Is.False);
        }
    }
}
