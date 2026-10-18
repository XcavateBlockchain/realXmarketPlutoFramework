using System.Text.Json;

namespace PlutoFrameworkCore.Xcavate
{
    /// <summary>
    /// The terms document a lawyer has published through the Xcavate Profile API
    /// (<c>GET /api/profiles/{address}/terms-document</c>): its public URL, the uploaded
    /// file's name and when it was last replaced.
    /// </summary>
    /// <remarks>
    /// One document per wallet, last write wins; a 404 from the endpoint means none and
    /// is the client's "no document" state, not an error.
    /// </remarks>
    public record TermsDocumentInfo(string Url, string FileName, DateTimeOffset UpdatedAt)
    {
        /// <summary>
        /// Reads the endpoint's response body (<c>{"url","fileName","updatedAt"}</c>,
        /// camelCase with an ISO-8601 <c>updatedAt</c>). A body that does not match the
        /// contract throws rather than degrading to a half-empty record - the caller
        /// would surface a wrong file name or an unreadable date as if they were real.
        /// </summary>
        public static TermsDocumentInfo Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"The terms document answer is not a JSON object: {json}");
            }

            return new TermsDocumentInfo(
                RequiredString(root, "url"),
                RequiredString(root, "fileName"),
                RequiredDateTimeOffset(root, "updatedAt"));
        }

        private static string RequiredString(JsonElement root, string property)
        {
            if (root.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!;
            }

            throw new FormatException($"The terms document answer carries no usable \"{property}\": {root}");
        }

        private static DateTimeOffset RequiredDateTimeOffset(JsonElement root, string property)
        {
            if (root.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String
                && value.TryGetDateTimeOffset(out var parsed))
            {
                return parsed;
            }

            throw new FormatException($"The terms document answer carries no usable \"{property}\": {root}");
        }
    }
}
