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

	public BalanceOverviewView()
	{
		InitializeComponent();
	}

	public ReceiveTransferFlowEnum Flow
	{
		get => (ReceiveTransferFlowEnum)GetValue(FlowProperty);

		set => SetValue(FlowProperty, value);
	}
}