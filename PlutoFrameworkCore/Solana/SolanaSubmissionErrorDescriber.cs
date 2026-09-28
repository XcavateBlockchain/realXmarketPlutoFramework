namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// Turns a failed submission's raw exception message into what the program's bundled
    /// IDL says the error means. Anything that cannot be decoded - no catalog, unknown
    /// program, or a failure that is not a program rejection at all - keeps the original
    /// message untouched, so decoding can only ever add information, never lose it.
    /// </summary>
    public static class SolanaSubmissionErrorDescriber
    {
        /// <param name="rawMessage">The exception message, e.g. "Could not submit the
        /// transaction on Devnet: Transaction simulation failed: Error processing
        /// Instruction 0: custom program error: 0x177d".</param>
        /// <param name="catalog">The cluster's IDL error catalog, or null where no IDLs
        /// are bundled.</param>
        /// <param name="instructionProgramIds">The program each instruction of the
        /// submitted transaction targeted, in order; index N tells which program raised
        /// "Instruction N" errors. Null when the submitter did not record them.</param>
        public static string Describe(
            string rawMessage,
            AnchorIdlErrorCatalog? catalog,
            IReadOnlyList<string>? instructionProgramIds)
        {
            if (catalog is null
                || !SolanaCustomProgramErrorParser.TryParse(rawMessage, out var parsed)
                || parsed is null
                || instructionProgramIds is null
                || parsed.InstructionIndex < 0
                || parsed.InstructionIndex >= instructionProgramIds.Count)
            {
                return rawMessage;
            }

            var programId = instructionProgramIds[parsed.InstructionIndex];

            // The program must be one the app ships an IDL for; attributing an SPL or
            // system program's code to a known program would invent an explanation.
            if (!catalog.ContainsProgram(programId))
            {
                return rawMessage;
            }

            var subject = catalog.ProgramName(programId) is string name
                ? $"the {name} program"
                : "the program";

            // Everything the message said before the node's reason fragment (the app's
            // own "Could not submit ... simulation failed" lead-in) stays as context.
            var leadIn = rawMessage[..parsed.MatchStart].TrimEnd(' ', ':');
            var nodeDetail = rawMessage.Substring(parsed.MatchStart, parsed.MatchLength);

            var rest = catalog.TryDescribe(programId, parsed.Code, out var error) && error is not null
                ? $" - {error.Message} (error {error.Code})."
                // Stale metadata: the program deployed errors this app version does not
                // know. Name the program and keep the node's own words so support can
                // still match the failure.
                : $" with an error this app version cannot decode (code {parsed.Code}). Node detail: {nodeDetail}";

            return leadIn.Length == 0
                ? char.ToUpperInvariant(subject[0]) + subject[1..] + " refused the transaction" + rest
                : $"{leadIn}: {subject} refused it{rest}";
        }
    }
}
