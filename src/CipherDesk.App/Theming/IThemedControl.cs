namespace CipherDesk.App.Theming;

/// <summary>
/// Implemented by controls that repaint themselves when the palette changes.
/// </summary>
/// <remarks>
/// Controls do not subscribe to <see cref="ThemeManager"/> directly. The owning form walks its
/// control tree instead, which avoids a static event holding references to disposed controls -
/// a classic and easy-to-miss WinForms leak.
/// </remarks>
public interface IThemedControl
{
    void ApplyTheme(ThemePalette palette);
}
