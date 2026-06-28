using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Xcavate
{
    public partial class QuestionnaireV2DeclarationItem : ObservableObject
    {
        public required string Id { get; init; }
        public required string Text { get; init; }
        public required string YesParameter { get; init; }
        public required string NoParameter { get; init; }

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

    public partial class QuestionnaireV2DeclarationPageViewModel : ObservableObject
    {
        private readonly QuestionnaireV2FlowState flowState;
        private readonly int sectionIndex;
        private readonly string sectionId;

        public ObservableCollection<QuestionnaireV2DeclarationItem> Declarations { get; } = [];

        public int Step => sectionIndex;

        public int Steps => flowState.Info.Sections.Count;

        public ButtonStateEnum ContinueButtonState => Declarations.All(declaration => declaration.Answer is not null)
            ? ButtonStateEnum.Enabled
            : ButtonStateEnum.Disabled;

        public QuestionnaireV2DeclarationPageViewModel(QuestionnaireV2FlowState flowState, int sectionIndex)
        {
            this.flowState = flowState;
            this.sectionIndex = sectionIndex;

            var section = flowState.GetSection(sectionIndex);
            sectionId = section.Id;

            foreach (var declaration in section.Declarations)
            {
                Declarations.Add(new QuestionnaireV2DeclarationItem
                {
                    Id = declaration.Id,
                    Text = declaration.QuestionText,
                    YesParameter = $"{declaration.Id}||Yes",
                    NoParameter = $"{declaration.Id}||No"
                });
            }
        }

        [RelayCommand]
        public void SetDeclaration(string parameter)
        {
            var split = parameter.Split("||", StringSplitOptions.None);

            if (split.Length != 2)
            {
                return;
            }

            var declarationId = split[0];
            var answer = split[1];

            var declaration = Declarations.FirstOrDefault(item => item.Id == declarationId);

            if (declaration is null)
            {
                return;
            }

            declaration.Answer = string.Equals(declaration.Answer, answer, StringComparison.OrdinalIgnoreCase)
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

            var sectionResponses = flowState.Responses[sectionId];

            foreach (var declaration in Declarations)
            {
                sectionResponses[declaration.Id] = string.Equals(declaration.Answer, "Yes", StringComparison.OrdinalIgnoreCase);
            }

            await QuestionnaireV2CompletionHelper.NavigateNextOrCompleteAsync(flowState, sectionIndex);
        }
    }
}
