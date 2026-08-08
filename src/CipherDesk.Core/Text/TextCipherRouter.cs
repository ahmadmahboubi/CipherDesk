using System;
using System.Collections.Generic;
using CipherDesk.Core.Abstractions;

namespace CipherDesk.Core.Text;

/// <summary>
/// Central router for all text encryption and decryption operations.
///
/// The router is responsible only for selecting the appropriate cipher
/// implementation. It does not contain cryptographic logic.
///
/// Supported formats:
/// - Modern: AES-256-GCM authenticated encryption.
/// - Legacy: the original v1 encryption format retained for backward compatibility.
/// - CBC: the AES-256-CBC format compatible with the original
///   implementation.
///
/// Explicitly selected formats are always honored. Auto detection is used
/// only when the caller explicitly passes <see cref="CipherFormat.Auto"/>.
/// </summary>
public sealed class TextCipherRouter
{
    private readonly IReadOnlyDictionary<CipherFormat, ITextCipher> _ciphers;

    /// <summary>
    /// Creates a router using the default cipher implementations.
    /// </summary>
    public TextCipherRouter()
        : this(
            new ModernTextCipher(),
            new LegacyTextCipher(),
            new CBCTextCipher())
    {
    }

    /// <summary>
    /// Creates a router using the supplied cipher implementations.
    /// </summary>
    public TextCipherRouter(
        ITextCipher modern,
        ITextCipher legacy,
        ITextCipher cbc)
    {
        ArgumentNullException.ThrowIfNull(modern);
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(cbc);

        _ciphers = new Dictionary<CipherFormat, ITextCipher>
        {
            [CipherFormat.Modern] = modern,
            [CipherFormat.Legacy] = legacy,
            [CipherFormat.CBC] = cbc
        };
    }

    /// <summary>
    /// Encrypts plaintext using the explicitly requested format.
    ///
    /// Auto always resolves to Modern for encryption. Encryption must never
    /// silently select Legacy or CBC because the caller did not provide
    /// enough information to justify using those formats.
    /// </summary>
    public string Encrypt(
        string plainText,
        ReadOnlySpan<char> password,
        CipherFormat format = CipherFormat.Modern)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        CipherFormat effectiveFormat = format == CipherFormat.Auto
            ? CipherFormat.Modern
            : format;

        ITextCipher cipher = GetCipher(effectiveFormat);

        return cipher.Encrypt(
            plainText,
            password);
    }

    /// <summary>
    /// Decrypts ciphertext using the requested format.
    ///
    /// If a concrete format is supplied, that format is used directly.
    /// Detect() is never called for Modern, Legacy, or CBC.
    ///
    /// Auto is the only mode that performs payload detection.
    /// </summary>
    public string Decrypt(
        string cipherText,
        ReadOnlySpan<char> password,
        CipherFormat format = CipherFormat.Auto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        if (format != CipherFormat.Auto)
        {
            ITextCipher cipher = GetCipher(format);

            return cipher.Decrypt(
                cipherText,
                password);
        }

        CipherFormat detectedFormat = Detect(cipherText);

        return GetCipher(detectedFormat)
            .Decrypt(cipherText, password);
    }

    /// <summary>
    /// Detects the format of an encrypted payload.
    ///
    /// Modern has a recognizable signature and can therefore be detected
    /// reliably.
    ///
    /// Legacy and CBC currently use payload formats that cannot be
    /// reliably distinguished from each other without attempting decryption.
    ///
    /// Therefore Auto detection falls back to Legacy.
    ///
    /// CBC data must be decrypted by explicitly selecting
    /// <see cref="CipherFormat.CBC"/>.
    /// </summary>
    public CipherFormat Detect(string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        ITextCipher modern = GetCipher(CipherFormat.Modern);

        if (modern.CanDecrypt(cipherText))
            return CipherFormat.Modern;

        return CipherFormat.Legacy;
    }

    /// <summary>
    /// Gets the cipher implementation associated with the requested format.
    /// </summary>
    private ITextCipher GetCipher(CipherFormat format)
    {
        if (format == CipherFormat.Auto)
            throw new ArgumentException(
                "Auto is not a concrete cipher format.",
                nameof(format));

        if (!_ciphers.TryGetValue(format, out ITextCipher? cipher))
        {
            throw new NotSupportedException(
                $"The cipher format '{format}' is not supported.");
        }

        return cipher;
    }
}
