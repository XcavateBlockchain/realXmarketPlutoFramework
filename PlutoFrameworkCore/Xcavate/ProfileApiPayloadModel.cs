using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PlutoFrameworkCore.Xcavate
{
    /// <summary>
    /// Recognises the request-signing payload of the Xcavate Profile API
    /// (https://github.com/pyrahermesagent/XcavateProfile):
    /// <c>{METHOD}:{path}:{body hash}:{timestamp}</c>, where the hash is the 0x-prefixed
    /// uppercase-hex Blake2b-128 of the body (empty for bodyless requests) and the
    /// timestamp is ISO-8601 UTC with exactly seven fractional-second digits.
    ///
    /// Used to decide whether a message-signing request from a hosted dapp is a routine
    /// Profile API authentication the wallet may sign without the confirmation sheet. The
    /// match is deliberately strict: a false negative costs the user one tap on the sheet,
    /// while a false positive would sign silently.
    /// </summary>
    public static class ProfileApiPayloadModel
    {
        /// <summary>
        /// How far a payload's timestamp may lie from now and still be auto-signed. Wider
        /// than the server's own 5-minute acceptance window so honest clock drift never
        /// costs the user a popup, but still refuses far-dated payloads a page might try
        /// to stockpile signatures with.
        /// </summary>
        public static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Real payloads are a method, an API path, a fixed-size hash and a fixed-size
        /// timestamp - nowhere near this long.
        /// </summary>
        private const int MaxPayloadLength = 1024;

        private static readonly UTF8Encoding StrictUtf8 = new(
            encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        // METHOD : path : hash-or-empty : timestamp. The path class is printable ASCII,
        // which includes ':' - the fixed-shape hash and end-anchored timestamp keep the
        // split unambiguous anyway.
        private static readonly Regex PayloadRegex = new(
            @"^(?:GET|POST|PUT|DELETE):/[\x21-\x7E]*:(?:0x[0-9A-F]{32})?:(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z)\z",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether the message a dapp asked to have signed is a Profile API signing payload
        /// with a timestamp near <paramref name="utcNow"/>.
        /// </summary>
        public static bool IsProfileApiSignPayload(byte[] messageBytes, DateTime utcNow)
        {
            if (messageBytes.Length is 0 or > MaxPayloadLength)
            {
                return false;
            }

            string message;

            try
            {
                message = StrictUtf8.GetString(messageBytes);
            }
            catch (DecoderFallbackException)
            {
                // Not text at all - a transaction blob or a digest, never a Profile API payload.
                return false;
            }

            return IsProfileApiSignPayload(message, utcNow);
        }

        /// <inheritdoc cref="IsProfileApiSignPayload(byte[], DateTime)"/>
        public static bool IsProfileApiSignPayload(string message, DateTime utcNow)
        {
            if (message.Length > MaxPayloadLength)
            {
                return false;
            }

            var match = PayloadRegex.Match(message);

            if (!match.Success)
            {
                return false;
            }

            // The regex fixes the shape; parsing rejects impossible dates like month 13.
            if (!DateTime.TryParseExact(
                    match.Groups[1].Value,
                    "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp))
            {
                return false;
            }

            return (timestamp - utcNow).Duration() <= TimestampTolerance;
        }
    }
}
