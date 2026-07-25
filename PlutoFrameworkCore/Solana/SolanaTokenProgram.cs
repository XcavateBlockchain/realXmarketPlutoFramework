namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// SPL token program ids. Which one owns a mint decides which
    /// <c>getTokenAccountsByOwner</c> call returns its accounts, so a mint filed under the
    /// wrong program reports a zero balance rather than an error.
    /// </summary>
    public static class SolanaTokenProgram
    {
        public const string Legacy = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

        public const string Token2022 = "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb";
    }
}
