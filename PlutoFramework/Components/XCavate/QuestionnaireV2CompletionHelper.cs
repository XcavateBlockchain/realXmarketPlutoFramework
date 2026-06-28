using PlutoFramework.Components.Onboarding;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Xcavate
{
    public static class QuestionnaireV2CompletionHelper
    {
        public static async Task NavigateNextOrCompleteAsync(QuestionnaireV2FlowState flowState, int currentSectionIndex)
        {
            var nextSectionIndex = currentSectionIndex + 1;

            if (nextSectionIndex < flowState.Info.Sections.Count)
            {
                await Shell.Current.Navigation.PushAsync(new QuestionnaireV2QuestionsPage(flowState, nextSectionIndex));
                return;
            }

            var address = KeysModel.GetPublicKey();

            var answers = new QuestionnaireAnswers
            {
                AccountAddress = address,
                UserId = $"User_{address}",
                Responses = flowState.Responses
            };

            var assessment = await QuestionnaireModel.EvaluateAnswersAsync(flowState.Responses);
            await QuestionnaireModel.PostAnswersAsync(answers);

            if (assessment.Passed)
            {
                var onboardingAgreementCoordinator = new OnboardingAgreementCoordinator();
                await onboardingAgreementCoordinator.StartAsync(flowState.Info.Navigation);
                return;
            }

            var sectionTitles = flowState.Info.Sections.ToDictionary(section => section.Id, section => section.Title);

            var failedSections = assessment.Sections
                .Where(section => !section.Passed)
                .Select(section => new QuestionnaireFailedSection
                {
                    Title = sectionTitles.TryGetValue(section.QuestionnaireId, out var title)
                        ? title
                        : section.QuestionnaireId,
                    Reason = string.IsNullOrWhiteSpace(section.Reason)
                        ? "No qualifying criteria met for this investor category."
                        : section.Reason
                })
                .ToList();

            await Shell.Current.Navigation.PushAsync(new QuestionnaireFailedPage(failedSections));
        }
    }
}
