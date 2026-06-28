using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model.Xcavate;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Xcavate
{
    public partial class QuestionnaireV2QuestionItem : ObservableObject
    {
        public required string Id { get; init; }
        public required string Text { get; init; }
        public required string YesParameter { get; init; }
        public required string NoParameter { get; init; }
        public QuestionnaireQuestion? SourceQuestion { get; init; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(YesButtonState))]
        [NotifyPropertyChangedFor(nameof(NoButtonState))]
        private string? answer;

        public ButtonStateEnum YesButtonState => string.Equals(Answer, "Yes", StringComparison.OrdinalIgnoreCase)
            ? ButtonStateEnum.Enabled
            : ButtonStateEnum.GrayEnabled;

        public ButtonStateEnum NoButtonState => string.Equals(Answer, "No", StringComparison.OrdinalIgnoreCase)
            ? ButtonStateEnum.Enabled
            : ButtonStateEnum.GrayEnabled;
    }

    public partial class QuestionnaireV2QuestionsPageViewModel : ObservableObject
    {
        private readonly QuestionnaireV2FlowState flowState;
        private readonly int sectionIndex;
        private readonly QuestionnaireSection section;

        public ObservableCollection<QuestionnaireV2QuestionItem> Questions { get; } = [];

        public int Step => sectionIndex;

        public int Steps => flowState.Info.Sections.Count;

        public string SectionTitle => section.Title;

        public string SectionDescription => section.Description;

        public string IntroText => section.IntroText ?? "";

        public ButtonStateEnum ContinueButtonState => Questions.All(question => question.Answer is not null)
            ? ButtonStateEnum.Enabled
            : ButtonStateEnum.Disabled;

        public QuestionnaireV2QuestionsPageViewModel(QuestionnaireV2FlowState flowState, int sectionIndex)
        {
            this.flowState = flowState;
            this.sectionIndex = sectionIndex;
            section = flowState.GetSection(sectionIndex);

            foreach (var question in section.Questions)
            {
                Questions.Add(new QuestionnaireV2QuestionItem
                {
                    Id = question.Id,
                    Text = question.QuestionText,
                    YesParameter = $"{question.Id}||Yes",
                    NoParameter = $"{question.Id}||No",
                    SourceQuestion = question
                });
            }
        }

        [RelayCommand]
        public void SetAnswer(string parameter)
        {
            var split = parameter.Split("||", StringSplitOptions.None);

            if (split.Length != 2)
            {
                return;
            }

            var questionId = split[0];
            var answer = split[1];

            var question = Questions.FirstOrDefault(item => item.Id == questionId);

            if (question is null)
            {
                return;
            }

            question.Answer = string.Equals(question.Answer, answer, StringComparison.OrdinalIgnoreCase)
                ? null
                : answer;

            OnPropertyChanged(nameof(ContinueButtonState));
        }

        [RelayCommand]
        public async Task ContinueAsync()
        {
            if (ContinueButtonState != ButtonStateEnum.Enabled)
            {
                return;
            }

            var sectionResponses = flowState.Responses[section.Id];

            foreach (var question in Questions)
            {
                sectionResponses[question.Id] = question.Answer ?? "No";
            }

            var requiredConditions = Questions
                .Where(question => string.Equals(question.Answer, "Yes", StringComparison.OrdinalIgnoreCase)
                                   && question.SourceQuestion?.Conditions is not null)
                .Select(question => question.SourceQuestion!.Conditions!)
                .ToList();

            var lastQuestionAnswer = Questions.LastOrDefault()?.Answer;
            var shouldShowDeclaration = string.Equals(lastQuestionAnswer, "No", StringComparison.OrdinalIgnoreCase);

            if (requiredConditions.Count > 0)
            {
                await Shell.Current.Navigation.PushAsync(
                    new QuestionnaireV2ConditionsPage(flowState, sectionIndex, requiredConditions, shouldShowDeclaration));
                return;
            }

            if (shouldShowDeclaration && section.Declarations.Count > 0)
            {
                await Shell.Current.Navigation.PushAsync(new QuestionnaireV2DeclarationPage(flowState, sectionIndex));
                return;
            }

            await QuestionnaireV2CompletionHelper.NavigateNextOrCompleteAsync(flowState, sectionIndex);
        }
    }
}
