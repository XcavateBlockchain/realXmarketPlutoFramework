

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Components.Onboarding;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Xcavate
{
    public record QuestionnaireStep
    {
        public required int SectionIndex { get; init; }
        public required string SectionId { get; init; }
        public required string SectionTitle { get; init; }
        public required string SectionDescription { get; init; }
        public required string StepId { get; init; }
        public required string StepType { get; init; }
        public required string QuestionText { get; init; }
        public List<string>? Options { get; init; }
        public string? ParentQuestionId { get; init; }
        public bool IsDeclaration { get; init; }
        public bool IsPrimaryQuestion { get; init; }
    }

    public partial class QuestionnairePageViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentStep))]
        [NotifyPropertyChangedFor(nameof(Step))]
        [NotifyPropertyChangedFor(nameof(Steps))]
        [NotifyPropertyChangedFor(nameof(SectionTitle))]
        [NotifyPropertyChangedFor(nameof(SectionDescription))]
        [NotifyPropertyChangedFor(nameof(QuestionText))]
        [NotifyPropertyChangedFor(nameof(PromptText))]
        [NotifyPropertyChangedFor(nameof(IsOptionsVisible))]
        [NotifyPropertyChangedFor(nameof(IsTextInputVisible))]
        [NotifyPropertyChangedFor(nameof(CanSubmitTextAnswer))]
        private QuestionnaireInfo? info;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSubmitTextAnswer))]
        private string textAnswer = "";

        private readonly List<QuestionnaireStep> steps = [];
        private readonly Dictionary<string, Dictionary<string, object?>> responses = [];
        private int currentStepIndex = 0;

        public ObservableCollection<string> AnswerOptions { get; } = [];

        public QuestionnaireStep? CurrentStep => steps.Count > 0 && currentStepIndex >= 0 && currentStepIndex < steps.Count
            ? steps[currentStepIndex]
            : null;

        public string SectionTitle => CurrentStep?.SectionTitle ?? "Questionnaire";

        public string SectionDescription => CurrentStep?.SectionDescription ?? "";

        public string QuestionText => CurrentStep?.QuestionText ?? "";

        public string PromptText => IsTextInputVisible ? "Please enter a response" : "Please select a response";

        public bool IsOptionsVisible => CurrentStep is not null && CurrentStep.StepType == "checkbox";

        public bool IsTextInputVisible => CurrentStep is not null && CurrentStep.StepType == "text";

        public bool CanSubmitTextAnswer => !string.IsNullOrWhiteSpace(TextAnswer);

        public ButtonStateEnum SubmitTextAnswerButtonState =>
            CanSubmitTextAnswer ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;

        public int Step => steps.Count == 0 ? 0 : currentStepIndex;

        public int Steps => steps.Count;

        partial void OnInfoChanged(QuestionnaireInfo? value)
        {
            if (value is null)
            {
                return;
            }

            InitializeSteps(value);
            MoveToNextVisibleStep(allowCurrentStep: true);
            RefreshCurrentStepUi();
        }

        private void InitializeSteps(QuestionnaireInfo questionnaireInfo)
        {
            steps.Clear();
            responses.Clear();
            currentStepIndex = 0;

            for (var sectionIndex = 0; sectionIndex < questionnaireInfo.Sections.Count; sectionIndex++)
            {
                var section = questionnaireInfo.Sections[sectionIndex];

                responses[section.Id] = [];

                foreach (var question in section.Questions)
                {
                    steps.Add(new QuestionnaireStep
                    {
                        SectionIndex = sectionIndex,
                        SectionId = section.Id,
                        SectionTitle = section.Title,
                        SectionDescription = section.Description,
                        StepId = question.Id,
                        StepType = question.Type,
                        QuestionText = question.QuestionText,
                        Options = question.Options,
                        IsDeclaration = false,
                        IsPrimaryQuestion = true
                    });

                    if (question.Conditions is not null)
                    {
                        steps.Add(new QuestionnaireStep
                        {
                            SectionIndex = sectionIndex,
                            SectionId = section.Id,
                            SectionTitle = section.Title,
                            SectionDescription = section.Description,
                            StepId = question.Conditions.Id,
                            StepType = question.Conditions.Type,
                            QuestionText = question.Conditions.QuestionText,
                            Options = question.Conditions.Options,
                            ParentQuestionId = question.Id,
                            IsDeclaration = false,
                            IsPrimaryQuestion = false
                        });
                    }
                }

                foreach (var declaration in section.Declarations)
                {
                    steps.Add(new QuestionnaireStep
                    {
                        SectionIndex = sectionIndex,
                        SectionId = section.Id,
                        SectionTitle = section.Title,
                        SectionDescription = section.Description,
                        StepId = declaration.Id,
                        StepType = "checkbox",
                        QuestionText = declaration.QuestionText,
                        Options = ["I agree"],
                        IsDeclaration = true,
                        IsPrimaryQuestion = false
                    });
                }
            }
        }

        private void RefreshCurrentStepUi()
        {
            TextAnswer = "";

            AnswerOptions.Clear();

            if (CurrentStep?.Options is not null)
            {
                foreach (var option in CurrentStep.Options)
                {
                    AnswerOptions.Add(option);
                }
            }

            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(Step));
            OnPropertyChanged(nameof(Steps));
            OnPropertyChanged(nameof(SectionTitle));
            OnPropertyChanged(nameof(SectionDescription));
            OnPropertyChanged(nameof(QuestionText));
            OnPropertyChanged(nameof(PromptText));
            OnPropertyChanged(nameof(IsOptionsVisible));
            OnPropertyChanged(nameof(IsTextInputVisible));
            OnPropertyChanged(nameof(CanSubmitTextAnswer));
            OnPropertyChanged(nameof(SubmitTextAnswerButtonState));
        }

        private bool IsStepVisible(QuestionnaireStep step)
        {
            if (string.IsNullOrEmpty(step.ParentQuestionId))
            {
                return true;
            }

            if (!responses.TryGetValue(step.SectionId, out var sectionResponses))
            {
                return false;
            }

            if (!sectionResponses.TryGetValue(step.ParentQuestionId, out var answer))
            {
                return false;
            }

            return string.Equals(answer?.ToString(), "Yes", StringComparison.OrdinalIgnoreCase);
        }

        private void MoveToNextVisibleStep(bool allowCurrentStep)
        {
            if (!allowCurrentStep)
            {
                currentStepIndex++;
            }

            while (currentStepIndex < steps.Count && !IsStepVisible(steps[currentStepIndex]))
            {
                currentStepIndex++;
            }
        }

        private void SetAnswer(string sectionId, string answerKey, object value)
        {
            if (!responses.TryGetValue(sectionId, out var sectionResponses))
            {
                sectionResponses = [];
                responses[sectionId] = sectionResponses;
            }

            sectionResponses[answerKey] = value;
        }

        private bool ShouldStoreAsNoForSingleYesOption(QuestionnaireStep step, string selectedOption)
        {
            if (step.IsDeclaration)
            {
                return false;
            }

            if (!string.Equals(selectedOption, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (step.Options is null || step.Options.Count != 1 || !string.Equals(step.Options[0], "Yes", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!responses.TryGetValue(step.SectionId, out var sectionResponses))
            {
                return false;
            }

            foreach (var previousStep in steps.Take(currentStepIndex))
            {
                if (previousStep.SectionIndex != step.SectionIndex || !previousStep.IsPrimaryQuestion)
                {
                    continue;
                }

                if (sectionResponses.TryGetValue(previousStep.StepId, out var previousAnswer)
                    && string.Equals(previousAnswer?.ToString(), "Yes", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task CompleteQuestionnaireAsync()
        {
            if (Info is null)
            {
                return;
            }

            var address = KeysModel.GetPublicKey();

            var answers = new QuestionnaireAnswers
            {
                AccountAddress = address,
                UserId = $"User_{address}",
                Responses = responses
            };

            // Evaluate first using the v2 stateless endpoint, then persist responses.
            var assessment = await QuestionnaireModel.EvaluateAnswersAsync(responses);
            await QuestionnaireModel.PostAnswersAsync(answers);

            if (assessment?.Passed == true)
            {
                var onboardingAgreementCoordinator = new OnboardingAgreementCoordinator();
                await onboardingAgreementCoordinator.StartAsync(Info.Navigation);
                return;
            }

            var sectionTitles = Info.Sections.ToDictionary(section => section.Id, section => section.Title);

            var failedSections = assessment?.Sections
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
                .ToList() ?? [];

            await Shell.Current.Navigation.PushAsync(new QuestionnaireFailedPage(failedSections));
        }

        [RelayCommand]
        public async Task SelectAnswerAsync(string selectedOption)
        {
            if (CurrentStep is null)
            {
                return;
            }

            if (CurrentStep.IsDeclaration)
            {
                SetAnswer(CurrentStep.SectionId, CurrentStep.StepId, true);
            }
            else
            {
                var effectiveAnswer = ShouldStoreAsNoForSingleYesOption(CurrentStep, selectedOption)
                    ? "No"
                    : selectedOption;

                SetAnswer(CurrentStep.SectionId, CurrentStep.StepId, effectiveAnswer);

                if (string.IsNullOrWhiteSpace(CurrentStep.ParentQuestionId))
                {
                    var childConditionStep = steps.FirstOrDefault(step =>
                        step.SectionId == CurrentStep.SectionId &&
                        step.ParentQuestionId == CurrentStep.StepId);

                    if (childConditionStep is not null && !string.Equals(effectiveAnswer, "Yes", StringComparison.OrdinalIgnoreCase))
                    {
                        if (responses.TryGetValue(CurrentStep.SectionId, out var sectionResponses))
                        {
                            sectionResponses.Remove(childConditionStep.StepId);
                        }
                    }
                }
            }

            MoveToNextVisibleStep(allowCurrentStep: false);

            if (currentStepIndex >= steps.Count)
            {
                await CompleteQuestionnaireAsync();
                return;
            }

            RefreshCurrentStepUi();
        }

        [RelayCommand(CanExecute = nameof(CanSubmitTextAnswer))]
        public async Task SubmitTextAnswerAsync()
        {
            if (CurrentStep is null || !IsTextInputVisible || string.IsNullOrWhiteSpace(TextAnswer))
            {
                return;
            }

            SetAnswer(CurrentStep.SectionId, CurrentStep.StepId, TextAnswer.Trim());

            MoveToNextVisibleStep(allowCurrentStep: false);

            if (currentStepIndex >= steps.Count)
            {
                await CompleteQuestionnaireAsync();
                return;
            }

            RefreshCurrentStepUi();
        }

        partial void OnTextAnswerChanged(string value)
        {
            SubmitTextAnswerCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(SubmitTextAnswerButtonState));
        }
    }
}
