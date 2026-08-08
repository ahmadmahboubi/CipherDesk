using System;
using System.Security.Cryptography;

namespace CipherDesk.Core.Internal;

/// <summary>
/// Password-based key derivation for the v2 format.
/// </summary>
internal static class KeyDerivation
{
    /// <summary>AES-256 key length in bytes.</summary>
    public const int KeySize = 32;

    /// <summary>
    /// PBKDF2-HMAC-SHA256 iteration count. Matches the OWASP recommendation at the time of writing.
    /// It is stored in the header, so raising it later stays backwards compatible.
    /// </summary>
    public const int DefaultIterations = 210_000;

    /// <summary>Derives an AES-256 key. The caller owns - and must dispose - the returned buffer.</summary>
    public static SecureBuffer DeriveKey(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt, int iterations)
    {
        // The span overload avoids materialising the password as an interned, immovable string.
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
        return new SecureBuffer(key);
    }

    public static byte[] CreateSalt() => RandomNumberGenerator.GetBytes(CipherHeader.SaltSize);
}
