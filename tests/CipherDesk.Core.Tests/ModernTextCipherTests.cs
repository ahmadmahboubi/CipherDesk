using System;
using CipherDesk.Core;
using CipherDesk.Core.Text;
using Xunit;

namespace CipherDesk.Core.Tests;

public sealed class ModernTextCipherTests
{
    // A low iteration count keeps the suite fast; the actual iteration count
    // is stored in the payload header, so payloads created with other work
    // factors can still be decrypted.
    private readonly ModernTextCipher _cipher =
        new(iterations: 1_000);

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Hello, World!")]
    [InlineData("Grüße – 日本語 ✓")]
    public void Round_trip_returns_the_original(string text)
    {
        string cipherText =
            _cipher.Encrypt(
                text,
                "password");

        string decrypted =
            _cipher.Decrypt(
                cipherText,
                "password");

        Assert.Equal(
            text,
            decrypted);
    }

    [Fact]
    public void Large_input_round_trips()
    {
        string text =
            new('x', 500_000);

        string cipherText =
            _cipher.Encrypt(
                text,
                "password");

        string decrypted =
            _cipher.Decrypt(
                cipherText,
                "password");

        Assert.Equal(
            text,
            decrypted);
    }

    [Fact]
    public void Encrypting_the_same_text_twice_produces_different_output()
    {
        // Random salt and nonce ensure that identical plaintext encrypted
        // with the same password does not produce identical ciphertext.
        string first =
            _cipher.Encrypt(
                "same",
                "key");

        string second =
            _cipher.Encrypt(
                "same",
                "key");

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void Wrong_password_is_rejected()
    {
        string cipherText =
            _cipher.Encrypt(
                "secret",
                "correct");

        Assert.Throws<InvalidPasswordException>(
            () => _cipher.Decrypt(
                cipherText,
                "wrong"));
    }

    [Fact]
    public void Passwords_have_no_length_limit()
    {
        string password =
            new('p', 4096);

        string cipherText =
            _cipher.Encrypt(
                "secret",
                password);

        string decrypted =
            _cipher.Decrypt(
                cipherText,
                password);

        Assert.Equal(
            "secret",
            decrypted);
    }

    [Fact]
    public void Tampering_with_the_ciphertext_is_detected()
    {
        byte[] payload =
            Convert.FromBase64String(
                _cipher.Encrypt(
                    "secret message",
                    "key"));

        // Flip one bit inside the encrypted payload.
        payload[^5] ^= 0x01;

        string tampered =
            Convert.ToBase64String(
                payload);

        Assert.Throws<InvalidPasswordException>(
            () => _cipher.Decrypt(
                tampered,
                "key"));
    }

    [Fact]
    public void Tampering_with_the_header_is_detected_because_it_is_authenticated()
    {
        byte[] payload =
            Convert.FromBase64String(
                _cipher.Encrypt(
                    "secret message",
                    "key"));

        // The payload contains the authenticated salt/header.
        payload[20] ^= 0x01;

        string tampered =
            Convert.ToBase64String(
                payload);

        Assert.Throws<InvalidPasswordException>(
            () => _cipher.Decrypt(
                tampered,
                "key"));
    }

    [Fact]
    public void Truncated_payloads_are_rejected()
    {
        byte[] payload =
            Convert.FromBase64String(
                _cipher.Encrypt(
                    "secret",
                    "key"));

        string truncated =
            Convert.ToBase64String(
                payload[..(payload.Length - 8)]);

        Assert.Throws<MalformedPayloadException>(
            () => _cipher.Decrypt(
                truncated,
                "key"));
    }

    [Fact]
    public void An_unknown_format_version_is_reported_clearly()
    {
        byte[] payload =
            Convert.FromBase64String(
                _cipher.Encrypt(
                    "secret",
                    "key"));

        payload[4] = 99;

        string invalidPayload =
            Convert.ToBase64String(
                payload);

        var exception =
            Assert.Throws<MalformedPayloadException>(
                () => _cipher.Decrypt(
                    invalidPayload,
                    "key"));

        Assert.Contains(
            "update",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detects_its_own_payloads_and_rejects_legacy_ones()
    {
        string modern =
            _cipher.Encrypt(
                "x",
                "k");

        string legacy =
            new LegacyTextCipher()
                .Encrypt(
                    "x",
                    "k");

        Assert.True(
            _cipher.CanDecrypt(
                modern));

        Assert.False(
            _cipher.CanDecrypt(
                legacy));

        Assert.False(
            _cipher.CanDecrypt(
                "not base64 at all"));

        Assert.False(
            _cipher.CanDecrypt(
                ""));
    }

    [Fact]
    public void Detects_cbc_payloads_as_not_modern()
    {
        string cbc =
            new CBCTextCipher()
                .Encrypt(
                    "x",
                    "k");

        Assert.False(
            _cipher.CanDecrypt(
                cbc));
    }
}

public sealed class TextCipherRouterTests
{
    private readonly TextCipherRouter _router =
        new(
            new ModernTextCipher(1_000),
            new LegacyTextCipher(),
            new CBCTextCipher());

    [Fact]
    public void Auto_detection_reads_modern_and_legacy_formats()
    {
        string modern =
            _router.Encrypt(
                "hello",
                "pw",
                CipherFormat.Modern);

        string legacy =
            _router.Encrypt(
                "hello",
                "pw",
                CipherFormat.Legacy);

        Assert.Equal(
            CipherFormat.Modern,
            _router.Detect(
                modern));

        Assert.Equal(
            CipherFormat.Legacy,
            _router.Detect(
                legacy));

        Assert.Equal(
            "hello",
            _router.Decrypt(
                modern,
                "pw"));

        Assert.Equal(
            "hello",
            _router.Decrypt(
                legacy,
                "pw"));
    }

    [Fact]
    public void CBC_format_can_be_encrypted_and_decrypted_explicitly()
    {
        string cipherText =
            _router.Encrypt(
                "hello",
                "pw",
                CipherFormat.CBC);

        Assert.Equal(
            "hello",
            _router.Decrypt(
                cipherText,
                "pw",
                CipherFormat.CBC));
    }

    [Fact]
    public void CBC_format_is_not_silently_used_by_auto_encryption()
    {
        string cipherText =
            _router.Encrypt(
                "hello",
                "pw",
                CipherFormat.Auto);

        Assert.Equal(
            CipherFormat.Modern,
            _router.Detect(
                cipherText));

        Assert.Equal(
            "hello",
            _router.Decrypt(
                cipherText,
                "pw",
                CipherFormat.Auto));
    }

    [Fact]
    public void Auto_encryption_never_silently_chooses_a_legacy_format()
    {
        string cipherText =
            _router.Encrypt(
                "hello",
                "pw",
                CipherFormat.Auto);

        Assert.Equal(
            CipherFormat.Modern,
            _router.Detect(
                cipherText));
    }

    [Fact]
    public void Explicit_format_bypasses_automatic_detection()
    {
        string cbc =
            _router.Encrypt(
                "hello",
                "pw",
                CipherFormat.CBC);

        Assert.Equal(
            "hello",
            _router.Decrypt(
                cbc,
                "pw",
                CipherFormat.CBC));
    }

    [Fact]
    public void Detection_is_by_signature_not_by_trial_decryption()
    {
        // A wrong password must surface as a password error.
        // It must never cause the router to reinterpret the payload
        // as another format.
        string modern =
            _router.Encrypt(
                "hello",
                "right",
                CipherFormat.Modern);

        Assert.Throws<InvalidPasswordException>(
            () => _router.Decrypt(
                modern,
                "wrong"));
    }

    [Fact]
    public void Auto_detection_does_not_identify_cbc_payloads_as_modern()
    {
        string cbc =
            _router.Encrypt(
                "hello",
                "pw",
                CipherFormat.CBC);

        Assert.False(
            new ModernTextCipher(1_000)
                .CanDecrypt(cbc));
    }

    [Fact]
    public void Explicit_legacy_decryption_works()
    {
        string legacy =
            _router.Encrypt(
                "legacy text",
                "password",
                CipherFormat.Legacy);

        Assert.Equal(
            "legacy text",
            _router.Decrypt(
                legacy,
                "password",
                CipherFormat.Legacy));
    }

    [Fact]
    public void Explicit_cbc_decryption_works()
    {
        string cbc =
            _router.Encrypt(
                "cbc text",
                "password",
                CipherFormat.CBC);

        Assert.Equal(
            "cbc text",
            _router.Decrypt(
                cbc,
                "password",
                CipherFormat.CBC));
    }
}
