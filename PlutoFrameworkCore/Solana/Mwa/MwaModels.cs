using System.Text.Json.Serialization;

namespace PlutoFrameworkCore.Solana.Mwa
{
    /// <summary>
    /// How this app identifies itself to the wallet. Shown to the user on the approval
    /// screen, so the values should be recognisable.
    /// </summary>
    public record MwaIdentity
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        /// <summary>
        /// Absolute, hierarchical URI. On Android the wallet may verify it against Digital
        /// Asset Links to confirm the request really comes from this app.
        /// </summary>
        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        /// <summary>
        /// Relative path or a data URI holding a base64 SVG, WebP, PNG or GIF.
        /// </summary>
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }
    }

    internal record MwaAuthorizeRequest
    {
        [JsonPropertyName("identity")]
        public required MwaIdentity Identity { get; set; }

        [JsonPropertyName("chain")]
        public required string Chain { get; set; }

        /// <summary>
        /// Present only when reauthorizing an existing grant. Mobile Wallet Adapter 2.0
        /// deprecated the separate reauthorize method in favour of this field.
        /// </summary>
        [JsonPropertyName("auth_token")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AuthToken { get; set; }
    }

    internal record MwaAuthorizeResponse
    {
        [JsonPropertyName("auth_token")]
        public string? AuthToken { get; set; }

        [JsonPropertyName("accounts")]
        public List<MwaAccountResponse>? Accounts { get; set; }

        [JsonPropertyName("wallet_uri_base")]
        public string? WalletUriBase { get; set; }
    }

    internal record MwaAccountResponse
    {
        /// <summary>
        /// Base64-encoded public key, not the base58 form users see. Convert with
        /// <see cref="SolanaAddress.FromBase64"/>.
        /// </summary>
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("display_address")]
        public string? DisplayAddress { get; set; }

        [JsonPropertyName("display_address_format")]
        public string? DisplayAddressFormat { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("chains")]
        public List<string>? Chains { get; set; }

        [JsonPropertyName("features")]
        public List<string>? Features { get; set; }
    }

    internal record MwaDeauthorizeRequest
    {
        [JsonPropertyName("auth_token")]
        public required string AuthToken { get; set; }
    }

    public record MwaCapabilities
    {
        [JsonPropertyName("max_transactions_per_request")]
        public int? MaxTransactionsPerRequest { get; set; }

        [JsonPropertyName("max_messages_per_request")]
        public int? MaxMessagesPerRequest { get; set; }

        [JsonPropertyName("supported_transaction_versions")]
        public List<object>? SupportedTransactionVersions { get; set; }

        [JsonPropertyName("features")]
        public List<string>? Features { get; set; }
    }

    internal record MwaSignMessagesRequest
    {
        /// <summary>Base64-encoded addresses that should sign.</summary>
        [JsonPropertyName("addresses")]
        public required List<string> Addresses { get; set; }

        /// <summary>Base64-encoded message payloads.</summary>
        [JsonPropertyName("payloads")]
        public required List<string> Payloads { get; set; }
    }

    internal record MwaSignMessagesResponse
    {
        [JsonPropertyName("signed_payloads")]
        public List<string>? SignedPayloads { get; set; }
    }

    internal record MwaSignAndSendTransactionsRequest
    {
        /// <summary>Base64-encoded, fully-formed transaction payloads.</summary>
        [JsonPropertyName("payloads")]
        public required List<string> Payloads { get; set; }
    }

    internal record MwaSignAndSendTransactionsResponse
    {
        /// <summary>Base64-encoded transaction signatures.</summary>
        [JsonPropertyName("signatures")]
        public List<string>? Signatures { get; set; }
    }

    /// <summary>
    /// The outcome of a successful authorization, translated into the shapes this app
    /// stores and displays: a base58 address rather than the protocol's base64.
    /// </summary>
    public record MwaAuthorizationResult
    {
        public required string AuthToken { get; set; }

        /// <summary>Base58 address, ready to store and display.</summary>
        public required string Address { get; set; }

        public required string Chain { get; set; }

        public string? WalletUriBase { get; set; }

        public string? AccountLabel { get; set; }
    }
}
