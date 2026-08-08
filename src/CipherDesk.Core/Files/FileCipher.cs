using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CipherDesk.Core.Abstractions;
using CipherDesk.Core.Internal;

namespace CipherDesk.Core.Files;

/// <summary>
/// Streaming file encryption using AES-256-GCM over independently authenticated chunks.
/// </summary>
/// <remarks>
/// <para>
/// GCM is not safe to use on a single unbounded stream, because nothing can be trusted until the
/// tag at the very end has been verified - which would mean buffering the whole file. Instead the
/// plaintext is split into fixed-size chunks, each sealed with its own nonce and tag.
/// </para>
/// <para>
/// Each chunk's associated data covers the file header, the nonce prefix, the chunk index and a
/// final-chunk flag. That binds chunks to their position and to this specific file, which defeats
/// reordering, splicing between files, duplication and truncation.
/// </para>
/// <para>Memory use is bounded by <see cref="ChunkSize"/> regardless of file size.</para>
/// </remarks>
public sealed class FileCipher : IFileCipher
{
    /// <summary>File extension used for encrypted output.</summary>
    public const string EncryptedExtension = ".cdsk";

    /// <summary>Plaintext bytes per chunk. 1 MiB balances syscall overhead against memory use.</summary>
    public const int ChunkSize = 1024 * 1024;

    private const int NoncePrefixSize = 8;
    private const int CounterSize = 4;
    private const int NonceSize = NoncePrefixSize + CounterSize; // 12 bytes, as AES-GCM expects
    private const int TagSize = 16;
    private const int ChunkHeaderSize = 5; // uint32 length + 1 flag byte

    private readonly int _iterations;

    public FileCipher() : this(KeyDerivation.DefaultIterations) { }

    public FileCipher(int iterations) => _iterations = iterations;

    /// <summary>Suggests a destination path for encryption, without overwriting an existing file.</summary>
    public static string SuggestEncryptedPath(string sourcePath) =>
        MakeUnique(sourcePath + EncryptedExtension);

    /// <summary>Suggests a destination path for decryption, without overwriting an existing file.</summary>
    public static string SuggestDecryptedPath(string sourcePath)
    {
        string candidate = sourcePath.EndsWith(EncryptedExtension, StringComparison.OrdinalIgnoreCase)
            ? sourcePath[..^EncryptedExtension.Length]
            : Path.Combine(
                Path.GetDirectoryName(sourcePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(sourcePath) + "-decrypted" + Path.GetExtension(sourcePath));

        return MakeUnique(candidate);
    }

    private static string MakeUnique(string path)
    {
        if (!File.Exists(path)) return path;

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        for (int i = 2; i < 1000; i++)
        {
            string candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        return path;
    }

    public async Task EncryptAsync(
        string sourcePath,
        string destinationPath,
        char[] password,
        IProgress<CryptoProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = KeyDerivation.CreateSalt();
        byte[] headerBytes = new CipherHeader(CipherHeader.KindStream, _iterations, salt, ChunkSize).ToBytes();
        byte[] noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixSize);

        using SecureBuffer key = KeyDerivation.DeriveKey(password, salt, _iterations);
        using var aes = new AesGcm(key.Bytes, TagSize);

        await RunAtomicallyAsync(destinationPath, async destination =>
        {
            await using FileStream source = OpenRead(sourcePath);
            long total = source.Length;

            await destination.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
            await destination.WriteAsync(noncePrefix, cancellationToken).ConfigureAwait(false);

            byte[] plainBuffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            byte[] cipherBuffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            byte[] tag = new byte[TagSize];
            byte[] nonce = new byte[NonceSize];
            byte[] chunkHeader = new byte[ChunkHeaderSize];
            byte[] associatedData = new byte[CipherHeader.Size + NoncePrefixSize + CounterSize + 1];

            try
            {
                noncePrefix.CopyTo(nonce, 0);
                long processed = 0;
                int counter = 0;
                bool isFinal = false;

                while (!isFinal)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int read = await ReadChunkAsync(source, plainBuffer, cancellationToken).ConfigureAwait(false);
                    processed += read;
                    isFinal = processed >= total || read == 0;

                    BinaryPrimitives.WriteInt32BigEndian(nonce.AsSpan(NoncePrefixSize), counter);
                    BuildAssociatedData(associatedData, headerBytes, noncePrefix, counter, isFinal);
                    SealChunk(aes, nonce, plainBuffer, read, cipherBuffer, tag, associatedData);

                    BinaryPrimitives.WriteInt32BigEndian(chunkHeader, read);
                    chunkHeader[4] = isFinal ? (byte)1 : (byte)0;

                    await destination.WriteAsync(chunkHeader, cancellationToken).ConfigureAwait(false);
                    await destination.WriteAsync(cipherBuffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    await destination.WriteAsync(tag, cancellationToken).ConfigureAwait(false);

                    progress?.Report(new CryptoProgress(processed, total));
                    counter++;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBuffer.AsSpan(0, ChunkSize));
                ArrayPool<byte>.Shared.Return(plainBuffer);
                ArrayPool<byte>.Shared.Return(cipherBuffer);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DecryptAsync(
        string sourcePath,
        string destinationPath,
        char[] password,
        IProgress<CryptoProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(password);

        await using FileStream source = OpenRead(sourcePath);
        long total = source.Length;

        byte[] headerBytes = new byte[CipherHeader.Size];
        await ReadExactlyAsync(source, headerBytes, "file header", cancellationToken).ConfigureAwait(false);

        CipherHeader header = CipherHeader.Parse(headerBytes);
        if (header.Kind != CipherHeader.KindStream)
            throw new MalformedPayloadException("This file holds encrypted text, not an encrypted file. Use the Text tab.");
        if (header.ChunkSize <= 0 || header.ChunkSize > 64 * 1024 * 1024)
            throw new MalformedPayloadException("The file declares an unusable chunk size.");

        byte[] noncePrefix = new byte[NoncePrefixSize];
        await ReadExactlyAsync(source, noncePrefix, "nonce prefix", cancellationToken).ConfigureAwait(false);

        using SecureBuffer key = KeyDerivation.DeriveKey(password, header.Salt, header.Iterations);
        using var aes = new AesGcm(key.Bytes, TagSize);

        await RunAtomicallyAsync(destinationPath, async destination =>
        {
            byte[] cipherBuffer = ArrayPool<byte>.Shared.Rent(header.ChunkSize);
            byte[] plainBuffer = ArrayPool<byte>.Shared.Rent(header.ChunkSize);
            byte[] tag = new byte[TagSize];
            byte[] nonce = new byte[NonceSize];
            byte[] chunkHeader = new byte[ChunkHeaderSize];
            byte[] associatedData = new byte[CipherHeader.Size + NoncePrefixSize + CounterSize + 1];

            try
            {
                noncePrefix.CopyTo(nonce, 0);
                int counter = 0;
                bool isFinal = false;

                while (!isFinal)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await ReadExactlyAsync(source, chunkHeader, "chunk header", cancellationToken).ConfigureAwait(false);
                    int length = BinaryPrimitives.ReadInt32BigEndian(chunkHeader);
                    isFinal = chunkHeader[4] == 1;

                    if (length < 0 || length > header.ChunkSize)
                        throw new MalformedPayloadException("The file declares an invalid chunk length.");

                    await ReadExactlyAsync(source, cipherBuffer.AsMemory(0, length), "chunk body", cancellationToken).ConfigureAwait(false);
                    await ReadExactlyAsync(source, tag, "chunk tag", cancellationToken).ConfigureAwait(false);

                    BinaryPrimitives.WriteInt32BigEndian(nonce.AsSpan(NoncePrefixSize), counter);
                    BuildAssociatedData(associatedData, headerBytes, noncePrefix, counter, isFinal);
                    OpenChunk(aes, nonce, cipherBuffer, length, tag, plainBuffer, associatedData);

                    await destination.WriteAsync(plainBuffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);

                    progress?.Report(new CryptoProgress(Math.Min(source.Position, total), total));
                    counter++;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBuffer.AsSpan(0, header.ChunkSize));
                ArrayPool<byte>.Shared.Return(plainBuffer);
                ArrayPool<byte>.Shared.Return(cipherBuffer);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    // ---- helpers -------------------------------------------------------------------------

    /// <summary>
    /// Runs the body against a temporary file and moves it into place only on success, so a
    /// cancelled, failed or wrong-password run never leaves a half-written file behind.
    /// </summary>
    private static async Task RunAtomicallyAsync(
        string destinationPath,
        Func<FileStream, Task> body,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath)) ?? ".";
        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, Path.GetRandomFileName() + ".cdsk-tmp");

        try
        {
            await using (FileStream destination = new(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await body(destination).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, destinationPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* best effort - never mask the original failure */ }
        catch (UnauthorizedAccessException) { /* ditto */ }
    }

    private static FileStream OpenRead(string path) => new(
        path, FileMode.Open, FileAccess.Read, FileShare.Read,
        bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static async Task<int> ReadChunkAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int filled = 0;
        while (filled < ChunkSize)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(filled, ChunkSize - filled), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            filled += read;
        }
        return filled;
    }

    private static Task ReadExactlyAsync(Stream stream, byte[] buffer, string what, CancellationToken cancellationToken) =>
        ReadExactlyAsync(stream, buffer.AsMemory(), what, cancellationToken);

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, string what, CancellationToken cancellationToken)
    {
        int filled = 0;
        while (filled < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new MalformedPayloadException($"The file ends unexpectedly while reading the {what}; it is truncated or corrupt.");
            filled += read;
        }
    }

    /// <summary>Binds a chunk to this file, this position and its final-or-not status.</summary>
    private static void BuildAssociatedData(byte[] destination, byte[] header, byte[] noncePrefix, int counter, bool isFinal)
    {
        header.CopyTo(destination, 0);
        noncePrefix.CopyTo(destination, CipherHeader.Size);
        BinaryPrimitives.WriteInt32BigEndian(destination.AsSpan(CipherHeader.Size + NoncePrefixSize, CounterSize), counter);
        destination[^1] = isFinal ? (byte)1 : (byte)0;
    }

    // Span locals are illegal in async methods, so the AES-GCM calls live in synchronous helpers.

    private static void SealChunk(AesGcm aes, byte[] nonce, byte[] plain, int count, byte[] cipher, byte[] tag, byte[] associatedData) =>
        aes.Encrypt(nonce, plain.AsSpan(0, count), cipher.AsSpan(0, count), tag, associatedData);

    private static void OpenChunk(AesGcm aes, byte[] nonce, byte[] cipher, int count, byte[] tag, byte[] plain, byte[] associatedData)
    {
        try
        {
            aes.Decrypt(nonce, cipher.AsSpan(0, count), tag, plain.AsSpan(0, count), associatedData);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new InvalidPasswordException(ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidPasswordException(ex);
        }
    }
}
