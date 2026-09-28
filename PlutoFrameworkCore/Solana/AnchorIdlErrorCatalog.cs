using System.Text.Json;

namespace PlutoFrameworkCore.Solana
{
    /// <summary>
    /// One entry of an Anchor IDL's <c>errors</c> array: the numeric code the program
    /// raises, its enum-style name, and the human-readable message the program author
    /// wrote for it.
    /// </summary>
    public sealed record AnchorIdlError(long Code, string Name, string Message);

    /// <summary>
    /// The error tables of every Anchor IDL bundled with the app, keyed by program
    /// address. Built from the checked-in IDL JSON (<c>idls/{cluster}/*.json</c>) at
    /// startup; used to turn "custom program error: 0x177d" into the message the program
    /// author wrote, e.g. "Listing is not active".
    /// </summary>
    /// <remarks>
    /// Keyed by program because codes are only unique within one program - 6013 is
    /// "Listing is not active" in marketplace and "Postcode is too long" in property.
    /// Programs evolve after an app version ships, so lookups that miss are an expected
    /// case, not an error: callers fall back to naming the program with the code, or to
    /// the node's raw reason when the program itself is unknown.
    /// </remarks>
    public sealed class AnchorIdlErrorCatalog
    {
        private sealed record Program(string? Name, Dictionary<long, AnchorIdlError> Errors);

        private readonly Dictionary<string, Program> programs = new(StringComparer.Ordinal);

        public int ProgramCount => programs.Count;

        /// <summary>
        /// Adds one IDL file's error table. Returns false - never throws - when the JSON
        /// is malformed or carries no program address: a bundled file is app-shipped data,
        /// and a corrupt one must not take startup or the other programs down with it.
        /// Re-adding an address replaces that program's table, so an updated IDL wins.
        /// </summary>
        public bool TryAddIdl(string? idlJson)
        {
            if (string.IsNullOrWhiteSpace(idlJson))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(idlJson);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("address", out var addressElement)
                    || addressElement.ValueKind != JsonValueKind.String
                    || addressElement.GetString() is not string address
                    || address.Length == 0)
                {
                    return false;
                }

                string? name = null;

                if (root.TryGetProperty("metadata", out var metadata)
                    && metadata.ValueKind == JsonValueKind.Object
                    && metadata.TryGetProperty("name", out var nameElement)
                    && nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString();
                }

                var errors = new Dictionary<long, AnchorIdlError>();

                if (root.TryGetProperty("errors", out var errorsElement)
                    && errorsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in errorsElement.EnumerateArray())
                    {
                        // One truncated entry skips itself; the rest of the table still loads.
                        if (entry.ValueKind != JsonValueKind.Object
                            || !entry.TryGetProperty("code", out var codeElement)
                            || !codeElement.TryGetInt64(out var code)
                            || !entry.TryGetProperty("msg", out var msgElement)
                            || msgElement.ValueKind != JsonValueKind.String
                            || msgElement.GetString() is not string message)
                        {
                            continue;
                        }

                        var errorName = entry.TryGetProperty("name", out var nameProp)
                            && nameProp.ValueKind == JsonValueKind.String
                                ? nameProp.GetString() ?? string.Empty
                                : string.Empty;

                        errors[code] = new AnchorIdlError(code, errorName, message);
                    }
                }

                programs[address] = new Program(name, errors);

                return true;
            }
            catch (Exception)
            {
                // Not just JsonException: any surprise in app-shipped data means "this file
                // contributes nothing", never a startup crash.
                return false;
            }
        }

        /// <summary>
        /// The program's own words for <paramref name="code"/>, or false when the program
        /// is unknown or its bundled IDL predates the code (stale metadata).
        /// </summary>
        public bool TryDescribe(string? programId, long code, out AnchorIdlError? error)
        {
            error = null;

            if (programId is null
                || !programs.TryGetValue(programId, out var program)
                || !program.Errors.TryGetValue(code, out var found))
            {
                return false;
            }

            error = found;

            return true;
        }

        /// <summary>
        /// Whether any bundled IDL carries this program's address. Distinct from
        /// <see cref="ProgramName"/>: a program whose IDL has no metadata name is still
        /// known, and its error codes must not be read as another program's.
        /// </summary>
        public bool ContainsProgram(string? programId) =>
            programId is not null && programs.ContainsKey(programId);

        /// <summary>
        /// The IDL's metadata name for the program ("marketplace"), or null when the
        /// program is unknown or its IDL does not name it.
        /// </summary>
        public string? ProgramName(string? programId) =>
            programId is not null && programs.TryGetValue(programId, out var program)
                ? program.Name
                : null;
    }
}
