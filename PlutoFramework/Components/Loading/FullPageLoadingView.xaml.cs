namespace PlutoFramework.Components.Loading;

public partial class FullPageLoadingView : ContentView
{
	public FullPageLoadingView()
	{
		InitializeComponent();

        // Pinned to the layer the popups that must stay reachable while loading
        // (password prompt, wallet signing) define themselves against.
        ZIndex = PopupLayers.Loading;

        BindingContext = DependencyService.Get<FullPageLoadingViewModel>();

    }
}