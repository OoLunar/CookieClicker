using System;
using System.Runtime.InteropServices;

namespace OoLunar.CookieClicker
{
    public static partial class Ed25519
    {
        public const int SignatureBytes = 64;
        public const int PublicKeyBytes = 32;

        static Ed25519()
        {
            if (Init() == -1)
            {
                throw new InvalidOperationException("Failed to initialize libsodium.");
            }
        }

        public static unsafe bool Verify(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> message, ReadOnlySpan<byte> publicKey)
        {
            if (signature.Length != SignatureBytes)
            {
                throw new ArgumentException($"Signature must be {SignatureBytes} bytes in length.", nameof(signature));
            }

            if (publicKey.Length != PublicKeyBytes)
            {
                throw new ArgumentException($"Public key must be {PublicKeyBytes} bytes in length.", nameof(publicKey));
            }

            fixed (byte* signaturePtr = signature)
            fixed (byte* messagePtr = message)
            fixed (byte* publicKeyPtr = publicKey)
            {
                return VerifyDetached(signaturePtr, messagePtr, (ulong)message.Length, publicKeyPtr) == 0;
            }
        }

        [LibraryImport("libsodium", EntryPoint = "sodium_init")]
        private static unsafe partial int Init();

        [LibraryImport("libsodium", EntryPoint = "crypto_sign_ed25519_verify_detached")]
        private static unsafe partial int VerifyDetached(byte* signature, byte* message, ulong messageLength, byte* publicKey);
    }
}
