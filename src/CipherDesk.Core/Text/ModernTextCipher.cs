using System;
using System.Security.Cryptography;
using System.Text;
using CipherDesk.Core.Abstractions;
using CipherDesk.Core.Internal;

namespace CipherDesk.Core.Text;

/// <summary>
/// The default text cipher: AES-256-GCM with a PBKDF2-HMAC-SHA256 derived key,
/// a random 128-bit salt and a random 96-bit nonce, wrapped in a versioned, authenticated header.
/// </summary>
/// <remarks>
/// Layout: <c>header(32) || nonce(12) || ciphertext(n) || tag(16)</c>, Base64 encoded.
/// The header is passed to GCM as associated data, so the salt, iteration count and version
/// are covered by the authentication tag and cannot be downgraded by an attacker.
/// </remarks>
public sealed class ModernTextCipher : ITextCipher
{
    internal const int NonceSize = 12; // 96 bits: the size AES-GCM is specified for.
    internal const int TagSize = 16;   // 128-bit tag, the maximum GCM allows.

    private readonly int _iterations;

    public ModernTextCipher() : this(KeyDerivation.DefaultIterations) { }

    /// <param name="iterations">PBKDF2 work factor. Stored in the header so old payloads keep working.</param>
    public ModernTextCipher(int iterations)
    {
        if (iterations < 1_000)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Refusing to derive keys with a trivial work factor.");
        _iterations = iterations;
    }

    public CipherFormat Format => CipherFormat.Modern;

    public bool CanDecrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText)) return false;

        // Only the first few bytes are needed to see the signature, so decode a short prefix.
        string trimmed = cipherText.Trim();
        int prefixLength = Math.Min(trimmed.Length - (trimmed.Length % 4), 8);
        if (prefixLength < 8) return false;

        Span<byte> probe = stackalloc byte[6];
        return Convert.TryFromBase64Chars(trimmed.AsSpan(0, prefixLength), probe, out int written)
               && CipherHeader.HasMagic(probe[..written]);
    }

    public string Encrypt(string plainText, ReadOnlySpan<char> password)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        byte[] salt = KeyDerivation.CreateSalt();
        var header = new CipherHeader(CipherHeader.KindText, _iterations, salt, chunkSize: 0);
        byte[] headerBytes = header.ToBytes();

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] output = new byte[CipherHeader.Size + NonceSize + plainBytes.Length + TagSize];

        try
        {
            headerBytes.CopyTo(output, 0);

            Span<byte> nonce = output.AsSpan(CipherHeader.Size, NonceSize);
            RandomNumberGenerator.Fill(nonce);

            Span<byte> cipherSpan = output.AsSpan(CipherHeader.Size + NonceSize, plainBytes.Length);
            Span<byte> tagSpan = output.AsSpan(CipherHeader.Size + NonceSize + plainBytes.Length, TagSize);

            using var key = KeyDerivation.DeriveKey(password, salt, _iterations);
            using var aes = new AesGcm(key.Bytes, TagSize);
            aes.Encrypt(nonce, plainBytes, cipherSpan, tagSpan, headerBytes);

            return Convert.ToBase64String(output);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public string Decrypt(string cipherText, ReadOnlySpan<char> password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(cipherText.Trim());
        }
        catch (FormatException ex)
        {
            throw new MalformedPayloadException("The input is not valid Base64 text.", ex);
        }

        CipherHeader header = CipherHeader.Parse(payload);
        if (header.Kind != CipherHeader.KindText)
            throw new MalformedPayloadException("This payload is an encrypted file, not encrypted text. Use the Files tab.");

        int minimum = CipherHeader.Size + NonceSize + TagSize;
        if (payload.Length < minimum)
            throw new MalformedPayloadException("The payload is truncated.");

        int cipherLength = payload.Length - minimum;
        ReadOnlySpan<byte> headerBytes = payload.AsSpan(0, CipherHeader.Size);
        ReadOnlySpan<byte> nonce = payload.AsSpan(CipherHeader.Size, NonceSize);
        ReadOnlySpan<byte> cipherSpan = payload.AsSpan(CipherHeader.Size + NonceSize, cipherLength);
        ReadOnlySpan<byte> tag = payload.AsSpan(CipherHeader.Size + NonceSize + cipherLength, TagSize);

        byte[] plainBytes = new byte[cipherLength];
        try
        {
            using var key = KeyDerivation.DeriveKey(password, header.Salt, header.Iterations);
            using var aes = new AesGcm(key.Bytes, TagSize);
            aes.Decrypt(nonce, cipherSpan, tag, plainBytes, headerBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new InvalidPasswordException(ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidPasswordException(ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }
}
