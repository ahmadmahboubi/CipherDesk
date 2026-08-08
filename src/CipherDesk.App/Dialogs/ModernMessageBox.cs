using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Controls;
using CipherDesk.App.Theming;

namespace CipherDesk.App.Dialogs;

/// <summary>
/// Defines one button displayed by <see cref="ModernMessageBox"/>.
/// </summary>
public sealed record DialogButton(
    string Text,
    DialogResult Result,
    ButtonVariant Variant = ButtonVariant.Secondary);

/// <summary>
/// A themed replacement for the standard MessageBox.
///
/// Keeps a real WinForms dialog frame rather than faking a borderless window.
/// This preserves Escape handling, focus behaviour, window management,
/// and the accessibility tree, while the caption is recoloured through DWM.
/// </summary>
public sealed class ModernMessageBox : Form
{
    private readonly ToastKind _kind;

    private ModernMessageBox(
        string title,
        string message,
        ToastKind kind,
        IReadOnlyList<DialogButton> buttons)
    {
        _kind = kind;

        // ---------------------------------------------------------
        // Form configuration
        // ---------------------------------------------------------

        Text = title;

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Typography.Body;

        Padding = new Padding(22, 20, 22, 18);
        MinimumSize = new Size(420, 0);

        // ---------------------------------------------------------
        // Main layout
        // ---------------------------------------------------------

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,

            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,

            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(SizeType.AutoSize));

        layout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        layout.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        layout.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        // ---------------------------------------------------------
        // Icon
        // ---------------------------------------------------------

        var icon = new Label
        {
            AutoSize = true,
            Font = Typography.Icon(20f),

            Text = GlyphFor(kind),

            Margin = new Padding(0, 2, 16, 0),

            ForeColor = ColorFor(
                kind,
                ThemeManager.Current)
        };

        // ---------------------------------------------------------
        // Message
        // ---------------------------------------------------------

        var body = new Label
        {
            AutoSize = true,

            MaximumSize = new Size(430, 0),

            Text = message,

            Font = Typography.Body,

            Margin = new Padding(0, 4, 0, 18)
        };

        // ---------------------------------------------------------
        // Button row
        // ---------------------------------------------------------

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,

            Dock = DockStyle.Fill,

            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,

            WrapContents = false,

            BackColor = Color.Transparent,

            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // Keep references here.
        //
        // Do NOT assign AcceptButton / CancelButton while the
        // controls are still outside the Form hierarchy.
        ModernButton? defaultButton = null;
        ModernButton? cancelButton = null;

        // ---------------------------------------------------------
        // Create buttons
        // ---------------------------------------------------------

        foreach (DialogButton definition in buttons)
        {
            var button = new ModernButton
            {
                Text = definition.Text,

                Variant = definition.Variant,

                DialogResult = definition.Result,

                Size = new Size(112, 36),

                Margin = new Padding(8, 0, 0, 0)
            };

            buttonRow.Controls.Add(button);

            // The first definition is the default action.
            defaultButton ??= button;

            // Cancel / No becomes the Escape action.
            if (definition.Result is DialogResult.Cancel or DialogResult.No)
            {
                cancelButton = button;
            }
        }

        // ---------------------------------------------------------
        // Build control hierarchy
        // ---------------------------------------------------------

        layout.Controls.Add(icon, 0, 0);
        layout.Controls.Add(body, 1, 0);
        layout.Controls.Add(buttonRow, 1, 1);

        Controls.Add(layout);

        // ---------------------------------------------------------
        // Form sizing
        // ---------------------------------------------------------

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // ---------------------------------------------------------
        // Apply theme
        // ---------------------------------------------------------

        ThemeManager.Apply(this);

        icon.ForeColor = ColorFor(
            kind,
            ThemeManager.Current);

        // ---------------------------------------------------------
        // IMPORTANT:
        //
        // Set AcceptButton / CancelButton only AFTER the controls
        // have been added to the Form hierarchy.
        //
        // Setting ActiveControl here is intentionally avoided.
        // ---------------------------------------------------------

        if (defaultButton is not null)
        {
            AcceptButton = defaultButton;
        }

        if (cancelButton is not null)
        {
            CancelButton = cancelButton;
        }
    }

    // -------------------------------------------------------------
    // Handle creation / DWM theme
    // -------------------------------------------------------------

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        DwmWindowTheme.Apply(
            Handle,
            ThemeManager.Current);
    }

    // -------------------------------------------------------------
    // Initial focus
    //
    // We deliberately don't set ActiveControl inside the
    // constructor. WinForms has not necessarily finished creating
    // and displaying the control hierarchy at that point.
    // -------------------------------------------------------------

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (AcceptButton is not Control button)
        {
            return;
        }

        if (!button.Visible ||
            !button.Enabled ||
            !button.CanFocus)
        {
            return;
        }

        // Wait until WinForms has completed the current layout/
        // activation cycle before requesting focus.
        BeginInvoke(new Action(() =>
        {
            if (IsDisposed ||
                Disposing ||
                !IsHandleCreated)
            {
                return;
            }

            if (!button.Visible ||
                !button.Enabled ||
                !button.CanFocus)
            {
                return;
            }

            button.Focus();
        }));
    }

    // -------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------

    /// <summary>
    /// Shows a dialog with an arbitrary button set.
    /// The first button is the default action.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string title,
        string message,
        ToastKind kind,
        params DialogButton[] buttons)
    {
        DialogButton[] effective =
            buttons.Length > 0
                ? buttons
                : new[]
                {
                    new DialogButton(
                        "OK",
                        DialogResult.OK,
                        ButtonVariant.Primary)
                };

        using var dialog = new ModernMessageBox(
            title,
            message,
            kind,
            effective);

        return owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);
    }

    // -------------------------------------------------------------
    // Convenience methods
    // -------------------------------------------------------------

    public static void Error(
        IWin32Window? owner,
        string title,
        string message)
    {
        Show(
            owner,
            title,
            message,
            ToastKind.Error,
            new DialogButton(
                "Close",
                DialogResult.OK,
                ButtonVariant.Primary));
    }

    public static void Info(
        IWin32Window? owner,
        string title,
        string message)
    {
        Show(
            owner,
            title,
            message,
            ToastKind.Info,
            new DialogButton(
                "OK",
                DialogResult.OK,
                ButtonVariant.Primary));
    }

    /// <summary>
    /// Returns true when the user picks the affirmative action.
    /// </summary>
    public static bool Confirm(
        IWin32Window? owner,
        string title,
        string message,
        string confirmText,
        bool destructive = false)
    {
        return Show(
            owner,
            title,
            message,
            ToastKind.Warning,

            new DialogButton(
                confirmText,
                DialogResult.Yes,
                destructive
                    ? ButtonVariant.Danger
                    : ButtonVariant.Primary),

            new DialogButton(
                "Cancel",
                DialogResult.Cancel)
        ) == DialogResult.Yes;
    }

    // -------------------------------------------------------------
    // Icon / color helpers
    // -------------------------------------------------------------

    private static string GlyphFor(ToastKind kind)
    {
        return kind switch
        {
            ToastKind.Success => Glyphs.Success,
            ToastKind.Warning => Glyphs.Warning,
            ToastKind.Error => Glyphs.Error,
            _ => Glyphs.Info
        };
    }

    private static Color ColorFor(
        ToastKind kind,
        ThemePalette palette)
    {
        return kind switch
        {
            ToastKind.Success => palette.Success,
            ToastKind.Warning => palette.Warning,
            ToastKind.Error => palette.Danger,
            _ => palette.Accent
        };
    }
}
