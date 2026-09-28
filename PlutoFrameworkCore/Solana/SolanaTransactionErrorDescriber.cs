using Solnet.Rpc.Models;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// One <c>getSignatureStatuses</c> error, as the transaction error page shows it.
    /// </summary>
    /// <remarks>
    /// Not a full translation table: Solnet's enum names split into readable words by
    /// default, so a new error code the cluster adds still reads instead of vanishing.
    /// The few special cases are the failures a transfer actually produces, in words that
    /// say what the user can do about them.
    /// </remarks>
    public static class SolanaTransactionErrorDescriber
    {
        /// <summary>
        /// The plain-words reason a submitted transaction failed. Null is the
        /// "failed, but the node gave no reason" case.
        /// </summary>
        public static string Describe(TransactionError? error)
        {
            if (error is null)
            {
                return "The transaction failed on the network.";
            }

            // An instruction error carries the index of the offending instruction and a
            // program-specific code, and without them the user cannot tell which part of
            // the transaction was rejected.
            if (error.Type == TransactionErrorType.InstructionError && error.InstructionError is not null)
            {
                var inner = error.InstructionError;

                var custom = inner.CustomError is null
                    ? string.Empty
                    : $" (code {inner.CustomError.Value})";

                return $"The program rejected instruction {inner.InstructionIndex}: {Readable(inner.Type)}{custom}.";
            }

            return error.Type switch
            {
                TransactionErrorType.InsufficientFundsForFee => "Not enough SOL to pay the transaction fee.",
                TransactionErrorType.InsufficientFundsForRent => "Not enough SOL to cover the rent for a new account.",
                TransactionErrorType.BlockhashNotFound => "The recent blockhash had expired, so the transaction could no longer be processed.",
                _ => $"{Readable(error.Type)}.",
            };
        }

        /// <summary>
        /// The same reason, with custom program errors decoded through the cluster's
        /// bundled IDL catalog. <paramref name="instructionProgramIds"/> maps instruction
        /// index to the program it targeted - the transaction's own instruction list - so
        /// a code only ever resolves against the program that actually raised it. Anything
        /// the catalog cannot decode falls back to <see cref="Describe(TransactionError?)"/>.
        /// </summary>
        public static string Describe(
            TransactionError? error,
            AnchorIdlErrorCatalog? catalog,
            IReadOnlyList<string>? instructionProgramIds)
        {
            if (error?.Type == TransactionErrorType.InstructionError
                && error.InstructionError is { Type: InstructionErrorType.Custom, CustomError: not null } inner
                && catalog is not null
                && instructionProgramIds is not null
                && inner.InstructionIndex >= 0
                && inner.InstructionIndex < instructionProgramIds.Count
                && catalog.ContainsProgram(instructionProgramIds[inner.InstructionIndex]))
            {
                var programId = instructionProgramIds[inner.InstructionIndex];

                var subject = catalog.ProgramName(programId) is string name
                    ? $"The {name} program"
                    : "The program";

                if (catalog.TryDescribe(programId, inner.CustomError.Value, out var idlError) && idlError is not null)
                {
                    return $"{subject} rejected instruction {inner.InstructionIndex}: {idlError.Message} (error {idlError.Code}).";
                }

                // Stale metadata: the program deployed errors this app version does not
                // know. Name the program and keep the code so the failure stays matchable.
                return $"{subject} rejected instruction {inner.InstructionIndex} with an error this app version cannot decode (code {inner.CustomError.Value}).";
            }

            return Describe(error);
        }

        /// <summary>
        /// "InsufficientFundsForFee" becomes "Insufficient funds for fee".
        /// </summary>
        private static string Readable(Enum value)
        {
            var name = value.ToString();

            // char + char would promote to int, so the pieces are built as strings.
            var pieces = name.Select(
                (c, i) => i > 0 && char.IsUpper(c)
                    ? " " + char.ToLowerInvariant(c).ToString()
                    : c.ToString()).ToArray();

            return string.Concat(pieces);
        }
    }
}
