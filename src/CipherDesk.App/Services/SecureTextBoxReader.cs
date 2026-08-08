using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CipherDesk.App.Services;

/// <summary>
/// Reads a password out of a <see cref="TextBox"/> into a buffer the caller can wipe.
/// </summary>
/// <remarks>
/// <para>
/// Reading <see cref="Control.Text"/> allocates an immutable <see cref="string"/> on the managed
/// heap. Strings cannot be overwritten, may be moved by the GC and survive until collection, so a
/// password read that way can sit in a crash dump or page file long after the app has finished
/// with it. Pulling the characters straight out of the native edit control with <c>WM_GETTEXT</c>
/// avoids creating that string at all.
/// </para>
/// <para>
/// This is a meaningful reduction in exposure, not a guarantee: the edit control keeps its own
/// copy until it is cleared, and Windows may still page that out. Full protection would need a
/// custom control that never stores plaintext, which is out of scope here.
/// </para>
/// </remarks>
public static class SecureTextBoxReader
{
    private const int WmGetText = 0x000D;
    private const int WmGetTextLength = 0x000E;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, [Out] char[] lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessageLength(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Returns the contents as a character array. The caller owns it and should wipe it with
    /// <see cref="Wipe"/> - a <c>try/finally</c> or <see cref="PasswordScope"/> is the safe pattern.
    /// </summary>
    public static char[] Read(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        if (!textBox.IsHandleCreated)
        {
            // No native window yet: fall back to the managed path rather than failing.
            return textBox.Text.ToCharArray();
        }

        int length = (int)SendMessageLength(textBox.Handle, WmGetTextLength, IntPtr.Zero, IntPtr.Zero);
        if (length <= 0) return Array.Empty<char>();

        char[] buffer = new char[length + 1]; // WM_GETTEXT writes a terminating null.
        int copied = (int)SendMessage(textBox.Handle, WmGetText, new IntPtr(buffer.Length), buffer);

        if (copied == length)
        {
            char[] password = buffer[..length];
            Wipe(buffer); // the oversized buffer still holds the password
            return password;
        }

        // Length changed between the two messages; fall back rather than return a partial password.
        Wipe(buffer);
        return textBox.Text.ToCharArray();
    }

    /// <summary>Overwrites a password buffer with zeros.</summary>
    public static void Wipe(char[]? buffer)
    {
        if (buffer is null || buffer.Length == 0) return;
        Array.Clear(buffer, 0, buffer.Length);
    }

    /// <summary>Scope helper so a password buffer is always wiped, including on exceptions.</summary>
    public readonly struct PasswordScope : IDisposable
    {
        public PasswordScope(TextBox textBox) => Password = Read(textBox);

        public char[] Password { get; }

        public bool IsEmpty => Password.Length == 0;

        public void Dispose() => Wipe(Password);
    }
}
