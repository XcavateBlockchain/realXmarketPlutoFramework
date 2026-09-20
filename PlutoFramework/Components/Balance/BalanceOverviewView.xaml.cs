using PlutoFramework.Components.Buttons;

namespace PlutoFramework.Components.Balance;

public partial class BalanceOverviewView : ContentView
{
	/// <summary>
	/// Which network's receive/transfer flows the embedded buttons dispatch to.
	/// Defaults to Substrate, so pages that do not set it (the Polkadot balance page,
	/// the custom layouts) keep their current behavior.
	/// </summary>
	public static readonly BindableProperty FlowProperty = BindableProperty.Create(
		nameof(Flow), typeof(ReceiveTransferFlowEnum), typeof(BalanceOverviewView),
		defaultValue: ReceiveTransferFlowEnum.Substrate,
		propertyChanging: (bindable, oldValue, newValue) =>
			((BalanceOverviewView)bindable).receiveAndTransferView.Flow = (ReceiveTransferFlowEnum)newValue);

	/// <summary>
	/// When true, the USD total rolls digit-by-digit on every change - the animation the
	/// InvestorMainPage cards use - instead of rendering as a plain label. Opt-in so the
	/// other pages embedding this card keep the static label.
	/// </summary>
	public static readonly BindableProperty RollingTickerProperty = BindableProperty.Create(
		nameof(RollingTicker), typeof(bool), typeof(BalanceOverviewView),
		defaultValue: false,
		propertyChanged: (bindable, oldValue, newValue) =>
		{
			var control = (BalanceOverviewView)bindable;
			var rolling = (bool)newValue;

			control.staticValueLabel.IsVisible = !rolling;
			control.rollingValue.IsVisible = rolling;
		});

	public BalanceOverviewView()
	{
		InitializeComponent();
	}

	public ReceiveTransferFlowEnum Flow
	{
		get => (ReceiveTransferFlowEnum)GetValue(FlowProperty);

		set => SetValue(FlowProperty, value);
	}

	public bool RollingTicker
	{
		get => (bool)GetValue(RollingTickerProperty);

		set => SetValue(RollingTickerProperty, value);
	}
}