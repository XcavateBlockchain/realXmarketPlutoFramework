using PlutoFramework.Components.Onboarding;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Xcavate
{
    public static class QuestionnaireV2CompletionHelper
    {
        public static async Task NavigateNextOrCompleteAsync(QuestionnaireV2FlowState flowState, int currentSectionIndex)
        {
            var address = KeysModel.GetPublicKey();

            if (currentSectionIndex == 0)
            {
                var firstSectionId = flowState.Info.Sections.First().Id;

                var answers = new QuestionnaireAnswers
                {
                    AccountAddress = address,
                    UserId = $"User_{address}",
                    Responses = new Dictionary<string, Dictionary<string, object?>>
                    {
                        [firstSectionId] = flowState.Responses[firstSectionId]
                    }
                };

                var submission = await QuestionnaireModel.PostAnswersAsync(answers);
                var assessment = submission.Assessment ?? throw new Exception("Assessment was not returned from phase 1 submission.");

                if (assessment.Passed)
                {
                    var onboardingAgreementCoordinator = new OnboardingAgreementCoordinator();
                    await onboardingAgreementCoordinator.StartAsync(flowState.Info.Navigation);
                    return;
                }

                if (assessment.RequiresSecondAssessment == true && flowState.Info.Sections.Count > 1)
                {
                    flowState.SubmissionId = submission.Id;
                    await Shell.Current.Navigation.PushAsync(new QuestionnaireV2QuestionsPage(flowState, 1));
                    return;
                }

                await NavigateToFailedPageAsync(assessment.Message);
                return;
            }

            if (flowState.Info.Sections.Count <= 1)
            {
                await NavigateToFailedPageAsync("Assessment could not be completed.");
                return;
            }

            var secondSection = flowState.Info.Sections[1];

            if (string.IsNullOrWhiteSpace(flowState.SubmissionId))
            {
                await NavigateToFailedPageAsync("Assessment state is invalid. Please restart the questionnaire.");
                return;
            }

            var updatedSubmission = await QuestionnaireModel.PostSecondAnswersAsync(
                flowState.SubmissionId,
                address,
                flowState.Responses[secondSection.Id]);

            var secondAssessment = updatedSubmission.Assessment ?? throw new Exception("Assessment was not returned from phase 2 submission.");

            if (secondAssessment.Passed)
            {
                var onboardingAgreementCoordinator = new OnboardingAgreementCoordinator();
                await onboardingAgreementCoordinator.StartAsync(flowState.Info.Navigation);
                return;
            }

            await NavigateToFailedPageAsync(secondAssessment.Message);
        }

        private static async Task NavigateToFailedPageAsync(string reason)
        {
            var failedSections = new List<QuestionnaireFailedSection>
            {
                new()
                {
                    Title = "Investor eligibility assessment",
                    Reason = string.IsNullOrWhiteSpace(reason)
                        ? "This investment is not suitable for you. You cannot proceed."
                        : reason
                }
            };

            await Shell.Current.Navigation.PushAsync(new QuestionnaireFailedPage(failedSections));
        }
    }
}
