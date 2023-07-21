using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentResults;
using Microsoft.Extensions.Configuration;
using OoLunar.HyperSharp;

namespace OoLunar.CookieClicker.Responders
{
    public sealed class DiscordHeaderVerifier : IResponder<HyperContext, HyperStatus>
    {
        public string[] Implements { get; init; } = new[] { "IDiscordHeaderVerifier" };
        private readonly byte[] _publicKey;

        public DiscordHeaderVerifier(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            byte[] publicKey = new byte[32];
            FromHex(configuration["Discord:PublicKey"], publicKey);
            _publicKey = publicKey;
        }

        public Task<Result<HyperStatus>> RespondAsync(HyperContext context)
        {
            if (!context.Headers.TryGetValue("X-Signature-Timestamp", out IReadOnlyList<string>? timestamp) || !context.Headers.TryGetValue("X-Signature-Ed25519", out IReadOnlyList<string>? signature))
            {
                return Task.FromResult(Result.Ok(new HyperStatus(HttpStatusCode.Unauthorized, new(), new Error("Missing authentication headers"))));
            }
            else if (timestamp.Count != 1 || signature.Count != 1)
            {
                return Task.FromResult(Result.Ok(new HyperStatus(HttpStatusCode.Unauthorized, new(), new Error("Invalid authentication headers"))));
            }

            if (!context.Headers.TryGetValue("Content-Length", out IReadOnlyList<string>? contentLength) || contentLength.Count != 1 || !int.TryParse(contentLength[0], out int length))
            {
                return Task.FromResult(Result.Ok(new HyperStatus(HttpStatusCode.Unauthorized, new(), new Error("Invalid or incorrect content length"))));
            }

            int timestampLength = Encoding.UTF8.GetByteCount(timestamp[0]);
            byte[] message = new byte[length + timestampLength];
            Encoding.UTF8.GetBytes(timestamp[0], message);
            context.BodyReader.AsStream(true).ReadAtLeast(message.AsSpan(timestampLength, length), length, true);

            string content = Encoding.UTF8.GetString(message.AsSpan(timestampLength));
            context.Metadata["Body"] = content;

            Span<byte> signatureSpan = stackalloc byte[64];
            FromHex(signature[0], signatureSpan);
            return Ed25519.Verify(signatureSpan, message, _publicKey)
                ? Task.FromResult(Result.Ok<HyperStatus>(default!))
                : Task.FromResult(Result.Ok(new HyperStatus(HttpStatusCode.Unauthorized, new(), new Error("Invalid authentication headers"))));
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
