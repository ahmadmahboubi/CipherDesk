namespace CipherDesk.Core.Passwords;

/// <summary>Coarse strength bands, used to drive the strength meter in the UI.</summary>
public enum PasswordStrength
{
    Empty = 0,
    VeryWeak = 1,
    Weak = 2,
    Fair = 3,
    Strong = 4,
    VeryStrong = 5
}

/// <summary>Result of a strength assessment.</summary>
/// <param name="Strength">The band the password falls into.</param>
/// <param name="EntropyBits">Estimated entropy in bits, after penalties.</param>
/// <param name="Advice">A single short, actionable suggestion, or null when there is nothing to add.</param>
public readonly record struct PasswordAssessment(PasswordStrength Strength, double EntropyBits, string? Advice)
{
    /// <summary>Human-readable band name for display.</summary>
    public string Label => Strength switch
    {
        PasswordStrength.Empty => "No password",
        PasswordStrength.VeryWeak => "Very weak",
        PasswordStrength.Weak => "Weak",
        PasswordStrength.Fair => "Fair",
        PasswordStrength.Strong => "Strong",
        _ => "Very strong"
    };
}
