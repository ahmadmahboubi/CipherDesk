using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using CipherDesk.App.Controls;
using CipherDesk.App.Dialogs;
using CipherDesk.App.Services;
using CipherDesk.App.Theming;
using CipherDesk.App.Views;
using CipherDesk.Core.Files;
using CipherDesk.Core.Text;

namespace CipherDesk.App.Forms;

/// <summary>
/// The application shell: header, workspace and status bar.
/// </summary>
/// <remarks>
/// The form owns chrome, theming and keyboard routing only. All encryption behaviour lives in the
/// views, and all cryptography lives in CipherDesk.Core - the form never touches a key or a cipher.
/// </remarks>
public sealed class MainForm : Form, IAppShell
{
    private readonly AppSettings _settings;
    private readonly ToastHost _toast = new();
    private readonly ToolTip _toolTip = new() { InitialDelay = 400 };

    private readonly SegmentedControl _workspaceSelector = new();
    private readonly Panel _workspaceHost = new();
    private readonly Label _statusLabel = new();
    private readonly Label _formatBadge = new();
    private readonly Label _shortcutHint = new();
    private readonly ModernProgressBar _busyIndicator = new();
    private readonly ModernButton _themeButton = new();
    private readonly ModernButton _aboutButton = new();

    private readonly TextCipherView _textView;
    private readonly FileCipherView _fileView;

    public MainForm(AppSettings settings)
    {
        _settings = settings;

        _textView = new TextCipherView(this, settings, new TextCipherRouter());
        _fileView = new FileCipherView(this, new FileCipher());

        InitializeShell();
        BuildLayout();

        ThemeManager.Changed += OnThemeChanged;
        ApplyTheme();

        ShowWorkspace(0);
        SetStatus("Ready.");
    }

    // ---- shell construction -----------------------------------------------------------------

    private void InitializeShell()
    {
        Text = "CipherDesk";
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Typography.Body;
        MinimumSize = new Size(760, 640);
        ClientSize = new Size(_settings.WindowWidth, _settings.WindowHeight);
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(20, 16, 20, 12);
        AllowDrop = true;
        KeyPreview = true;
        DoubleBuffered = true;

        Icon = LoadApplicationIcon();

        if (_settings.WindowMaximized) WindowState = FormWindowState.Maximized;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // workspace
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // status bar

        root.Controls.Add(BuildHeader(), 0, 0);

        _workspaceHost.Dock = DockStyle.Fill;
        _workspaceHost.BackColor = Color.Transparent;
        _workspaceHost.Margin = new Padding(0, 14, 0, 10);
        _workspaceHost.Controls.Add(_textView);
        _workspaceHost.Controls.Add(_fileView);
        root.Controls.Add(_workspaceHost, 0, 1);

        root.Controls.Add(BuildStatusBar(), 0, 2);

        Controls.Add(root);
        _toast.Attach(this);
    }

    private Control BuildHeader()
    {
        var title = new Label
        {
            Text = "CipherDesk",
            Font = Typography.Display,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 1)
        };

        var subtitle = new Label
        {
            Text = "AES-256 encryption for text and files",
            Font = Typography.Caption,
            Tag = "secondary",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0)
        };

        var titleStack = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Dock = DockStyle.Left
        };
        titleStack.Controls.Add(title, 0, 0);
        titleStack.Controls.Add(subtitle, 0, 1);

        _workspaceSelector.SetSegments(
            new Segment("Text", Glyphs.Text));
            //new Segment("Files", Glyphs.Files));
        _workspaceSelector.Width = 220;
        _workspaceSelector.Height = 38;
        _workspaceSelector.Margin = new Padding(0, 0, 12, 0);
        _workspaceSelector.SelectedIndexChanged += (_, _) => ShowWorkspace(_workspaceSelector.SelectedIndex);

        _themeButton.Glyph = Glyphs.Sun;
        _themeButton.Variant = ButtonVariant.Ghost;
        _themeButton.Size = new Size(38, 38);
        _themeButton.Margin = new Padding(0, 0, 4, 0);
        _themeButton.TabStop = false;
        _themeButton.Click += (_, _) => CycleTheme();

        _aboutButton.Glyph = Glyphs.Info;
        _aboutButton.Variant = ButtonVariant.Ghost;
        _aboutButton.Size = new Size(38, 38);
        _aboutButton.Margin = Padding.Empty;
        _aboutButton.TabStop = false;
        _aboutButton.Click += (_, _) => new AboutDialog().ShowDialog(this);
        _toolTip.SetToolTip(_aboutButton, "About CipherDesk (F1)");

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 0, 0)
        };
        right.Controls.Add(_workspaceSelector);
        right.Controls.Add(_themeButton);
        right.Controls.Add(_aboutButton);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(titleStack, 0, 0);
        header.Controls.Add(right, 1, 0);

        return header;
    }

    private Control BuildStatusBar()
    {
        _statusLabel.AutoSize = true;
        _statusLabel.Font = Typography.Caption;
        _statusLabel.Tag = "secondary";
        _statusLabel.Margin = new Padding(0, 5, 12, 0);

        _busyIndicator.Width = 90;
        _busyIndicator.Height = 4;
        _busyIndicator.Visible = false;
        _busyIndicator.Margin = new Padding(0, 11, 12, 0);

        _formatBadge.AutoSize = true;
        _formatBadge.Font = Typography.Caption;
        _formatBadge.Margin = new Padding(12, 5, 0, 0);

        _shortcutHint.AutoSize = true;
        _shortcutHint.Font = Typography.Caption;
        _shortcutHint.Tag = "secondary";
        _shortcutHint.Text = "Ctrl+E encrypt  \u00b7  Ctrl+D decrypt  \u00b7  F1 help";
        _shortcutHint.Margin = new Padding(16, 5, 0, 0);

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        left.Controls.Add(_busyIndicator);
        left.Controls.Add(_statusLabel);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        right.Controls.Add(_formatBadge);
        right.Controls.Add(_shortcutHint);

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Height = 26
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.Controls.Add(left, 0, 0);
        bar.Controls.Add(right, 1, 0);

        return bar;
    }

    // ---- IAppShell ---------------------------------------------------------------------------

    public void Notify(string message, ToastKind kind = ToastKind.Info) => _toast.ShowToast(message, kind);

    public void SetStatus(string message) => _statusLabel.Text = message;

    public void SetBusy(bool busy)
    {
        _busyIndicator.Visible = busy;
        _busyIndicator.Indeterminate = busy;
    }

    public void SetFormatBadge(string text)
    {
        _formatBadge.Text = text;
        _formatBadge.ForeColor = text.Contains("Legacy", StringComparison.OrdinalIgnoreCase)
            ? ThemeManager.Current.Warning
            : ThemeManager.Current.Success;
    }

    // ---- workspace switching --------------------------------------------------------------------

    private void ShowWorkspace(int index)
    {
        _textView.Visible = index == 0;
        _fileView.Visible = index == 1;

        if (index == 0)
        {
            _textView.BringToFront();
            SetFormatBadge(GetFormatBadgeText(_textView.SelectedFormat));
        }
    }

    private static string GetFormatBadgeText(Core.CipherFormat format) =>
        format switch
        {
            Core.CipherFormat.Modern => "AES-256-GCM",
            Core.CipherFormat.Legacy => "Legacy v1 (insecure)",
            Core.CipherFormat.CBC => "AES-256-CBC",
            _ => "AES-256-GCM"
        };

    // ---- theming ---------------------------------------------------------------------------------

    private void CycleTheme()
    {
        ThemeMode mode = ThemeManager.CycleMode();
        _settings.Theme = mode;

        string label = mode switch
        {
            ThemeMode.Light => "Light theme",
            ThemeMode.Dark => "Dark theme",
            _ => "Following the system theme"
        };

        Notify(label);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyTheme();

    private void ApplyTheme()
    {
        ThemePalette palette = ThemeManager.Current;

        SuspendLayout();
        ThemeManager.Apply(this);
        ResumeLayout(true);

        _themeButton.Glyph = palette.IsDark ? Glyphs.Moon : Glyphs.Sun;
        _toolTip.SetToolTip(_themeButton, $"Theme: {ThemeManager.Mode} (Ctrl+T to change)");

        if (IsHandleCreated) DwmWindowTheme.Apply(Handle, palette);

        SetFormatBadge(_formatBadge.Text.Length > 0 ? _formatBadge.Text : "AES-256-GCM");
        Invalidate(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DwmWindowTheme.Apply(Handle, ThemeManager.Current);
    }

    // ---- keyboard ------------------------------------------------------------------------------

    /// <summary>
    /// Central shortcut routing. Using ProcessCmdKey rather than KeyDown means shortcuts still fire
    /// while a text box has focus, which is where the user usually is.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        bool textWorkspace = _textView.Visible;

        switch (keyData)
        {
            case Keys.Control | Keys.E:
                if (textWorkspace) _ = _textView.EncryptAsync();
                return true;

            case Keys.Control | Keys.D:
                if (textWorkspace) _ = _textView.DecryptAsync();
                return true;

            case Keys.Control | Keys.Shift | Keys.C:
                if (textWorkspace) _textView.CopyOutput();
                return true;

            case Keys.Control | Keys.L:
                if (textWorkspace) _textView.ClearAll();
                return true;

            case Keys.Control | Keys.O:
                if (textWorkspace) _textView.LoadTextFile(); else _fileView.BrowseForSource();
                return true;

            case Keys.Control | Keys.S:
                if (textWorkspace) _textView.SaveOutputToFile();
                return true;

            case Keys.Control | Keys.T:
                CycleTheme();
                return true;

            case Keys.Control | Keys.D1:
                _workspaceSelector.SelectedIndex = 0;
                return true;

            case Keys.Control | Keys.D2:
                _workspaceSelector.SelectedIndex = 1;
                return true;

            case Keys.F1:
                new AboutDialog().ShowDialog(this);
                return true;
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    // ---- lifetime -------------------------------------------------------------------------------

    private static Icon? LoadApplicationIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
        }
        catch (Exception ex) when (ex is System.IO.IOException or ArgumentException or NotSupportedException)
        {
            return null; // the default WinForms icon is a perfectly acceptable fallback
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _settings.WindowMaximized = WindowState == FormWindowState.Maximized;

        if (WindowState == FormWindowState.Normal)
        {
            _settings.WindowWidth = ClientSize.Width;
            _settings.WindowHeight = ClientSize.Height;
        }

        _settings.Save();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Changed -= OnThemeChanged;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
