using PlutoFramework.Model;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The password rules two setup screens enforce. Tested here rather than through the
    /// screens because nothing in the MAUI project is reachable from this test project.
    /// </summary>
    public class PasswordRules
    {
        private const string ValidPassword = "Passw0rd";

        [Test]
        public void ValidPasswordSatisfiesEveryRule()
        {
            Assert.That(PasswordRulesModel.IsValid(ValidPassword), Is.True);
        }

        [Test]
        public void PasswordShorterThanSixCharactersFailsLength()
        {
            Assert.That(PasswordRulesModel.HasAllowedLength("Pa0w"), Is.False);
            Assert.That(PasswordRulesModel.IsValid("Pa0w"), Is.False);
        }

        [Test]
        public void PasswordLongerThanTwentyCharactersFailsLength()
        {
            // 21 characters.
            var tooLong = "Passw0rdPassw0rdPassw";

            Assert.That(tooLong, Has.Length.EqualTo(21));
            Assert.That(PasswordRulesModel.HasAllowedLength(tooLong), Is.False);
            Assert.That(PasswordRulesModel.IsValid(tooLong), Is.False);
        }

        [Test]
        public void PasswordWithoutUppercaseIsRejected()
        {
            Assert.That(PasswordRulesModel.HasUppercase("passw0rd"), Is.False);
            Assert.That(PasswordRulesModel.IsValid("passw0rd"), Is.False);
        }

        [Test]
        public void PasswordWithoutLowercaseIsRejected()
        {
            Assert.That(PasswordRulesModel.HasLowercase("PASSW0RD"), Is.False);
            Assert.That(PasswordRulesModel.IsValid("PASSW0RD"), Is.False);
        }

        [Test]
        public void PasswordWithoutDigitIsRejected()
        {
            Assert.That(PasswordRulesModel.HasDigit("Password"), Is.False);
            Assert.That(PasswordRulesModel.IsValid("Password"), Is.False);
        }

        [Test]
        public void NullAndEmptyAreRejectedWithoutThrowing()
        {
            Assert.That(PasswordRulesModel.IsValid(null), Is.False);
            Assert.That(PasswordRulesModel.IsValid(""), Is.False);
        }

        [Test]
        public void BoundaryLengthsAreAccepted()
        {
            // Exactly 6 and exactly 20, both otherwise valid.
            Assert.That(PasswordRulesModel.HasAllowedLength("Pas0wd"), Is.True);
            Assert.That(PasswordRulesModel.HasAllowedLength("Passw0rdPassw0rdPass"), Is.True);
        }
    }
}
