using PlutoFramework.Model;
using SolanaAccount = Solnet.Wallet.Account;

namespace PlutoFrameworkCore.Keys
{
    /// <summary>
    /// A locally held Solana account, derived from a BIP39 phrase at m/44'/501'/0'/0'.
    /// </summary>
    public record SolanaMnemonicKey : ISolanaAccountKey
    {
        public required string Mnemonics { get; set; }

        public SolanaAccount Account => SolanaMnemonicsModel.GetAccountFromMnemonics(Mnemonics);

        public string Address => Account.PublicKey.Key;
    }
}
