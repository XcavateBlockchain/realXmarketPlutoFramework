using Microsoft.AspNetCore.WebUtilities;
using PlutoFrameworkCore.AssetDidComm;

namespace PlutoFrameworkCore.Keys
{
    public record EncryptionX25519Key
    {
        public required byte[] SecretKey { get; set; }

        public byte[] PublicKey => X25519Model.DerivePublicKey(SecretKey);

        public string PublicKeyString => WebEncoders.Base64UrlEncode(PublicKey);
    }
}
