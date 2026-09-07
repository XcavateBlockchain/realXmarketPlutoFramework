
namespace PlutoFramework.Components.Buttons
{
    /// <summary>
    /// Which network's receive/transfer flows a <see cref="ReceiveAndTransferView"/>
    /// dispatches to. Substrate is the default, so every existing use site keeps its
    /// current behavior.
    /// </summary>
    public enum ReceiveTransferFlowEnum
    {
        Substrate,
        Solana,
    }
}
