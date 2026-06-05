using PlutoFramework.Model;

namespace PlutoFramework.Components.Buttons;

public partial class ReceiveAndTransferView : ContentView
{
    public ReceiveAndTransferView()
    {
        InitializeComponent();
    }

    void OnReceiveClicked(System.Object sender, System.EventArgs e)
    {
        ReceiveAndTransferModel.Receive();
    }

    void OnTransferClicked(System.Object sender, System.EventArgs e)
    {
        ReceiveAndTransferModel.Transfer();
    }
}
