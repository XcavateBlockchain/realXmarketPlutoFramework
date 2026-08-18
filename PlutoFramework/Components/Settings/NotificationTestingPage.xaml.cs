using PlutoFramework.Templates.PageTemplate;

namespace PlutoFramework.Components.Settings;

/// <summary>
/// Reports whether push notifications would actually reach this device, and offers the
/// repairs - re-register, relink Solana - that are otherwise only triggered automatically.
/// </summary>
public partial class NotificationTestingPage : PageTemplate
{
    private readonly NotificationTestingViewModel viewModel = new();

    public NotificationTestingPage()
    {
        InitializeComponent();

        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Checked on arrival rather than behind a button: the answer is the reason to open
        // this page, and it is a single request.
        _ = viewModel.CheckCommand.ExecuteAsync(null);
    }
}
