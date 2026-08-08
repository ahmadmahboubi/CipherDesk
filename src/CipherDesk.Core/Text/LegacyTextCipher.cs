using System;
using System.Security.Cryptography;
using System.Text;
using CipherDesk.Core.Abstractions;
using CipherDesk.Core.Internal;

namespace CipherDesk.Core.Text;

/// <summary>
/// Bit-for-bit reimplementation of the original CipherDesk (v1) algorithm, kept so that
/// ciphertext produced by earlier releases can still be read.
/// </summary>
/// <remarks>
/// <para>
/// The v1 scheme is <b>not secure</b> and is deliberately excluded from the default encryption path:
/// </para>
/// <list type="bullet">
///   <item><description>The "key" is the Base64 of the password padded with the digits of 0, 1, 2, ...
///   That is an encoding, not a hash - anyone holding the key can Base64-decode it back to the password,
///   and a weak password yields a correspondingly weak key with no stretching at all.</description></item>
///   <item><description>The IV is all zeros, so identical plaintext under the same password always
///   produces identical ciphertext, and equal block prefixes are visible to an observer.</description></item>
///   <item><description>There is no salt, so one dictionary works against every message ever produced.</description></item>
///   <item><description>CBC without a MAC is unauthenticated: ciphertext can be altered undetectably
///   and is exposed to padding-oracle attacks.</description></item>
/// </list>
/// <para>
/// Behaviour is preserved exactly, including the UTF-8 (no BOM) text encoding and PKCS#7 padding.
/// Verified against golden vectors in <c>LegacyCompatibilityTests</c>.
/// </para>
/// </remarks>
public sealed class LegacyTextCipher : ITextCipher
{
    private const int KeyLength = 32;

    /// <summary>
    /// The largest password v1 can represent. Base64 expands 25 bytes to 36 characters, which is
    /// not a legal AES key length - the original build threw a raw CryptographicException here.
    /// </summary>
    public const int MaxPasswordBytes = 24;

    public CipherFormat Format => CipherFormat.Legacy;

    public bool CanDecrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText)) return false;

        // Legacy payloads are raw Base64 of a whole number of AES blocks and carry no signature,
        // so "decodes as Base64 and is block aligned" is the strongest test available.
        try
        {
            byte[] decoded = Convert.FromBase64String(cipherText.Trim());
            return decoded.Length > 0 && decoded.Length % 16 == 0 && !CipherHeader.HasMagic(decoded);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public string Encrypt(string plainText, ReadOnlySpan<char> password)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        using var key = DeriveLegacyKey(password);
        // StreamWriter's default encoding in the original implementation was UTF-8 without a BOM.
        byte[] plainBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(plainText);

        try
        {
            using Aes aes = CreateAes(key.Bytes);
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public string Decrypt(string cipherText, ReadOnlySpan<char> password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        byte[] cipherBytes;
        try
        {
            cipherBytes = Convert.FromBase64String(cipherText.Trim());
        }
        catch (FormatException ex)
        {
            throw new MalformedPayloadException("The input is not valid Base64 text.", ex);
        }

        if (cipherBytes.Length == 0 || cipherBytes.Length % 16 != 0)
            throw new MalformedPayloadException(
                "The input is not a whole number of AES blocks, so it is truncated or was never encrypted.");

        using var key = DeriveLegacyKey(password);
        byte[]? plainBytes = null;

        try
        {
            using Aes aes = CreateAes(key.Bytes);
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            // StreamReader stripped a UTF-8 BOM if one was present; keep that behaviour.
            ReadOnlySpan<byte> text = plainBytes;
            if (text.Length >= 3 && text[0] == 0xEF && text[1] == 0xBB && text[2] == 0xBF)
                text = text[3..];

            return Encoding.UTF8.GetString(text);
        }
        catch (CryptographicException ex)
        {
            // v1 has no authentication tag, so a bad password usually surfaces as a padding error.
            throw new InvalidPasswordException(ex);
        }
        finally
        {
            if (plainBytes is not null) CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    private static Aes CreateAes(byte[] key)
    {
        Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = new byte[16]; // v1 used a zero IV; required for compatibility.
        return aes;
    }

    /// <summary>
    /// Reproduces v1 key generation: Base64 the UTF-8 password, then append 0, 1, 2, ...
    /// until the string reaches 32 characters, and use those characters' ASCII bytes as the key.
    /// </summary>
    private static SecureBuffer DeriveLegacyKey(ReadOnlySpan<char> password)
    {
        int passwordBytes = Encoding.UTF8.GetByteCount(password);
        if (passwordBytes > MaxPasswordBytes)
        {
            throw new UnsupportedOperationException(
                $"Legacy (v1) mode supports passwords up to {MaxPasswordBytes} bytes; this one is {passwordBytes}. " +
                "Shorten the password, or switch to the modern format, which has no such limit.");
        }

        using var passwordBuffer = new SecureBuffer(passwordBytes);
        Encoding.UTF8.GetBytes(password, passwordBuffer.Span);

        // 32 is the target; the small headroom guards the padding loop against an overshoot.
        Span<char> keyChars = stackalloc char[KeyLength + 8];
        if (!Convert.TryToBase64Chars(passwordBuffer.Span, keyChars, out int length))
            throw new UnsupportedOperationException("The password could not be encoded for legacy mode.");

        for (int counter = 0; length < KeyLength; counter++)
        {
            foreach (char digit in counter.ToString(System.Globalization.CultureInfo.InvariantCulture))
                keyChars[length++] = digit;
        }

        if (length != KeyLength)
            throw new UnsupportedOperationException("The password cannot be expressed as a legacy 256-bit key.");

        var key = new SecureBuffer(KeyLength);
        Encoding.ASCII.GetBytes(keyChars[..KeyLength], key.Span); // Base64 and digits are ASCII by definition.
        keyChars.Clear();
        return key;
    }
}
