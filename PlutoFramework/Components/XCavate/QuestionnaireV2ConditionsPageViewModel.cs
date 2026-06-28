using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model.Xcavate;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Xcavate
{
    public partial class QuestionnaireV2ConditionsPageViewModel : ObservableObject
    {
        private readonly QuestionnaireV2FlowState flowState;
        private readonly int sectionIndex;
        private readonly QuestionnaireSection section;
        private readonly IReadOnlyList<QuestionnaireCondition> requiredConditions;
        private readonly bool shouldShowDeclaration;

        private int currentConditionIndex = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanContinue))]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private string textAnswer = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanContinue))]
        [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
        private string selectedOption = "";

        public ObservableCollection<string> AnswerOptions { get; } = [];

        public int Step => sectionIndex;

        public int Steps => flowState.Info.Sections.Count;

        public QuestionnaireCondition CurrentCondition => requiredConditions[currentConditionIndex];

        public string CurrentQuestionText => CurrentCondition.QuestionText;

        public bool IsOptionsVisible => CurrentCondition.Type == "checkbox";

        public bool IsTextVisible => CurrentCondition.Type == "text";

        public bool CanContinue => IsOptionsVisible
            ? !string.IsNullOrWhiteSpace(SelectedOption)
            : !string.IsNullOrWhiteSpace(TextAnswer);

        public ButtonStateEnum ContinueButtonState => CanContinue ? ButtonStateEnum.Enabled : ButtonStateEnum.Disabled;

        public QuestionnaireV2ConditionsPageViewModel(
            QuestionnaireV2FlowState flowState,
            int sectionIndex,
            List<QuestionnaireCondition> requiredConditions,
            bool shouldShowDeclaration)
        {
            this.flowState = flowState;
            this.sectionIndex = sectionIndex;
            this.requiredConditions = requiredConditions;
            this.shouldShowDeclaration = shouldShowDeclaration;
            section = flowState.GetSection(sectionIndex);

            RefreshCurrentConditionUi();
        }

        [RelayCommand]
        public async Task SelectOptionAndContinueAsync(string option)
        {
            SelectedOption = option;

            await ContinueAsync();
        }

        [RelayCommand]
        public async Task ContinueAsync()
        {
            if (!CanContinue)
            {
                return;
            }

            var value = IsOptionsVisible
                ? SelectedOption.Trim()
                : TextAnswer.Trim();

            flowState.Responses[section.Id][CurrentCondition.Id] = value;

            currentConditionIndex++;

            if (currentConditionIndex < requiredConditions.Count)
            {
                RefreshCurrentConditionUi();
                return;
            }

            if (shouldShowDeclaration && section.Declarations.Count > 0)
            {
                await Shell.Current.Navigation.PushAsync(new QuestionnaireV2DeclarationPage(flowState, sectionIndex));
                return;
            }

            await QuestionnaireV2CompletionHelper.NavigateNextOrCompleteAsync(flowState, sectionIndex);
        }

        private void RefreshCurrentConditionUi()
        {
            TextAnswer = "";
            SelectedOption = "";
            AnswerOptions.Clear();

            if (CurrentCondition.Options is not null)
            {
                foreach (var option in CurrentCondition.Options)
                {
                    AnswerOptions.Add(option);
                }
            }

            OnPropertyChanged(nameof(CurrentCondition));
            OnPropertyChanged(nameof(CurrentQuestionText));
            OnPropertyChanged(nameof(IsOptionsVisible));
            OnPropertyChanged(nameof(IsTextVisible));
            OnPropertyChanged(nameof(CanContinue));
            OnPropertyChanged(nameof(ContinueButtonState));
        }
    }
}
