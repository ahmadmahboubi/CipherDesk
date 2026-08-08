using System;
using System.Security.Cryptography;
using System.Text;
using CipherDesk.Core.Abstractions;
using CipherDesk.Core.Internal;

namespace CipherDesk.Core.Text;

/// <summary>
/// Compatibility implementation for the encryption algorithm used by the
/// original EncryptString / DecryptString methods.
///
/// This implementation intentionally preserves the original cryptographic
/// behaviour so ciphertext produced by the legacy application can still be
/// decrypted.
///
/// Legacy algorithm:
/// - RijndaelManaged
/// - CBC mode
/// - 256-bit key
/// - 128-bit block size
/// - PKCS#7 padding
/// - Fixed IV: "hamed1476u@qwxcT"
/// - Key derivation: PasswordDeriveBytes(password, null).GetBytes(32)
/// - UTF-8 plaintext encoding
/// - Base64 ciphertext representation
///
/// WARNING:
/// This algorithm is cryptographically obsolete and must not be used for
/// new encrypted data. It has no authentication tag, uses a fixed IV,
/// and uses the legacy PasswordDeriveBytes KDF without a salt.
/// </summary>
public sealed class CBCTextCipher : ITextCipher
{
    private const int KeySize = 256;
    private const int KeySizeBytes = KeySize / 8;

    private const string InitVector = "hamed1476u@qwxcT";

    private static readonly byte[] InitVectorBytes =
        Encoding.UTF8.GetBytes(InitVector);

    public CipherFormat Format => CipherFormat.Legacy;

    /// <summary>
    /// Determines whether the supplied text looks like a ciphertext
    /// produced by the legacy algorithm.
    ///
    /// Since the original format has no magic/header/version marker,
    /// this can only perform a structural Base64/block-size check.
    /// </summary>
    public bool CanDecrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
            return false;

        try
        {
            string normalized = NormalizeCipherText(cipherText);

            byte[] cipherBytes = Convert.FromBase64String(normalized);

            return cipherBytes.Length > 0 &&
                   cipherBytes.Length % 16 == 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Encrypts plaintext using the exact legacy algorithm.
    /// </summary>
    public string Encrypt(
        string plainText,
        ReadOnlySpan<char> password)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        byte[] plainTextBytes =
            Encoding.UTF8.GetBytes(plainText);

        try
        {
            using var key = DeriveKey(password);

            using RijndaelManaged symmetricKey = CreateCipher();

            using ICryptoTransform encryptor =
                symmetricKey.CreateEncryptor(
                    key.Bytes,
                    InitVectorBytes);

            byte[] cipherTextBytes =
                encryptor.TransformFinalBlock(
                    plainTextBytes,
                    0,
                    plainTextBytes.Length);

            try
            {
                return Convert.ToBase64String(cipherTextBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(cipherTextBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainTextBytes);
        }
    }

    /// <summary>
    /// Decrypts ciphertext produced by the original
    /// DecryptString implementation.
    /// </summary>
    public string Decrypt(
        string cipherText,
        ReadOnlySpan<char> password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        string normalizedCipherText =
            NormalizeCipherText(cipherText);

        byte[] cipherTextBytes;

        try
        {
            cipherTextBytes =
                Convert.FromBase64String(normalizedCipherText);
        }
        catch (FormatException ex)
        {
            throw new MalformedPayloadException(
                "The input is not valid Base64 text.",
                ex);
        }

        if (cipherTextBytes.Length == 0 ||
            cipherTextBytes.Length % 16 != 0)
        {
            CryptographicOperations.ZeroMemory(cipherTextBytes);

            throw new MalformedPayloadException(
                "The input is not a whole number of AES/Rijndael blocks, " +
                "so it is truncated or was never encrypted.");
        }

        using var key = DeriveKey(password);

        byte[]? plainTextBytes = null;

        try
        {
            using RijndaelManaged symmetricKey =
                CreateCipher();

            using ICryptoTransform decryptor =
                symmetricKey.CreateDecryptor(
                    key.Bytes,
                    InitVectorBytes);

            plainTextBytes =
                decryptor.TransformFinalBlock(
                    cipherTextBytes,
                    0,
                    cipherTextBytes.Length);

            return Encoding.UTF8.GetString(
                plainTextBytes);
        }
        catch (CryptographicException ex)
        {
            // Wrong passwords normally surface as a padding error.
            throw new InvalidPasswordException(ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                cipherTextBytes);

            if (plainTextBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    plainTextBytes);
            }
        }
    }

    /// <summary>
    /// Creates the exact cipher configuration used by the original code:
    ///
    /// RijndaelManaged
    /// Mode = CBC
    /// BlockSize = 128
    /// KeySize = 256
    /// Padding = PKCS7
    /// </summary>
    private static RijndaelManaged CreateCipher()
    {
        var symmetricKey = new RijndaelManaged
        {
            BlockSize = 128,
            KeySize = KeySize,
            Mode = CipherMode.CBC,
            Padding = PaddingMode.PKCS7
        };

        return symmetricKey;
    }

    /// <summary>
    /// Reproduces:
    ///
    /// PasswordDeriveBytes password =
    ///     new PasswordDeriveBytes(passPhrase, null);
    ///
    /// byte[] keyBytes =
    ///     password.GetBytes(256 / 8);
    ///
    /// The salt is intentionally null because that is what the
    /// original implementation used.
    /// </summary>
    private static SecureBuffer DeriveKey(
        ReadOnlySpan<char> password)
    {
        string passwordString = password.ToString();

        try
        {
#pragma warning disable SYSLIB0041
            using var passwordDeriveBytes =
                new PasswordDeriveBytes(
                    passwordString,
                    null);
#pragma warning restore SYSLIB0041

            byte[] keyBytes =
                passwordDeriveBytes.GetBytes(KeySizeBytes);

            var secureKey =
                new SecureBuffer(KeySizeBytes);

            keyBytes.CopyTo(secureKey.Span);

            CryptographicOperations.ZeroMemory(
                keyBytes);

            return secureKey;
        }
        finally
        {
            passwordString = string.Empty;
        }
    }

    /// <summary>
    /// The original DecryptString implementation did:
    ///
    /// cipherText.Replace("\"", string.Empty)
    ///
    /// Preserve that behaviour for compatibility with values that may
    /// have been stored as quoted JSON strings.
    /// </summary>
    private static string NormalizeCipherText(
        string cipherText)
    {
        return cipherText
            .Trim()
            .Replace("\"", string.Empty);
    }
}
