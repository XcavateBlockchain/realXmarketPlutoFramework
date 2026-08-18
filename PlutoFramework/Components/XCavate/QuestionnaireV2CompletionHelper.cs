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
            var firstSection = flowState.GetSectionById(QuestionnaireV2FlowState.HighNetWorthSectionId)
                ?? throw new Exception($"Questionnaire section '{QuestionnaireV2FlowState.HighNetWorthSectionId}' is missing.");
            var secondSection = flowState.GetSectionById(QuestionnaireV2FlowState.SophisticatedInvestorSectionId);

            if (string.Equals(flowState.GetSection(currentSectionIndex).Id, firstSection.Id, StringComparison.Ordinal))
            {
                var answers = new QuestionnaireAnswers
                {
                    AccountAddress = address,
                    UserId = $"User_{address}",
                    Responses = new Dictionary<string, Dictionary<string, object?>>
                    {
                        [firstSection.Id] = flowState.Responses[firstSection.Id]
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

                if (assessment.RequiresSecondAssessment is not true)
                {
                    await NavigateToFailedPageAsync(assessment.Message);
                    return;
                }

                flowState.SubmissionId = submission.Id;

                if (secondSection is null)
                {
                    await NavigateToFailedPageAsync("Assessment could not be completed.");
                    return;
                }

                var secondSectionIndex = flowState.Info.Sections.FindIndex(section => string.Equals(section.Id, secondSection.Id, StringComparison.Ordinal));

                if (secondSectionIndex < 0)
                {
                    await NavigateToFailedPageAsync("Assessment could not be completed.");
                    return;
                }

                await Shell.Current.Navigation.PushAsync(new QuestionnaireV2QuestionsPage(flowState, secondSectionIndex));

                return;
            }

            if (secondSection is null || !string.Equals(flowState.GetSection(currentSectionIndex).Id, secondSection.Id, StringComparison.Ordinal))
            {
                await NavigateToFailedPageAsync("Assessment could not be completed.");
                return;
            }

            if (string.IsNullOrWhiteSpace(flowState.SubmissionId))
            {
                await NavigateToFailedPageAsync("Assessment state is invalid. Please restart the questionnaire.");
                return;
            }

            var updatedSubmission = await QuestionnaireModel.PostSecondAnswersAsync(
                flowState.SubmissionId,
                address,
                secondSection.Id,
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

        private static async Task NavigateToFailedPageAsync(params string[] reasons)
        {
            var failedSections = new List<QuestionnaireFailedSection>();
            foreach (string reason in reasons)
            {
                failedSections.Add(


                    new()
                    {
                        Title = "Investor eligibility assessment",
                        Reason = string.IsNullOrWhiteSpace(reason)
                            ? "This investment is not suitable for you. You cannot proceed."
                            : reason
                    }
                );
            }

            await Shell.Current.Navigation.PushAsync(new QuestionnaireFailedPage(failedSections));
        }
    }
}
