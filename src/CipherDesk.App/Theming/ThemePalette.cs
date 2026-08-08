using System.Drawing;

namespace CipherDesk.App.Theming;

/// <summary>
/// The complete set of colours the UI is allowed to use.
/// </summary>
/// <remarks>
/// Every colour in the application comes from here. Controls never hard-code a
/// <see cref="Color"/>, which is what keeps light and dark genuinely consistent
/// and makes a third theme a matter of adding one more instance.
/// </remarks>
public sealed record ThemePalette
{
    public required bool IsDark { get; init; }

    // Surfaces
    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceMuted { get; init; }
    public required Color Border { get; init; }
    public required Color BorderStrong { get; init; }

    // Text
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color TextDisabled { get; init; }

    // Brand
    public required Color Accent { get; init; }
    public required Color AccentHover { get; init; }
    public required Color AccentPressed { get; init; }
    public required Color OnAccent { get; init; }
    public required Color AccentSoft { get; init; }

    // Semantic
    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Danger { get; init; }

    // Inputs
    public required Color InputBackground { get; init; }
    public required Color InputBorder { get; init; }
    public required Color Selection { get; init; }

    /// <summary>Daylight palette: near-white surfaces on a soft grey canvas.</summary>
    public static ThemePalette Light { get; } = new()
    {
        IsDark = false,
        Background = Color.FromArgb(0xF4, 0xF5, 0xF8),
        Surface = Color.FromArgb(0xFF, 0xFF, 0xFF),
        SurfaceMuted = Color.FromArgb(0xF0, 0xF1, 0xF5),
        Border = Color.FromArgb(0xE3, 0xE6, 0xEC),
        BorderStrong = Color.FromArgb(0xCB, 0xD1, 0xDB),
        TextPrimary = Color.FromArgb(0x14, 0x17, 0x1F),
        TextSecondary = Color.FromArgb(0x5B, 0x63, 0x74),
        TextDisabled = Color.FromArgb(0xA0, 0xA7, 0xB4),
        Accent = Color.FromArgb(0x6D, 0x5A, 0xE6),
        AccentHover = Color.FromArgb(0x5D, 0x4A, 0xD8),
        AccentPressed = Color.FromArgb(0x4E, 0x3D, 0xC2),
        OnAccent = Color.White,
        AccentSoft = Color.FromArgb(0xEC, 0xE9, 0xFD),
        Success = Color.FromArgb(0x0E, 0x9F, 0x6E),
        Warning = Color.FromArgb(0xD9, 0x8E, 0x04),
        Danger = Color.FromArgb(0xD9, 0x35, 0x3B),
        InputBackground = Color.FromArgb(0xFF, 0xFF, 0xFF),
        InputBorder = Color.FromArgb(0xD5, 0xDA, 0xE3),
        Selection = Color.FromArgb(0xD9, 0xD3, 0xFB)
    };

    /// <summary>Night palette: deep neutral greys, brightened accent for contrast.</summary>
    public static ThemePalette Dark { get; } = new()
    {
        IsDark = true,
        Background = Color.FromArgb(0x12, 0x14, 0x18),
        Surface = Color.FromArgb(0x1B, 0x1E, 0x25),
        SurfaceMuted = Color.FromArgb(0x23, 0x27, 0x30),
        Border = Color.FromArgb(0x2B, 0x30, 0x3A),
        BorderStrong = Color.FromArgb(0x3A, 0x41, 0x4E),
        TextPrimary = Color.FromArgb(0xEC, 0xEF, 0xF4),
        TextSecondary = Color.FromArgb(0x9A, 0xA3, 0xB2),
        TextDisabled = Color.FromArgb(0x60, 0x69, 0x78),
        Accent = Color.FromArgb(0x8B, 0x7C, 0xF6),
        AccentHover = Color.FromArgb(0x9C, 0x8F, 0xF8),
        AccentPressed = Color.FromArgb(0x77, 0x67, 0xE4),
        OnAccent = Color.FromArgb(0x0E, 0x0C, 0x1A),
        AccentSoft = Color.FromArgb(0x27, 0x24, 0x3C),
        Success = Color.FromArgb(0x2F, 0xC4, 0x8D),
        Warning = Color.FromArgb(0xF5, 0xB1, 0x3D),
        Danger = Color.FromArgb(0xF0, 0x62, 0x67),
        InputBackground = Color.FromArgb(0x15, 0x18, 0x1E),
        InputBorder = Color.FromArgb(0x33, 0x39, 0x45),
        Selection = Color.FromArgb(0x3A, 0x33, 0x66)
    };
}
