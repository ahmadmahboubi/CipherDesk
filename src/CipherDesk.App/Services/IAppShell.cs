using CipherDesk.App.Controls;

namespace CipherDesk.App.Services;

/// <summary>
/// The slice of the main window that the views are allowed to touch.
/// </summary>
/// <remarks>
/// Views depend on this interface rather than on <c>MainForm</c>. That keeps the dependency
/// pointing inwards, stops the views from reaching into unrelated window state, and lets each
/// view be hosted or tested somewhere else without change.
/// </remarks>
public interface IAppShell
{
    /// <summary>Shows transient feedback in the corner of the window.</summary>
    void Notify(string message, ToastKind kind = ToastKind.Info);

    /// <summary>Sets the left-hand text of the status bar.</summary>
    void SetStatus(string message);

    /// <summary>Shows or hides the indeterminate activity indicator in the status bar.</summary>
    void SetBusy(bool busy);

    /// <summary>Sets the format badge on the right of the status bar.</summary>
    void SetFormatBadge(string text);
}
