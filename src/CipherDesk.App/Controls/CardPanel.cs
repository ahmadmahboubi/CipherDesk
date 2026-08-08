using System;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Theming;

namespace CipherDesk.App.Controls;

/// <summary>
/// A rounded surface with an optional title row and an action area on the right.
/// </summary>
public sealed class CardPanel : Panel, IThemedControl
{
    private readonly TableLayoutPanel _layout;
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly FlowLayoutPanel _actions;

    private ThemePalette _palette = ThemeManager.Current;

    public CardPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;

        Padding = new Padding(14, 12, 14, 14);

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // ------------------------------------------------------------
        // Title
        // ------------------------------------------------------------

        _titleLabel = new Label
        {
            AutoSize = true,
            Font = Typography.Subheading,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 4, 0, 0)
        };

        // ------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------

        _actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // ------------------------------------------------------------
        // Header
        // ------------------------------------------------------------

        _header = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 30,
            MinimumSize = new Size(0, 30),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };

        _header.Controls.Add(_titleLabel);
        _header.Controls.Add(_actions);

        // ------------------------------------------------------------
        // Body
        // ------------------------------------------------------------

        Body = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // ------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,

            ColumnCount = 1,
            RowCount = 2,

            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100f));

        // Header has natural height.
        _layout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        // Body also has natural height.
        _layout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        _layout.Controls.Add(
            _header,
            0,
            0);

        _layout.Controls.Add(
            Body,
            0,
            1);

        Controls.Add(_layout);

        // The card itself should be interactive-neutral.
        Enabled = true;
    }

    public Panel Body { get; }

    public string Title
    {
        get => _titleLabel.Text;

        set
        {
            _titleLabel.Text =
                value ?? string.Empty;

            _header.Visible =
                !string.IsNullOrEmpty(
                    _titleLabel.Text)
                || _actions.Controls.Count > 0;

            PerformLayout();
        }
    }

    public int CornerRadius { get; set; } = 12;

    public void AddAction(Control control)
    {
        control.Margin =
            new Padding(6, 0, 0, 0);

        _actions.Controls.Add(control);

        _header.Visible = true;

        PerformLayout();
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;

        _titleLabel.ForeColor =
            palette.TextSecondary;

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(
            PaintHelpers.EffectiveBackColor(this));

        PaintHelpers.DrawRoundedRectangle(
            e.Graphics,
            new RectangleF(
                0,
                0,
                Math.Max(0, Width - 1),
                Math.Max(0, Height - 1)),
            LogicalToDeviceUnits(CornerRadius),
            _palette.Surface,
            _palette.Border,
            LogicalToDeviceUnits(1));

        base.OnPaint(e);
    }
}
