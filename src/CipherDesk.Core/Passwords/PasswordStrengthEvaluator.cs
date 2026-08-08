using System;

namespace CipherDesk.Core.Passwords;

/// <summary>
/// A small, dependency-free password strength estimator.
/// </summary>
/// <remarks>
/// It approximates entropy as <c>length x log2(character pool)</c> and then applies penalties for
/// the patterns that make that estimate a lie: repeats, straight runs, keyboard rows and common
/// passwords. It is a UI hint, not a security control - a real audit would use something like zxcvbn.
/// </remarks>
public static class PasswordStrengthEvaluator
{
    private static readonly string[] KeyboardRuns =
    {
        "qwertyuiop", "asdfghjkl", "zxcvbnm", "0123456789", "abcdefghijklmnopqrstuvwxyz"
    };

    private static readonly string[] CommonPasswords =
    {
        "password", "passw0rd", "123456", "12345678", "123456789", "qwerty", "abc123", "letmein",
        "monkey", "dragon", "iloveyou", "admin", "welcome", "login", "master", "hello", "secret",
        "sunshine", "princess", "football", "baseball", "trustno1", "starwars", "whatever", "test"
    };

    public static PasswordAssessment Evaluate(ReadOnlySpan<char> password)
    {
        if (password.IsEmpty)
            return new PasswordAssessment(PasswordStrength.Empty, 0, "A password is required.");

        int poolSize = EstimatePoolSize(password, out bool hasLower, out bool hasUpper, out bool hasDigit, out bool hasSymbol);
        double entropy = password.Length * Math.Log2(Math.Max(poolSize, 2));

        entropy -= RepetitionPenalty(password);
        entropy -= SequencePenalty(password);
        if (IsCommon(password)) entropy = Math.Min(entropy, 12);
        entropy = Math.Max(entropy, 0);

        PasswordStrength strength = entropy switch
        {
            < 28 => PasswordStrength.VeryWeak,
            < 40 => PasswordStrength.Weak,
            < 60 => PasswordStrength.Fair,
            < 80 => PasswordStrength.Strong,
            _ => PasswordStrength.VeryStrong
        };

        return new PasswordAssessment(strength, entropy,
            BuildAdvice(password.Length, hasLower, hasUpper, hasDigit, hasSymbol, strength));
    }

    public static PasswordAssessment Evaluate(string? password) =>
        Evaluate(password.AsSpan());

    private static int EstimatePoolSize(
        ReadOnlySpan<char> password,
        out bool hasLower, out bool hasUpper, out bool hasDigit, out bool hasSymbol)
    {
        hasLower = hasUpper = hasDigit = hasSymbol = false;
        bool hasNonAscii = false;

        foreach (char c in password)
        {
            if (char.IsLower(c)) hasLower = true;
            else if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (c > 127) hasNonAscii = true;
            else hasSymbol = true;
        }

        int pool = 0;
        if (hasLower) pool += 26;
        if (hasUpper) pool += 26;
        if (hasDigit) pool += 10;
        if (hasSymbol) pool += 33;
        if (hasNonAscii) pool += 100; // conservative allowance for the rest of Unicode
        return pool;
    }

    /// <summary>Penalises "aaaaaa" style padding, which adds length but almost no entropy.</summary>
    private static double RepetitionPenalty(ReadOnlySpan<char> password)
    {
        int repeats = 0;
        for (int i = 1; i < password.Length; i++)
            if (password[i] == password[i - 1]) repeats++;

        return repeats * 2.0;
    }

    /// <summary>Penalises straight runs such as "abcd", "1234" and "qwerty".</summary>
    /// <remarks>Works on a stack buffer so the password is never materialised as a string.</remarks>
    private static double SequencePenalty(ReadOnlySpan<char> password)
    {
        if (password.Length < 4) return 0;

        Span<char> lowered = password.Length <= 256 ? stackalloc char[password.Length] : new char[password.Length];
        password.ToLowerInvariant(lowered);

        double penalty = 0;
        foreach (string run in KeyboardRuns)
        {
            for (int length = Math.Min(run.Length, lowered.Length); length >= 4; length--)
            {
                for (int start = 0; start + length <= run.Length; start++)
                {
                    if (lowered.IndexOf(run.AsSpan(start, length), StringComparison.Ordinal) >= 0)
                    {
                        penalty = Math.Max(penalty, length * 2.5);
                        break;
                    }
                }

                if (penalty > 0) break; // longest match found for this run
            }
        }

        lowered.Clear();
        return penalty;
    }

    private static bool IsCommon(ReadOnlySpan<char> password)
    {
        if (Matches(password)) return true;

        // "password1", "qwerty!!" and friends: strip trailing digits and symbols, then re-check.
        int end = password.Length;
        while (end > 0 && !char.IsLetter(password[end - 1])) end--;
        return end >= 4 && Matches(password[..end]);

        static bool Matches(ReadOnlySpan<char> candidate)
        {
            foreach (string common in CommonPasswords)
            {
                if (candidate.Equals(common, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    private static string? BuildAdvice(
        int length, bool hasLower, bool hasUpper, bool hasDigit, bool hasSymbol, PasswordStrength strength)
    {
        if (strength >= PasswordStrength.Strong) return null;
        if (length < 12) return "Length helps more than anything else - aim for 12 characters or more.";
        if (!hasUpper || !hasLower) return "Mix upper and lower case.";
        if (!hasDigit) return "Add a digit or two.";
        if (!hasSymbol) return "Add a symbol.";
        return "Avoid predictable words and sequences.";
    }
}
