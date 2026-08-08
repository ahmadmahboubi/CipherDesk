using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;

namespace CipherDesk.App.Theming;

/// <summary>
/// Central font definitions, resolved once at startup.
/// </summary>
/// <remarks>
/// Windows 11 ships "Segoe UI Variable", which has optical sizes tuned per text size and looks
/// noticeably better than plain Segoe UI at display sizes. It is absent on Windows 10, so each
/// role resolves through a fallback chain rather than assuming a family exists.
/// </remarks>
public static class Typography
{
    private static readonly string[] InstalledFamilies =
        FontFamily.Families.Select(f => f.Name).ToArray();

    private static readonly string UiDisplay = FirstAvailable("Segoe UI Variable Display", "Segoe UI Semibold", "Segoe UI");
    private static readonly string UiText = FirstAvailable("Segoe UI Variable Text", "Segoe UI");
    private static readonly string Monospace = FirstAvailable("Cascadia Mono", "Consolas", "Courier New");

    /// <summary>Icon font. Fluent icons on Windows 11, MDL2 on Windows 10.</summary>
    public static readonly string IconFamily = FirstAvailable("Segoe Fluent Icons", "Segoe MDL2 Assets", "Segoe UI Symbol");

    public static Font Display { get; } = new(UiDisplay, 17f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font Heading { get; } = new(UiDisplay, 11.5f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font Subheading { get; } = new(UiText, 9.75f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font Body { get; } = new(UiText, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font BodyStrong { get; } = new(UiText, 9.75f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font Caption { get; } = new(UiText, 8.5f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>Used for ciphertext and passwords, where character-by-character reading matters.</summary>
    public static Font Mono { get; } = new(Monospace, 9.75f, FontStyle.Regular, GraphicsUnit.Point);

    public static Font Icon(float size) => new(IconFamily, size, FontStyle.Regular, GraphicsUnit.Point);

    private static string FirstAvailable(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (InstalledFamilies.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                return candidate;
        }

        return candidates[^1];
    }
}
