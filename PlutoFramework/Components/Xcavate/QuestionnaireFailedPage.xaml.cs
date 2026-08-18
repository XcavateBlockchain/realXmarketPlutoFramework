using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Xcavate;

public partial class QuestionnaireFailedPage : PageTemplate
{
	public QuestionnaireFailedPage(IEnumerable<QuestionnaireFailedSection> failedSections)
	{
		InitializeComponent();

        var viewModel = new QuestionnaireFailedPageViewModel();
        viewModel.SetFailedSections(failedSections);

		BindingContext = viewModel;
    }
}