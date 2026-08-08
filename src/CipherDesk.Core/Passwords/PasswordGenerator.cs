using System;
using System.Security.Cryptography;

namespace CipherDesk.Core.Passwords;

/// <summary>Generates passphrase-grade random passwords using the OS CSPRNG.</summary>
public static class PasswordGenerator
{
    // Ambiguous glyphs (O/0, l/1/I) are excluded so a generated password can be transcribed reliably.
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+?";

    public const int DefaultLength = 20;

    /// <summary>Generates a password guaranteed to contain at least one character from each class.</summary>
    public static char[] Generate(int length = DefaultLength)
    {
        if (length < 8) throw new ArgumentOutOfRangeException(nameof(length), "Generated passwords must be at least 8 characters.");

        const string all = Lower + Upper + Digits + Symbols;
        var result = new char[length];

        result[0] = Pick(Lower);
        result[1] = Pick(Upper);
        result[2] = Pick(Digits);
        result[3] = Pick(Symbols);
        for (int i = 4; i < length; i++) result[i] = Pick(all);

        Shuffle(result);
        return result;
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    /// <summary>Fisher-Yates with a cryptographic source, so the guaranteed characters are not positionally predictable.</summary>
    private static void Shuffle(char[] buffer)
    {
        for (int i = buffer.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }
    }
}
