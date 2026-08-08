using System;

namespace CipherDesk.Core.Abstractions;

/// <summary>
/// Encrypts and decrypts short, in-memory strings.
/// </summary>
/// <remarks>
/// The password is taken as a <see cref="ReadOnlySpan{T}"/> of characters rather than a
/// <see cref="string"/> so callers can hold it in a buffer they are able to wipe afterwards.
/// </remarks>
public interface ITextCipher
{
    /// <summary>The container format this implementation produces and consumes.</summary>
    CipherFormat Format { get; }

    /// <summary>Encrypts <paramref name="plainText"/> and returns a Base64 container.</summary>
    string Encrypt(string plainText, ReadOnlySpan<char> password);

    /// <summary>Decrypts a Base64 container produced by <see cref="Encrypt"/>.</summary>
    string Decrypt(string cipherText, ReadOnlySpan<char> password);

    /// <summary>Returns true when <paramref name="cipherText"/> looks like this implementation's format.</summary>
    bool CanDecrypt(string cipherText);
}
