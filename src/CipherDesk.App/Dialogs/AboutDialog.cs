using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CipherDesk.App.Controls;
using CipherDesk.App.Theming;

namespace CipherDesk.App.Dialogs;

/// <summary>
/// Product information, keyboard shortcuts, supported encryption formats,
/// and an honest summary of the cryptographic protection used by CipherDesk.
/// </summary>
public sealed class AboutDialog : Form
{
    private const string ProjectUrl =
        "https://github.com/your-org/cipherdesk";

    public AboutDialog()
    {
        Text = "About CipherDesk";

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        AutoScaleMode = AutoScaleMode.Dpi;

        ClientSize = new Size(540, 520);
        MinimumSize = new Size(540, 520);
        Padding = new Padding(24, 20, 24, 18);

        Font = Typography.Body;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100f));

        layout.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        layout.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        layout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100f));

        layout.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(
            Heading("CipherDesk " + VersionString),
            0,
            0);

        layout.Controls.Add(
            Secondary(
                "Secure text and file encryption for Windows. " +
                "Free and open source under the MIT licence."),
            0,
            1);

        layout.Controls.Add(
            BuildDetails(),
            0,
            2);

        layout.Controls.Add(
            BuildButtons(),
            0,
            3);

        Controls.Add(layout);

        ThemeManager.Apply(this);
    }

    private static string VersionString =>
        Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString(3)
        ?? "2.0.0";

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        DwmWindowTheme.Apply(
            Handle,
            ThemeManager.Current);
    }

    private static Label Heading(string text)
    {
        return new Label
        {
            Text = text,
            Font = Typography.Heading,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };
    }

    private static Label Secondary(string text)
    {
        return new Label
        {
            Text = text,
            Font = Typography.Body,
            Tag = "secondary",
            AutoSize = true,
            MaximumSize = new Size(490, 0),
            Margin = new Padding(0, 0, 0, 16)
        };
    }

    private static Control BuildDetails()
    {
        var details = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = Typography.Body,
            TabStop = false,
            BackColor = SystemColors.Window,
            Text = string.Join(
                Environment.NewLine,
                new[]
                {
                    "How your data is protected",
                    "",
                    "  Modern format",
                    "  Cipher            AES-256-GCM",
                    "  Encryption        Authenticated encryption",
                    "  Key derivation    PBKDF2-HMAC-SHA256",
                    "  Work factor       210,000 iterations",
                    "  Salt              128-bit random per message",
                    "  Nonce             96-bit random per message",
                    "  Integrity         128-bit authentication tag",
                    "  Files             Authenticated 1 MiB chunks",
                    "",
                    "CBC format",
                    "  Cipher            AES-256-CBC",
                    "  Key derivation    PasswordDeriveBytes",
                    "  IV                Fixed application IV",
                    "  Purpose           Compatibility with the original",
                    "                    AES-256-CBC implementation",
                    "",
                    "  This format is retained for compatibility with",
                    "  existing data and should not be preferred for",
                    "  newly created encrypted data.",
                    "",
                    "Legacy v1 format",
                    "  Retained only for backward compatibility.",
                    "  No salt",
                    "  Fixed IV",
                    "  No authentication or integrity protection",
                    "  Unsalted and unstretched password-derived key",
                    "",
                    "  Legacy encryption should never be used for new data.",
                    "  It exists so older CipherDesk data can still be read.",
                    "",
                    "Keyboard shortcuts",
                    "  Ctrl+E            Encrypt",
                    "  Ctrl+D            Decrypt",
                    "  Ctrl+Shift+C      Copy the result",
                    "  Ctrl+O            Open a file",
                    "  Ctrl+S            Save the result",
                    "  Ctrl+L            Clear everything",
                    "  Ctrl+T            Change the theme",
                    "  Ctrl+1 / Ctrl+2   Switch between Text and Files",
                    "  F1                Open this window",
                    "",
                    "Security notes",
                    "  Modern encryption is the recommended format.",
                    "  Password strength directly affects security.",
                    "  CipherDesk has not been independently audited.",
                    "  The software is provided as is, without warranty."
                })
        };

        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        host.Controls.Add(details);

        return host;
    }

    private Control BuildButtons()
    {
        var close = new ModernButton
        {
            Text = "Close",
            Variant = ButtonVariant.Primary,
            Size = new Size(110, 36),
            DialogResult = DialogResult.OK,
            Margin = new Padding(8, 0, 0, 0)
        };

        var project = new ModernButton
        {
            Text = "Project page",
            Variant = ButtonVariant.Secondary,
            Size = new Size(130, 36),
            Margin = Padding.Empty
        };

        project.Click += (_, _) => OpenProjectPage();

        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 16, 0, 0),
            Padding = Padding.Empty
        };

        row.Controls.Add(close);
        row.Controls.Add(project);

        AcceptButton = close;
        CancelButton = close;

        return row;
    }

    private static void OpenProjectPage()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo(ProjectUrl)
                {
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
            when (ex is System.ComponentModel.Win32Exception
                or System.IO.FileNotFoundException)
        {
            // No default browser is configured.
            // Keep the dialog usable instead of throwing.
        }
    }
}
