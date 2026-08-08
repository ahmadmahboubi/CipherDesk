using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CipherDesk.App.Theming;

/// <summary>
/// Applies the palette to the parts of the window WinForms does not own: the title bar and border.
/// </summary>
/// <remarks>
/// Without this, a dark form still gets a bright white caption bar, which is the single most
/// obvious tell of a WinForms app pretending to be modern. All calls degrade silently on older
/// Windows builds that do not know the attribute.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class DwmWindowTheme
{
    private const int DwmwaUseImmersiveDarkModeLegacy = 19; // Windows 10 builds 18362-18984
    private const int DwmwaUseImmersiveDarkMode = 20;       // Windows 10 build 18985 and later
    private const int DwmwaWindowCornerPreference = 33;     // Windows 11
    private const int DwmwaCaptionColour = 35;              // Windows 11 build 22000+
    private const int DwmwaBorderColour = 34;

    private const int CornerPreferenceRound = 2;

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Styles the non-client area to match the supplied palette.</summary>
    public static void Apply(IntPtr handle, ThemePalette palette)
    {
        if (handle == IntPtr.Zero) return;

        SetDarkMode(handle, palette.IsDark);

        if (!IsWindows11OrLater) return;

        SetAttribute(handle, DwmwaWindowCornerPreference, CornerPreferenceRound);
        SetAttribute(handle, DwmwaCaptionColour, ToColorRef(palette.Background));
        SetAttribute(handle, DwmwaBorderColour, ToColorRef(palette.Border));
    }

    private static void SetDarkMode(IntPtr handle, bool dark)
    {
        int value = dark ? 1 : 0;

        // Newer attribute first; fall back for the narrow band of builds that only know the old one.
        if (SetAttribute(handle, DwmwaUseImmersiveDarkMode, value) != 0)
            SetAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, value);
    }

    private static int SetAttribute(IntPtr handle, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            return -1; // dwmapi is always present on supported systems; never let this be fatal.
        }
        catch (EntryPointNotFoundException)
        {
            return -1;
        }
    }

    /// <summary>DWM expects colours as 0x00BBGGRR, not the ARGB that <see cref="Color"/> stores.</summary>
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    private static bool IsWindows11OrLater =>
        Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Build >= 22000;
}
