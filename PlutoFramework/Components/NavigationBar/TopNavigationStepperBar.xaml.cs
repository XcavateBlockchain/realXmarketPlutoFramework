namespace PlutoFramework.Components.NavigationBar;

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

    public TopNavigationStepperBar()
    {
        InitializeComponent();
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

    private async void OnBackClicked(object sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }
}
