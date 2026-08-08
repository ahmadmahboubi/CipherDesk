using System;
using System.Linq;
using CipherDesk.Core.Passwords;
using Xunit;

namespace CipherDesk.Core.Tests;

public sealed class PasswordStrengthTests
{
    [Fact]
    public void An_empty_password_is_reported_as_empty()
    {
        Assert.Equal(PasswordStrength.Empty, PasswordStrengthEvaluator.Evaluate("").Strength);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Password1")]
    [InlineData("123456")]
    [InlineData("qwerty")]
    [InlineData("aaaaaaaaaaaa")]
    public void Well_known_and_padded_passwords_are_rated_weak(string password)
    {
        Assert.True(PasswordStrengthEvaluator.Evaluate(password).Strength <= PasswordStrength.Weak);
    }

    [Theory]
    [InlineData("Tr0ub4dor&3xKcd!Long")]
    [InlineData("9f!Kq2#vLm7@Rt4$Wp1&Zx")]
    public void Long_mixed_passwords_are_rated_strong(string password)
    {
        Assert.True(PasswordStrengthEvaluator.Evaluate(password).Strength >= PasswordStrength.Strong);
    }

    [Fact]
    public void Advice_is_offered_for_weak_passwords_and_withheld_for_strong_ones()
    {
        Assert.NotNull(PasswordStrengthEvaluator.Evaluate("abc").Advice);
        Assert.Null(PasswordStrengthEvaluator.Evaluate("9f!Kq2#vLm7@Rt4$Wp1&Zx").Advice);
    }

    [Fact]
    public void Keyboard_runs_are_penalised_relative_to_random_text()
    {
        double run = PasswordStrengthEvaluator.Evaluate("qwertyuiop").EntropyBits;
        double random = PasswordStrengthEvaluator.Evaluate("xkvbmqpzln").EntropyBits;

        Assert.True(run < random);
    }
}

public sealed class PasswordGeneratorTests
{
    [Fact]
    public void Generated_passwords_have_the_requested_length()
    {
        Assert.Equal(24, PasswordGenerator.Generate(24).Length);
    }

    [Fact]
    public void Generated_passwords_include_every_character_class()
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            char[] password = PasswordGenerator.Generate(12);

            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, c => !char.IsLetterOrDigit(c));
        }
    }

    [Fact]
    public void Generated_passwords_are_rated_very_strong()
    {
        char[] password = PasswordGenerator.Generate();
        Assert.Equal(PasswordStrength.VeryStrong, PasswordStrengthEvaluator.Evaluate(password).Strength);
    }

    [Fact]
    public void Generated_passwords_are_not_repeated()
    {
        string[] passwords = Enumerable.Range(0, 20).Select(_ => new string(PasswordGenerator.Generate())).ToArray();
        Assert.Equal(passwords.Length, passwords.Distinct().Count());
    }

    [Fact]
    public void Trivially_short_lengths_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(4));
    }
}
