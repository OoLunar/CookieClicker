using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Content.Authentication;
using GenHTTP.Api.Protocol;
using Microsoft.Extensions.Configuration;

namespace OoLunar.CookieClicker.Headers
{
    public sealed class DiscordHeaderAuthentication
    {
        private readonly byte[] _publicKey;

        public DiscordHeaderAuthentication(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            byte[] publicKey = new byte[32];
            FromHex(configuration["Discord:PublicKey"], publicKey);
            _publicKey = publicKey;
        }

        public ValueTask<IUser?> Authenticate(IRequest request, string key)
        {
            if (!request.Headers.TryGetValue("X-Signature-Timestamp", out string? timestamp) || !request.Headers.TryGetValue("X-Signature-Ed25519", out string? signature))
            {
                return ValueTask.FromException<IUser?>(new ProviderException(ResponseStatus.Forbidden, "Missing authentication headers"));
            }

            string content = string.Empty;
            if (request.Content is not null)
            {
                StreamReader reader = new(request.Content);
                content = reader.ReadToEnd();
            }

            Span<byte> signatureSpan = stackalloc byte[64];
            FromHex(signature, signatureSpan);
            return Ed25519.Verify(signatureSpan, Encoding.UTF8.GetBytes($"{timestamp}{content}"), _publicKey)
                ? ValueTask.FromResult<IUser?>(new DiscordUser())
                : ValueTask.FromException<IUser?>(new ProviderException(ResponseStatus.Forbidden, "Invalid authentication headers"));
        }

        private static void FromHex(ReadOnlySpan<char> hex, Span<byte> destination)
        {
            if ((hex.Length & 1) == 1)
            {
                throw new ArgumentException("Hex string must have an even number of characters.");
            }
            else if (destination.Length < hex.Length / 2)
            {
                throw new ArgumentException("Destination buffer is too small.");
            }

            for (int i = 0, j = 0; i < hex.Length; i += 2, j++)
            {
                byte highNibble = HexCharToByte(hex[i]);
                byte lowNibble = HexCharToByte(hex[i + 1]);
                destination[j] = (byte)((highNibble << 4) | lowNibble);
            }
        }

        private static byte HexCharToByte(char c) => c switch
        {
            >= '0' and <= '9' => (byte)(c - '0'),
            >= 'a' and <= 'f' => (byte)(c - 'a' + 10),
            >= 'A' and <= 'F' => (byte)(c - 'A' + 10),
            _ => throw new ArgumentException($"Invalid hex character '{c}'."),
        };
    }
}
