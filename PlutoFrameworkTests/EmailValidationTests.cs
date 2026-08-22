using PlutoFramework.Model;

namespace PlutoFrameworkTests
{
    /// <summary>
    /// The email rules the profile and company forms enforce, and the reasons they show under a
    /// field that fails them.
    /// </summary>
    public class EmailValidation
    {
        [Test]
        public void AnOrdinaryAddressIsAccepted()
        {
            Assert.That(FormModel.IsValidEmail("name@example.com"), Is.True);
            Assert.That(FormModel.DescribeEmailProblem("name@example.com"), Is.Null);
        }

        [Test]
        public void EveryReasonMatchesTheRuleTheButtonObeys()
        {
            string[] candidates =
            {
                "", "   ", "name", "name@", "@example.com", "name@example",
                "name@.com", "name@example.", "a@b@example.com", "na me@example.com",
                "name@example.com", "n@e.c",
            };

            Assert.Multiple(() =>
            {
                foreach (var candidate in candidates)
                {
                    var accepted = FormModel.DescribeEmailProblem(candidate) is null;

                    Assert.That(accepted, Is.EqualTo(FormModel.IsValidEmail(candidate)), $"'{candidate}'");
                }
            });
        }

        [Test]
        public void AnEmptyAddressAsksForOne()
        {
            Assert.That(FormModel.DescribeEmailProblem(""), Is.EqualTo("Enter your email address."));
            Assert.That(FormModel.DescribeEmailProblem(null), Is.EqualTo("Enter your email address."));
        }

        [Test]
        public void SpacesAreCalledOutSeparately()
        {
            Assert.That(
                FormModel.DescribeEmailProblem("na me@example.com"),
                Is.EqualTo("An email address cannot contain spaces."));
        }

        [Test]
        public void AMissingAtSignIsCalledOut()
        {
            Assert.That(
                FormModel.DescribeEmailProblem("name.example.com"),
                Is.EqualTo("An email address needs an @, like name@example.com."));
        }

        [Test]
        public void ASecondAtSignIsCalledOut()
        {
            Assert.That(
                FormModel.DescribeEmailProblem("a@b@example.com"),
                Is.EqualTo("An email address can only contain one @."));
        }

        [Test]
        public void AMissingLocalPartIsCalledOut()
        {
            Assert.That(
                FormModel.DescribeEmailProblem("@example.com"),
                Is.EqualTo("Add the part before the @, like name@example.com."));
        }

        [Test]
        public void AMissingDomainIsCalledOut()
        {
            Assert.That(
                FormModel.DescribeEmailProblem("name@"),
                Is.EqualTo("Add the domain after the @, like name@example.com."));
        }

        [Test]
        public void ADomainWithoutADotIsCalledOut()
        {
            Assert.That(
                FormModel.DescribeEmailProblem("name@example"),
                Is.EqualTo("The domain after the @ needs a dot, like example.com."));
        }

        [Test]
        public void ADomainStartingOrEndingWithADotIsCalledOut()
        {
            Assert.That(
                FormModel.DescribeEmailProblem("name@.com"),
                Is.EqualTo("The domain after the @ cannot start or end with a dot."));

            Assert.That(
                FormModel.DescribeEmailProblem("name@example."),
                Is.EqualTo("The domain after the @ cannot start or end with a dot."));
        }
    }
}
