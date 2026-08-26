using PlutoFramework.Model;

namespace PlutoFramework.Components.NavigationBar;

using System.Windows.Input;

public partial class TopNavigationStepperBar : ContentView
{
    public static readonly BindableProperty StepProperty = BindableProperty.Create(
        nameof(Step), typeof(int), typeof(TopNavigationStepperBar),
        defaultValue: 0,
        defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty StepsProperty = BindableProperty.Create(
        nameof(Steps), typeof(int), typeof(TopNavigationStepperBar),
        defaultValue: 0,
        defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty BackCommandProperty = BindableProperty.Create(
        nameof(BackCommand), typeof(ICommand), typeof(TopNavigationStepperBar));

    public TopNavigationStepperBar()
    {
        InitializeComponent();

        BackCommand ??= new Command(async () => await GoBackAsync());
    }

    public int Step
    {
        get => (int)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int Steps
    {
        get => (int)GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    private static async Task GoBackAsync()
    {
        // A visible popup is dismissed before the page goes back.
        if (PopupManager.TryCloseTopPopup())
        {
            return;
        }

        var navigation = Shell.Current?.Navigation;

        if (navigation is not null && navigation.NavigationStack.Count > 1)
        {
            await navigation.PopAsync();
        }
    }
}
