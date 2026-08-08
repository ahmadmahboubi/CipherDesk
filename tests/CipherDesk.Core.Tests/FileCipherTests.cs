using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CipherDesk.Core;
using CipherDesk.Core.Files;
using Xunit;

namespace CipherDesk.Core.Tests;

public sealed class FileCipherTests : IDisposable
{
    private readonly FileCipher _cipher =
        new(iterations: 1_000);

    private readonly string _workspace;

    private static readonly char[] Password =
        "correct horse battery staple".ToCharArray();

    public FileCipherTests()
    {
        _workspace =
            Path.Combine(
                Path.GetTempPath(),
                "cipherdesk-tests-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_workspace);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(
                _workspace,
                recursive: true);
        }
        catch (IOException)
        {
            // The OS will clean up the temporary directory eventually.
        }
        catch (UnauthorizedAccessException)
        {
            // The OS may still have a file handle open.
        }
    }

    private string InWorkspace(string name) =>
        Path.Combine(
            _workspace,
            name);

    private string CreateFile(
        string name,
        int sizeInBytes)
    {
        string path =
            InWorkspace(name);

        byte[] content =
            RandomNumberGenerator.GetBytes(
                sizeInBytes);

        File.WriteAllBytes(
            path,
            content);

        return path;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(1024 * 1024 + 1)]
    [InlineData(3 * 1024 * 1024 + 77)]
    public async Task Round_trip_reproduces_the_file_exactly(
        int size)
    {
        string source =
            CreateFile(
                "input.bin",
                size);

        string encrypted =
            InWorkspace(
                "input.bin.cdsk");

        string decrypted =
            InWorkspace(
                "output.bin");

        await _cipher.EncryptAsync(
            source,
            encrypted,
            Password);

        await _cipher.DecryptAsync(
            encrypted,
            decrypted,
            Password);

        Assert.Equal(
            File.ReadAllBytes(source),
            File.ReadAllBytes(decrypted));
    }

    [Fact]
    public async Task Encrypted_output_does_not_contain_the_plaintext()
    {
        string source =
            InWorkspace(
                "secret.txt");

        File.WriteAllText(
            source,
            "the quick brown fox jumps over the lazy dog");

        string encrypted =
            InWorkspace(
                "secret.txt.cdsk");

        await _cipher.EncryptAsync(
            source,
            encrypted,
            Password);

        string raw =
            Convert.ToHexString(
                File.ReadAllBytes(encrypted));

        string plainHex =
            Convert.ToHexString(
                File.ReadAllBytes(source));

        Assert.DoesNotContain(
            plainHex,
            raw,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_password_fails_and_leaves_no_output_file()
    {
        string source =
            CreateFile(
                "input.bin",
                4096);

        string encrypted =
            InWorkspace(
                "input.bin.cdsk");

        string decrypted =
            InWorkspace(
                "output.bin");

        await _cipher.EncryptAsync(
            source,
            encrypted,
            Password);

        await Assert.ThrowsAsync<InvalidPasswordException>(
            () => _cipher.DecryptAsync(
                encrypted,
                decrypted,
                "wrong".ToCharArray()));

        // Decryption uses a temporary output file, so a failed operation
        // must never leave a partially decrypted destination behind.
        Assert.False(
            File.Exists(decrypted));
    }

    [Fact]
    public async Task Modifying_a_single_byte_is_detected()
    {
        string source =
            CreateFile(
                "input.bin",
                8192);

        string encrypted =
            InWorkspace(
                "input.bin.cdsk");

        string output =
            InWorkspace(
                "out.bin");

        await _cipher.EncryptAsync(
            source,
            encrypted,
            Password);

        byte[] payload =
            File.ReadAllBytes(
                encrypted);

        payload[^20] ^= 0x01;

        File.WriteAllBytes(
            encrypted,
            payload);

        await Assert.ThrowsAsync<InvalidPasswordException>(
            () => _cipher.DecryptAsync(
                encrypted,
                output,
                Password));

        Assert.False(
            File.Exists(output));
    }

    [Fact]
    public async Task Truncating_the_file_is_detected()
    {
        string source =
            CreateFile(
                "input.bin",
                3 * 1024 * 1024);

        string encrypted =
            InWorkspace(
                "input.bin.cdsk");

        string output =
            InWorkspace(
                "out.bin");

        await _cipher.EncryptAsync(
            source,
            encrypted,
            Password);

        byte[] payload =
            File.ReadAllBytes(
                encrypted);

        File.WriteAllBytes(
            encrypted,
            payload[..(payload.Length / 2)]);

        await Assert.ThrowsAnyAsync<CipherDeskCryptoException>(
            () => _cipher.DecryptAsync(
                encrypted,
                output,
                Password));

        Assert.False(
            File.Exists(output));
    }

    [Fact]
    public async Task A_file_that_is_not_cipherdesk_data_is_reported_clearly()
    {
        string source =
            CreateFile(
                "random.bin",
                2048);

        string output =
            InWorkspace(
                "out.bin");

        await Assert.ThrowsAsync<MalformedPayloadException>(
            () => _cipher.DecryptAsync(
                source,
                output,
                Password));

        Assert.False(
            File.Exists(output));
    }

    [Fact]
    public async Task Progress_is_reported_and_ends_at_one_hundred_percent()
    {
        string source =
            CreateFile(
                "input.bin",
                5 * 1024 * 1024);

        int reports = 0;
        double last = 0;

        // Synchronous progress keeps assertions deterministic.
        // Progress<T> may dispatch callbacks asynchronously.
        var collector =
            new SynchronousProgress(
                progress =>
                {
                    reports++;
                    last = progress.Fraction;
                });

        await _cipher.EncryptAsync(
            source,
            InWorkspace("input.bin.cdsk"),
            Password,
            collector);

        Assert.True(
            reports >= 5);

        Assert.Equal(
            1d,
            last,
            precision: 3);
    }

    [Fact]
    public async Task Cancellation_stops_the_work_and_removes_the_partial_file()
    {
        string source =
            CreateFile(
                "large.bin",
                12 * 1024 * 1024);

        string destination =
            InWorkspace(
                "large.bin.cdsk");

        using var cancellation =
            new CancellationTokenSource();

        var progress =
            new SynchronousProgress(
                value =>
                {
                    if (value.Fraction > 0.2)
                    {
                        cancellation.Cancel();
                    }
                });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _cipher.EncryptAsync(
                source,
                destination,
                Password,
                progress,
                cancellation.Token));

        Assert.False(
            File.Exists(destination));

        Assert.Empty(
            Directory.GetFiles(
                _workspace,
                "*.cdsk-tmp"));
    }

    [Fact]
    public void Suggested_paths_never_overwrite_an_existing_file()
    {
        string source =
            CreateFile(
                "doc.txt",
                16);

        string existing =
            InWorkspace(
                "doc.txt.cdsk");

        File.WriteAllText(
            existing,
            "already here");

        string suggestion =
            FileCipher.SuggestEncryptedPath(
                source);

        Assert.NotEqual(
            existing,
            suggestion);

        Assert.False(
            File.Exists(suggestion));
    }

    [Fact]
    public async Task Encrypting_the_same_file_twice_produces_different_ciphertext()
    {
        string source =
            CreateFile(
                "input.bin",
                64 * 1024);

        string first =
            InWorkspace(
                "first.cdsk");

        string second =
            InWorkspace(
                "second.cdsk");

        await _cipher.EncryptAsync(
            source,
            first,
            Password);

        await _cipher.EncryptAsync(
            source,
            second,
            Password);

        Assert.NotEqual(
            File.ReadAllBytes(first),
            File.ReadAllBytes(second));
    }

    [Fact]
    public async Task Empty_file_round_trips_successfully()
    {
        string source =
            CreateFile(
                "empty.bin",
                0);

        string encrypted =
            InWorkspace(
                "empty.bin.cdsk");

        string decrypted =
            InWorkspace(
                "empty.out");

        await _cipher.EncryptAsync(
            source,
            encrypted,
            Password);

        await _cipher.DecryptAsync(
            encrypted,
            decrypted,
            Password);

        Assert.Empty(
            File.ReadAllBytes(
                decrypted));
    }

    [Fact]
    public async Task Decryption_of_tampered_file_does_not_leave_partial_output()
    {
        string source =
            CreateFile(
                "input.bin",
                1024 * 1024);

        string encrypted =
            InWorkspace(
                "input.bin.cdsk");

        string output =
            InWorkspace(
                "output.bin");

        await _cipher.EncryptAsync(
            source,
            encrypted,
            Password);

        byte[] payload =
            File.ReadAllBytes(
                encrypted);

        payload[payload.Length / 2] ^= 0x80;

        File.WriteAllBytes(
            encrypted,
            payload);

        await Assert.ThrowsAnyAsync<CipherDeskCryptoException>(
            () => _cipher.DecryptAsync(
                encrypted,
                output,
                Password));

        Assert.False(
            File.Exists(output));
    }

    /// <summary>
    /// Reports synchronously on the calling thread,
    /// which keeps progress assertions deterministic.
    /// </summary>
    private sealed class SynchronousProgress
        : IProgress<CryptoProgress>
    {
        private readonly Action<CryptoProgress> _handler;

        public SynchronousProgress(
            Action<CryptoProgress> handler)
        {
            _handler = handler
                ?? throw new ArgumentNullException(
                    nameof(handler));
        }

        public void Report(
            CryptoProgress value)
        {
            _handler(value);
        }
    }
}
