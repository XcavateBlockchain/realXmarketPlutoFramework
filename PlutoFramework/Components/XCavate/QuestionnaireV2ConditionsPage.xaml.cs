using PlutoFramework.Model.Xcavate;
using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Xcavate;

public partial class QuestionnaireV2ConditionsPage : PageTemplate
{
    public QuestionnaireV2ConditionsPage(QuestionnaireV2FlowState flowState, int sectionIndex, List<QuestionnaireCondition> requiredConditions, bool shouldShowDeclaration)
    {
        InitializeComponent();

        BindingContext = new QuestionnaireV2ConditionsPageViewModel(flowState, sectionIndex, requiredConditions, shouldShowDeclaration);
    }
}
