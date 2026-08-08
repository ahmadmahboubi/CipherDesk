namespace CipherDesk.Core;

/// <summary>
/// Identifies the on-the-wire container format used for a payload.
/// </summary>
public enum CipherFormat
{
    /// <summary>Detect the format from the payload itself. Only valid for decryption.</summary>
    Auto = 0,

    /// <summary>
    /// CipherDesk v2: AES-256-GCM, PBKDF2-HMAC-SHA256 key derivation, random salt and nonce.
    /// This is the default for anything newly encrypted.
    /// </summary>
    Modern = 1,

    /// <summary>
    /// The original v1 format: AES-256-CBC with an all-zero IV and an unsalted, unstretched key.
    /// Retained purely so existing ciphertext keeps working. Cryptographically weak - see docs/FILE-FORMAT.md.
    /// </summary>
    Legacy = 2,

    /// <summary>
    /// The legacy AES-256-CBC format: Rijndael-256-CBC with a fixed IV and an
    /// unsalted key derived using the legacy PasswordDeriveBytes algorithm.
    /// Retained purely for backward compatibility with ciphertext produced by
    /// the original AES-256-CBC implementation.
    /// Cryptographically weak - see docs/FILE-FORMAT.md.
    /// </summary>
    CBC = 3
}
