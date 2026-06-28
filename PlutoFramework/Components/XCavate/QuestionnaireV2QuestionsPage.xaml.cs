using PlutoFramework.Model.Xcavate;
using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Xcavate;

public partial class QuestionnaireV2QuestionsPage : PageTemplate
{
    public QuestionnaireV2QuestionsPage(QuestionnaireV2FlowState flowState, int sectionIndex)
    {
        InitializeComponent();

        BindingContext = new QuestionnaireV2QuestionsPageViewModel(flowState, sectionIndex);
    }

    public QuestionnaireV2QuestionsPage(QuestionnaireInfo info)
        : this(new QuestionnaireV2FlowState(info), 0)
    {
    }
}
