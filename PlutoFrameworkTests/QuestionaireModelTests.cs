using PlutoFramework.Model.Xcavate;

namespace PlutoFrameworkTests
{
    public class QuestionnaireModelTests
    {
        [Test]
        public async Task GetQuestionsAsync_ReturnsSections()
        {
            var sections = await QuestionnaireModel.GetXcavateQuestionsAsync();

            Assert.That(sections.Count, Is.GreaterThan(0));
            Assert.That(sections.Any(section => section.Id == "high_net_worth_investor"), Is.True);
            Assert.That(sections.Any(section => section.Id == "sophisticated-investor"), Is.True);
        }

        [Test]
        public async Task EvaluateAnswersAsync_ReturnsAssessment()
        {
            var responses = new Dictionary<string, Dictionary<string, object?>>
            {
                ["high_net_worth_investor"] = new Dictionary<string, object?>
                {
                    ["annual_income"] = "Yes",
                    ["yes_to_annual_income"] = "Over £100,000",
                    ["net_assets"] = "No",
                    ["none_of_these_apply_to_me"] = "No",
                    ["high_net_worth_risk_acknowledgement"] = true,
                },
                ["sophisticated-investor"] = new Dictionary<string, object?>
                {
                    ["worked_in_private_equity"] = "No",
                    ["been_director_of_company"] = "No",
                    ["made_investments_in_unlisted_companies"] = "No",
                    ["been_member_of_network_or_syndicate"] = "No",
                    ["none_of_these_apply_to_me"] = "No",
                    ["risk_declaration"] = true,
                }
            };

            var assessment = await QuestionnaireModel.EvaluateAnswersAsync(responses);

            Assert.That(assessment.Successful, Is.True);
            Assert.That(assessment.Message, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AcceptTermsAsync_Completes()
        {
            var address = "5EU6EyEq6RhqYed1gCYyQRVttdy6FC9yAtUUGzPe3gfpFX8o";
            var response = await QuestionnaireModel.AcceptTermsAsync(address);

            Assert.That(response, Is.Not.Null.And.Not.Empty);
        }
    }
}
