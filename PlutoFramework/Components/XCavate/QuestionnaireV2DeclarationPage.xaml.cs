using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Xcavate;

public partial class QuestionnaireV2DeclarationPage : PageTemplate
{
    public QuestionnaireV2DeclarationPage(QuestionnaireV2FlowState flowState, int sectionIndex)
    {
        InitializeComponent();

        BindingContext = new QuestionnaireV2DeclarationPageViewModel(flowState, sectionIndex);
    }
}
