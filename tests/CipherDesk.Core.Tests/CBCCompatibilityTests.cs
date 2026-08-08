using System;
using System.Security.Cryptography;
using System.Text;
using CipherDesk.Core.Text;
using Xunit;

namespace CipherDesk.Core.Tests;

/// <summary>
/// Guards backwards compatibility with the original
/// EncryptString / DecryptString implementation.
///
/// The expected ciphertexts must be produced by an independent reference
/// implementation of the original algorithm, not by CBCTextCipher itself.
/// This ensures that changes to key derivation, IV handling, Rijndael settings,
/// padding, or text encoding cannot silently break compatibility.
///
/// The original format uses:
/// - RijndaelManaged
/// - 256-bit key
/// - 128-bit block size
/// - CBC mode
/// - PKCS#7 padding
/// - Fixed IV: "hamed1476u@qwxcT"
/// - PasswordDeriveBytes with a null salt
/// - UTF-8 plaintext encoding
/// - Base64 ciphertext representation
///
/// This format is retained solely for compatibility with existing data and
/// must not be used for newly encrypted data.
/// </summary>
public sealed class CBCCompatibilityTests
{
    private readonly CBCTextCipher _cipher = new();

    [Theory]
    [InlineData(
        "Hello, World!",
        "pass",
        "REPLACE_WITH_REFERENCE_CIPHERTEXT")]
    [InlineData(
        "The quick brown fox jumps over the lazy dog",
        "s3cret",
        "REPLACE_WITH_REFERENCE_CIPHERTEXT")]
    [InlineData(
        "",
        "p",
        "REPLACE_WITH_REFERENCE_CIPHERTEXT")]
    [InlineData(
        "a",
        "a",
        "REPLACE_WITH_REFERENCE_CIPHERTEXT")]
    [InlineData(
        "Grüße aus Berlin – ünïcödé ✓ 日本語",
        "Pässw0rd!",
        "REPLACE_WITH_REFERENCE_CIPHERTEXT")]
    public void Encrypt_matches_the_original_implementation_byte_for_byte(
        string plainText,
        string password,
        string expected)
    {
        Assert.Equal(
            expected,
            _cipher.Encrypt(plainText, password));
    }

    [Theory]
    [InlineData(
        "Hello, World!",
        "pass",
        "REPLACE_WITH_REFERENCE_CIPHERTEXT")]
    [InlineData(
        "",
        "p",
        "REPLACE_WITH_REFERENCE_CIPHERTEXT")]
    [InlineData(
        "Grüße aus Berlin – ünïcödé ✓ 日本語",
        "Pässw0rd!",
        "REPLACE_WITH_REFERENCE_CIPHERTEXT")]
    public void Decrypt_reads_ciphertext_written_by_the_original_implementation(
        string expected,
        string password,
        string cipherText)
    {
        Assert.Equal(
            expected,
            _cipher.Decrypt(cipherText, password));
    }

    [Fact]
    public void Long_input_matches_the_original_implementation()
    {
        // A 1000-character payload spans many blocks.
        // Comparing the ciphertext hash keeps the golden vector readable
        // while still detecting changes to the complete encryption pipeline.
        string cipherText =
            _cipher.Encrypt(
                new string('x', 1000),
                "LongPassword");

        string hash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(cipherText)))
            .ToLowerInvariant();

        // Replace this value with the SHA-256 hash calculated from the
        // independent original implementation.
        Assert.Equal(
            "REPLACE_WITH_REFERENCE_HASH",
            hash);
    }

    [Fact]
    public void Round_trip_preserves_arbitrary_text()
    {
        const string text =
            "line one\r\n" +
            "line two\ttabbed\r\n" +
            "  trailing spaces   ";

        string cipherText =
            _cipher.Encrypt(
                text,
                "roundtrip");

        string decrypted =
            _cipher.Decrypt(
                cipherText,
                "roundtrip");

        Assert.Equal(text, decrypted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("password")]
    [InlineData("Pässw0rd!")]
    [InlineData("LongPasswordUpTo24Bytes!")]
    public void Passwords_are_accepted_by_the_legacy_key_derivation(
        string password)
    {
        const string text = "secret";

        string cipherText =
            _cipher.Encrypt(text, password);

        string decrypted =
            _cipher.Decrypt(cipherText, password);

        Assert.Equal(text, decrypted);
    }

    [Fact]
    public void Fixed_iv_and_legacy_key_derivation_make_output_deterministic()
    {
        const string plainText = "same";
        const string password = "key";

        string first =
            _cipher.Encrypt(
                plainText,
                password);

        string second =
            _cipher.Encrypt(
                plainText,
                password);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_plaintext_produces_different_ciphertext()
    {
        const string password = "key";

        string first =
            _cipher.Encrypt(
                "first",
                password);

        string second =
            _cipher.Encrypt(
                "second",
                password);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Different_password_produces_different_ciphertext()
    {
        const string plainText = "secret";

        string first =
            _cipher.Encrypt(
                plainText,
                "password-one");

        string second =
            _cipher.Encrypt(
                plainText,
                "password-two");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Wrong_password_reports_a_password_problem()
    {
        string cipherText =
            _cipher.Encrypt(
                "secret",
                "correct");

        Assert.ThrowsAny<CipherDeskCryptoException>(
            () => _cipher.Decrypt(
                cipherText,
                "wrong"));
    }

    [Fact]
    public void Invalid_base64_is_rejected_as_malformed()
    {
        Assert.Throws<MalformedPayloadException>(
            () => _cipher.Decrypt(
                "this is not base64!!",
                "pass"));
    }

    [Fact]
    public void Empty_ciphertext_is_rejected_as_malformed()
    {
        Assert.Throws<ArgumentException>(
            () => _cipher.Decrypt(
                string.Empty,
                "pass"));
    }

    [Fact]
    public void Non_block_aligned_input_is_rejected()
    {
        byte[] invalidCiphertext =
            new byte[17];

        string base64 =
            Convert.ToBase64String(
                invalidCiphertext);

        Assert.Throws<MalformedPayloadException>(
            () => _cipher.Decrypt(
                base64,
                "pass"));
    }

    [Fact]
    public void Quoted_ciphertext_is_accepted_for_legacy_compatibility()
    {
        string cipherText =
            _cipher.Encrypt(
                "secret",
                "pass");

        string quoted =
            $"\"{cipherText}\"";

        Assert.Equal(
            "secret",
            _cipher.Decrypt(
                quoted,
                "pass"));
    }

    [Fact]
    public void Whitespace_around_ciphertext_is_ignored()
    {
        string cipherText =
            _cipher.Encrypt(
                "secret",
                "pass");

        string padded =
            $"  \r\n{cipherText}\r\n  ";

        Assert.Equal(
            "secret",
            _cipher.Decrypt(
                padded,
                "pass"));
    }

    [Fact]
    public void CanDecrypt_accepts_valid_legacy_ciphertext()
    {
        string cipherText =
            _cipher.Encrypt(
                "secret",
                "pass");

        Assert.True(
            _cipher.CanDecrypt(cipherText));
    }

    [Fact]
    public void CanDecrypt_rejects_invalid_base64()
    {
        Assert.False(
            _cipher.CanDecrypt(
                "this is not base64!!"));
    }

    [Fact]
    public void CanDecrypt_rejects_non_block_aligned_payload()
    {
        string invalidCiphertext =
            Convert.ToBase64String(
                new byte[17]);

        Assert.False(
            _cipher.CanDecrypt(
                invalidCiphertext));
    }

    [Fact]
    public void Empty_plaintext_is_supported()
    {
        string cipherText =
            _cipher.Encrypt(
                string.Empty,
                "password");

        Assert.Equal(
            string.Empty,
            _cipher.Decrypt(
                cipherText,
                "password"));
    }

    [Fact]
    public void Unicode_plaintext_is_preserved()
    {
        const string text =
            "سلام دنیا 🌍 Привет мир 日本語 한국어";

        const string password =
            "Pässw0rd!";

        string cipherText =
            _cipher.Encrypt(
                text,
                password);

        string decrypted =
            _cipher.Decrypt(
                cipherText,
                password);

        Assert.Equal(
            text,
            decrypted);
    }
}
