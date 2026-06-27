using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Xcavate;

public partial class QuestionnaireFailedPage : PageTemplate
{
	public QuestionnaireFailedPage(string message)
	{
		InitializeComponent();

		BindingContext = new QuestionnaireFailedPageViewModel
		{
			Text = message,
        };
    }
}