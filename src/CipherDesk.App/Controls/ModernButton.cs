using System;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Theming;

namespace CipherDesk.App.Controls;

/// <summary>Visual weight of a button, which determines its colours.</summary>
public enum ButtonVariant
{
    /// <summary>Filled accent. One per screen area - the action the user most likely wants.</summary>
    Primary,

    /// <summary>Outlined. The equally valid alternative next to a primary action.</summary>
    Secondary,

    /// <summary>Borderless. Small utility actions such as copy, paste and clear.</summary>
    Ghost,

    /// <summary>Filled danger colour, for destructive or cancelling actions.</summary>
    Danger
}

/// <summary>
/// A flat, rounded, fully owner-drawn button.
/// </summary>
/// <remarks>
/// It derives from <see cref="Button"/> rather than <see cref="Control"/> on purpose: that keeps
/// mnemonics, tab order, <c>AcceptButton</c>/<c>CancelButton</c> wiring and the accessibility tree
/// working for free, while <see cref="OnPaint"/> replaces the classic chrome entirely.
/// </remarks>
public sealed class ModernButton : Button, IThemedControl
{
    private ThemePalette _palette = ThemeManager.Current;
    private bool _hovered;
    private bool _pressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Font = Typography.BodyStrong;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        AutoSize = false;
        Padding = new Padding(14, 0, 14, 0);
    }

    /// <summary>Colour treatment. Defaults to <see cref="ButtonVariant.Secondary"/>.</summary>
    public ButtonVariant Variant { get; set; } = ButtonVariant.Secondary;

    /// <summary>Optional icon-font code point drawn to the left of the caption. See <see cref="Glyphs"/>.</summary>
    public string? Glyph { get; set; }

    /// <summary>Corner radius in logical pixels.</summary>
    public int CornerRadius { get; set; } = 8;

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnter(EventArgs e) { Invalidate(); base.OnEnter(e); }

    protected override void OnLeave(EventArgs e) { Invalidate(); base.OnLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(PaintHelpers.EffectiveBackColor(this));

        (Color background, Color foreground, Color? border) = ResolveColors();

        float radius = LogicalToDeviceUnits(CornerRadius);
        var bounds = new RectangleF(0, 0, Width, Height);

        PaintHelpers.DrawRoundedRectangle(
            g, bounds, radius,
            background == Color.Transparent ? null : background,
            border,
            LogicalToDeviceUnits(1));

        // A visible focus ring is a hard accessibility requirement, not decoration.
        if (Focused && TabStop)
        {
            PaintHelpers.DrawRoundedRectangle(
                g, RectangleF.Inflate(bounds, -LogicalToDeviceUnits(2), -LogicalToDeviceUnits(2)),
                radius, null, _palette.Accent, LogicalToDeviceUnits(2));
        }

        DrawContent(g, foreground);
    }

    private (Color Background, Color Foreground, Color? Border) ResolveColors()
    {
        if (!Enabled)
        {
            return Variant switch
            {
                ButtonVariant.Primary or ButtonVariant.Danger =>
                    (_palette.SurfaceMuted, _palette.TextDisabled, (Color?)null),
                _ => (Color.Transparent, _palette.TextDisabled, _palette.Border)
            };
        }

        return Variant switch
        {
            ButtonVariant.Primary => (
                _pressed ? _palette.AccentPressed : _hovered ? _palette.AccentHover : _palette.Accent,
                _palette.OnAccent,
                null),

            ButtonVariant.Danger => (
                _pressed ? PaintHelpers.Blend(_palette.Danger, Color.Black, 0.2)
                         : _hovered ? PaintHelpers.Blend(_palette.Danger, Color.White, 0.1)
                         : _palette.Danger,
                Color.White,
                null),

            ButtonVariant.Ghost => (
                _pressed ? _palette.SurfaceMuted
                         : _hovered ? PaintHelpers.Blend(PaintHelpers.EffectiveBackColor(this), _palette.TextPrimary, 0.06)
                         : Color.Transparent,
                _palette.TextSecondary,
                null),

            _ => (
                _pressed ? _palette.SurfaceMuted
                         : _hovered ? PaintHelpers.Blend(_palette.Surface, _palette.Accent, 0.06)
                         : _palette.Surface,
                _palette.TextPrimary,
                _hovered ? _palette.Accent : _palette.BorderStrong)
        };
    }

    private void DrawContent(Graphics g, Color foreground)
    {
        bool hasGlyph = !string.IsNullOrEmpty(Glyph);
        bool hasText = !string.IsNullOrEmpty(Text);

        var format = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter |
                     TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

        if (hasGlyph && !hasText)
        {
            using Font iconFont = Typography.Icon(Font.SizeInPoints + 0.5f);
            TextRenderer.DrawText(g, Glyph, iconFont, ClientRectangle, foreground, format);
            return;
        }

        if (!hasGlyph)
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, foreground, format);
            return;
        }

        // Glyph plus caption: measure both, then centre the pair as a single unit.
        using Font glyphFont = Typography.Icon(Font.SizeInPoints);
        Size glyphSize = TextRenderer.MeasureText(g, Glyph, glyphFont, Size.Empty, TextFormatFlags.NoPadding);
        Size textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);

        int gap = LogicalToDeviceUnits(7);
        int totalWidth = glyphSize.Width + gap + textSize.Width;
        int left = Math.Max(Padding.Left, (Width - totalWidth) / 2);

        var glyphBounds = new Rectangle(left, 0, glyphSize.Width, Height);
        var textBounds = new Rectangle(left + glyphSize.Width + gap, 0, Width - left - glyphSize.Width - gap - Padding.Right, Height);

        TextRenderer.DrawText(g, Glyph, glyphFont, glyphBounds, foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, Text, Font, textBounds, foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
}
