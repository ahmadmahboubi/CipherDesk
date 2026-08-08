using System;
using System.Security.Cryptography;
using System.Text;
using CipherDesk.Core;
using CipherDesk.Core.Text;
using Xunit;

namespace CipherDesk.Core.Tests;

/// <summary>
/// Guards backwards compatibility with the original (v1) implementation.
/// </summary>
/// <remarks>
/// The expected ciphertexts below were produced from an independent reference implementation of
/// the original algorithm, not from this code. That is what makes them meaningful: if a refactor
/// ever changes the derived key, the padding, the IV or the text encoding, these fail immediately.
/// Do not regenerate them from CipherDesk output.
/// </remarks>
public sealed class LegacyCompatibilityTests
{
    private readonly LegacyTextCipher _cipher = new();

    [Theory]
    [InlineData("Hello, World!", "pass", "oHhvi8KhmFVLTij9baGFyg==")]
    [InlineData("The quick brown fox jumps over the lazy dog", "s3cret",
        "3Nmce4CAfh/Ji1vphgAWOsg+DyFYXdOrgMRJV3dh9p3T2c7PJFN25Jdz8TDgr5nn")]
    [InlineData("", "p", "ByFBcGm0O6oZ8pI0DePyew==")]
    [InlineData("a", "a", "zv150ud94HLoGRSrxmr1CQ==")]
    [InlineData("Gr\u00FC\u00DFe aus Berlin \u2013 \u00FCn\u00EFc\u00F6d\u00E9 \u2713 \u65E5\u672C\u8A9E", "P\u00E4ssw0rd!",
        "a1S3/FcbzFW4kAsHvGZdNFra8lSkWZZQcQ8twgDnox3oYa7tBlN0ViccPpDF05EHS9FGQ/IiwZY0RC9cLiv2Hw==")]
    public void Encrypt_matches_the_original_implementation_byte_for_byte(string plainText, string password, string expected)
    {
        Assert.Equal(expected, _cipher.Encrypt(plainText, password));
    }

    [Theory]
    [InlineData("Hello, World!", "pass", "oHhvi8KhmFVLTij9baGFyg==")]
    [InlineData("", "p", "ByFBcGm0O6oZ8pI0DePyew==")]
    [InlineData("Gr\u00FC\u00DFe aus Berlin \u2013 \u00FCn\u00EFc\u00F6d\u00E9 \u2713 \u65E5\u672C\u8A9E", "P\u00E4ssw0rd!",
        "a1S3/FcbzFW4kAsHvGZdNFra8lSkWZZQcQ8twgDnox3oYa7tBlN0ViccPpDF05EHS9FGQ/IiwZY0RC9cLiv2Hw==")]
    public void Decrypt_reads_ciphertext_written_by_the_original(string expected, string password, string cipherText)
    {
        Assert.Equal(expected, _cipher.Decrypt(cipherText, password));
    }

    [Fact]
    public void Long_input_matches_the_original_implementation()
    {
        // A 1000 character payload spans many blocks, which is where a padding or streaming
        // mistake would show up. Compared by hash to keep the vector readable.
        string cipherText = _cipher.Encrypt(new string('x', 1000), "LongPasswordUpTo24Bytes!");
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cipherText))).ToLowerInvariant();

        Assert.Equal("98dc47998633dbdd00a92c6e4e6031f0a9c58253d9dd4558990fb3c8c803a050", hash);
    }

    [Fact]
    public void Round_trip_preserves_arbitrary_text()
    {
        const string text = "line one\r\nline two\ttabbed\r\n  trailing spaces   ";
        Assert.Equal(text, _cipher.Decrypt(_cipher.Encrypt(text, "roundtrip"), "roundtrip"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(23)]
    [InlineData(24)]
    public void Passwords_up_to_the_documented_limit_are_accepted(int passwordLength)
    {
        string password = new('a', passwordLength);
        Assert.Equal("secret", _cipher.Decrypt(_cipher.Encrypt("secret", password), password));
    }

    [Fact]
    public void Passwords_over_the_limit_fail_with_an_explanation_rather_than_a_crash()
    {
        // The original threw a raw CryptographicException here, because Base64 of 25 bytes is
        // 36 characters and 36 bytes is not a legal AES key length.
        string password = new('a', 25);

        var exception = Assert.Throws<UnsupportedOperationException>(() => _cipher.Encrypt("secret", password));
        Assert.Contains("24 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrong_password_reports_a_password_problem()
    {
        string cipherText = _cipher.Encrypt("secret", "correct");
        Assert.ThrowsAny<CipherDeskCryptoException>(() => _cipher.Decrypt(cipherText, "wrong"));
    }

    [Fact]
    public void Invalid_base64_is_rejected_as_malformed_not_as_a_bad_password()
    {
        Assert.Throws<MalformedPayloadException>(() => _cipher.Decrypt("this is not base64!!", "pass"));
    }

    [Fact]
    public void Non_block_aligned_input_is_rejected()
    {
        Assert.Throws<MalformedPayloadException>(() => _cipher.Decrypt(Convert.ToBase64String(new byte[17]), "pass"));
    }

    [Fact]
    public void Legacy_output_is_deterministic_which_is_exactly_why_it_is_deprecated()
    {
        // Documenting the weakness in a test: the fixed IV means the same input always produces
        // the same ciphertext, so an observer can tell when two messages are identical.
        Assert.Equal(_cipher.Encrypt("same", "key"), _cipher.Encrypt("same", "key"));
    }
}
