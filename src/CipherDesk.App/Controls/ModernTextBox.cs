using System;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Theming;

namespace CipherDesk.App.Controls;

/// <summary>
/// A native TextBox wrapped inside a themed rounded container.
/// </summary>
public sealed class ModernTextBox : Panel, IThemedControl
{
    private ThemePalette _palette;
    private bool _focused;
    private bool _monospace;

    public ModernTextBox()
    {
        _palette = ThemeManager.Current;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        TabStop = false;

        Padding = new Padding(12, 7, 12, 7);

        Inner = new TextBox
        {
            BorderStyle = BorderStyle.None,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,

            Font = Typography.Body,

            Multiline = false,
            ScrollBars = ScrollBars.None,
            AcceptsTab = false,

            TabStop = true,
            Enabled = true,
            ReadOnly = false,

            BackColor = _palette.InputBackground,
            ForeColor = _palette.TextPrimary,

            Cursor = Cursors.IBeam
        };

        Inner.GotFocus += Inner_GotFocus;
        Inner.LostFocus += Inner_LostFocus;
        Inner.TextChanged += Inner_TextChanged;

        Controls.Add(Inner);

        UpdateHeight();
    }

    public TextBox Inner { get; }

    public override string Text
    {
        get => Inner.Text;
        set => Inner.Text = value ?? string.Empty;
    }

    public string PlaceholderText
    {
        get => Inner.PlaceholderText;
        set => Inner.PlaceholderText = value ?? string.Empty;
    }

    public bool Multiline
    {
        get => Inner.Multiline;
        set
        {
            if (Inner.Multiline == value)
                return;

            Inner.Multiline = value;
            Inner.ScrollBars = value
                ? ScrollBars.Vertical
                : ScrollBars.None;

            Inner.AcceptsTab = false;

            UpdateHeight();
            PerformLayout();
        }
    }

    public bool ReadOnly
    {
        get => Inner.ReadOnly;
        set
        {
            Inner.ReadOnly = value;
            ApplyInputColors();
        }
    }

    public bool IsPassword
    {
        get => Inner.UseSystemPasswordChar;
        set
        {
            if (Inner.UseSystemPasswordChar == value)
                return;

            Inner.UseSystemPasswordChar = value;

            Inner.Invalidate();
            Inner.Update();
        }
    }

    public bool UseMonospace
    {
        get => _monospace;
        set
        {
            if (_monospace == value)
                return;

            _monospace = value;

            Inner.Font = value
                ? Typography.Mono
                : Typography.Body;

            UpdateHeight();
            PerformLayout();
        }
    }

    public int CornerRadius { get; set; } = 10;

    public int TextLength => Inner.TextLength;

    /// <summary>
    /// Focuses the real native TextBox.
    /// </summary>
    public bool FocusPassword()
    {
        if (IsDisposed || Inner.IsDisposed)
            return false;

        // Do not rely only on Inner.Enabled.
        // A disabled parent can make the child effectively disabled.
        if (!Enabled)
            return false;

        if (!Inner.Enabled)
            Inner.Enabled = true;

        if (!Inner.CanFocus)
            return false;

        bool result = Inner.Focus();

        if (result)
        {
            _focused = true;

            Inner.SelectionStart = Inner.TextLength;
            Inner.SelectionLength = 0;
            Inner.ScrollToCaret();

            Invalidate();
            Update();
        }

        return result;
    }

    public void SelectAllText()
    {
        if (!Inner.CanFocus)
            return;

        Inner.Focus();
        Inner.SelectAll();
    }

    public void Clear()
    {
        Inner.Clear();
    }

    public void ApplyTheme(ThemePalette palette)
    {
        if (palette == null)
            throw new ArgumentNullException(nameof(palette));

        _palette = palette;

        ApplyInputColors();

        Invalidate();
        Inner.Invalidate();
    }

    private void ApplyInputColors()
    {
        if (Inner == null || Inner.IsDisposed)
            return;

        Inner.BackColor = _palette.InputBackground;

        Inner.ForeColor = ReadOnly
            ? _palette.TextSecondary
            : _palette.TextPrimary;

        Inner.Cursor = Cursors.IBeam;
    }

    private void Inner_GotFocus(object? sender, EventArgs e)
    {
        _focused = true;

        Invalidate();
        Update();
    }

    private void Inner_LostFocus(object? sender, EventArgs e)
    {
        _focused = false;

        Invalidate();
        Update();
    }

    private void Inner_TextChanged(object? sender, EventArgs e)
    {
        OnTextChanged(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);

        if (Inner == null || Inner.IsDisposed)
            return;

        // Keep native textbox synchronized with wrapper state.
        Inner.Enabled = Enabled;

        Invalidate();
        Inner.Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        FocusPassword();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        if (Inner != null &&
            !Inner.IsDisposed &&
            Controls.Contains(Inner))
        {
            Inner.BringToFront();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (Inner != null && !Inner.IsDisposed)
            UpdateHeight();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);

        if (Inner != null && !Inner.IsDisposed)
            UpdateHeight();
    }

    private void UpdateHeight()
    {
        if (Inner == null || Inner.IsDisposed)
            return;

        if (Inner.Multiline)
        {
            AutoSize = false;
            MinimumSize = new Size(
                0,
                LogicalToDeviceUnits(72));

            return;
        }

        AutoSize = false;

        int textHeight = Math.Max(
            Inner.PreferredHeight,
            LogicalToDeviceUnits(22));

        int height = textHeight + Padding.Vertical;

        MinimumSize = new Size(0, height);

        if (Height != height)
            Height = height;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (Width <= 0 || Height <= 0)
            return;

        Color borderColor = _focused
            ? _palette.Accent
            : _palette.InputBorder;

        PaintHelpers.DrawRoundedRectangle(
            e.Graphics,
            new RectangleF(
                0,
                0,
                Width - 1,
                Height - 1),
            LogicalToDeviceUnits(CornerRadius),
            _palette.InputBackground,
            borderColor,
            LogicalToDeviceUnits(_focused ? 2 : 1));
    }
}
