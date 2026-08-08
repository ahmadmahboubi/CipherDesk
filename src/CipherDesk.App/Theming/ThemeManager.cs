using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CipherDesk.App.Theming;

/// <summary>
/// Owns the active palette and keeps it in sync with the Windows app theme.
/// </summary>
public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private static ThemeMode _mode = ThemeMode.System;

    /// <summary>Raised after <see cref="Current"/> changes, on the thread the change was made from.</summary>
    public static event EventHandler? Changed;

    public static ThemePalette Current { get; private set; } = ThemePalette.Light;

    public static ThemeMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            Refresh();
        }
    }

    /// <summary>Call once at startup, before the first form is shown.</summary>
    public static void Initialize(ThemeMode mode)
    {
        _mode = mode;
        Current = Resolve(mode);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>Cycles Light -> Dark -> System, which is the order the toggle button walks.</summary>
    public static ThemeMode CycleMode()
    {
        Mode = Mode switch
        {
            ThemeMode.Light => ThemeMode.Dark,
            ThemeMode.Dark => ThemeMode.System,
            _ => ThemeMode.Light
        };

        return Mode;
    }

    public static bool IsSystemDark()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            // The value is "apps use LIGHT theme", so 0 means dark. Missing key means light.
            return key?.GetValue(AppsUseLightThemeValue) is int light && light == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Applies the current palette to a control and everything beneath it.
    /// Custom controls theme themselves; the built-in ones get sensible defaults here.
    /// </summary>
    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ApplyRecursive(root, Current);
    }

    private static void ApplyRecursive(Control control, ThemePalette palette)
    {
        switch (control)
        {
            case IThemedControl themed:
                themed.ApplyTheme(palette);
                break;

            case Form form:
                form.BackColor = palette.Background;
                form.ForeColor = palette.TextPrimary;
                break;

            case Label label:
                label.BackColor = System.Drawing.Color.Transparent;
                label.ForeColor = label.Tag as string == "secondary" ? palette.TextSecondary : palette.TextPrimary;
                break;

            case TextBoxBase textBox:
                textBox.BackColor = palette.InputBackground;
                textBox.ForeColor = palette.TextPrimary;
                break;

            default:
                control.ForeColor = palette.TextPrimary;
                break;
        }

        foreach (Control child in control.Controls)
            ApplyRecursive(child, palette);
    }

    private static void Refresh()
    {
        ThemePalette resolved = Resolve(_mode);
        if (resolved == Current) return;

        Current = resolved;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static ThemePalette Resolve(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => ThemePalette.Light,
        ThemeMode.Dark => ThemePalette.Dark,
        _ => IsSystemDark() ? ThemePalette.Dark : ThemePalette.Light
    };

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_mode != ThemeMode.System) return;
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color)) return;

        Refresh();
    }
}
