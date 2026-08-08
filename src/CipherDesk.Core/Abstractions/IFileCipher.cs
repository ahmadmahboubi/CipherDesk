using System;
using System.Threading;
using System.Threading.Tasks;
using CipherDesk.Core.Files;

namespace CipherDesk.Core.Abstractions;

/// <summary>
/// Encrypts and decrypts files of arbitrary size by streaming them in authenticated chunks,
/// so memory use stays flat regardless of file size.
/// </summary>
public interface IFileCipher
{
    Task EncryptAsync(
        string sourcePath,
        string destinationPath,
        char[] password,
        IProgress<CryptoProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task DecryptAsync(
        string sourcePath,
        string destinationPath,
        char[] password,
        IProgress<CryptoProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
