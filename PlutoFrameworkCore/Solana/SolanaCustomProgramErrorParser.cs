using System.Globalization;
using System.Text.RegularExpressions;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// A "custom program error" pulled out of a node's reason text: which instruction
    /// failed, the program-specific code, and where in the original text the fragment
    /// sits (so a caller can keep it as technical detail or replace it in place).
    /// </summary>
    public sealed record SolanaCustomProgramError(int InstructionIndex, long Code, int MatchStart, int MatchLength);

    /// <summary>
    /// Extracts the machine-readable part of a node's program-rejection reason, e.g.
    /// "Error processing Instruction 0: custom program error: 0x177d". The code is printed
    /// in hex by preflight simulation errors and in decimal elsewhere; both parse to the
    /// same number.
    /// </summary>
    public static class SolanaCustomProgramErrorParser
    {
        private static readonly Regex Pattern = new(
            @"Error processing Instruction (\d+): custom program error: (0x[0-9a-fA-F]+|\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryParse(string? reason, out SolanaCustomProgramError? error)
        {
            error = null;

            if (string.IsNullOrEmpty(reason))
            {
                return false;
            }

            var match = Pattern.Match(reason);

            if (!match.Success)
            {
                return false;
            }

            var codeText = match.Groups[2].Value;

            long code;

            try
            {
                code = codeText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? long.Parse(codeText[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    : long.Parse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                return false;
            }

            error = new SolanaCustomProgramError(index, code, match.Index, match.Length);

            return true;
        }
    }
}
