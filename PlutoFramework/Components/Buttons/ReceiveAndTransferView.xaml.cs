using PlutoFramework.Model;

namespace PlutoFramework.Components.Buttons;

public partial class ReceiveAndTransferView : ContentView
{
    public static readonly BindableProperty FlowProperty = BindableProperty.Create(
        nameof(Flow), typeof(ReceiveTransferFlowEnum), typeof(ReceiveAndTransferView),
        defaultValue: ReceiveTransferFlowEnum.Substrate);

    public ReceiveAndTransferView()
    {
        InitializeComponent();
    }

    public ReceiveTransferFlowEnum Flow
    {
        get => (ReceiveTransferFlowEnum)GetValue(FlowProperty);

        set => SetValue(FlowProperty, value);
    }

    void OnReceiveClicked(System.Object sender, System.EventArgs e)
    {
        if (Flow == ReceiveTransferFlowEnum.Solana)
        {
            ReceiveAndTransferModel.ReceiveSolana();

            return;
        }

        ReceiveAndTransferModel.Receive();
    }

    void OnTransferClicked(System.Object sender, System.EventArgs e)
    {
        if (Flow == ReceiveTransferFlowEnum.Solana)
        {
            ReceiveAndTransferModel.TransferSolana();

            return;
        }

        ReceiveAndTransferModel.Transfer();
    }
}
