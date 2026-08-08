using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Theming;

namespace CipherDesk.App.Controls;

/// <summary>A single item in a <see cref="SegmentedControl"/>.</summary>
public sealed record Segment(string Text, string? Glyph = null, string? Tooltip = null);

/// <summary>
/// A pill-shaped set of mutually exclusive options - the modern replacement for a row of
/// radio buttons or a <see cref="TabControl"/> header.
/// </summary>
public sealed class SegmentedControl : Control, IThemedControl
{
    private readonly List<Segment> _segments = new();
    private ThemePalette _palette = ThemeManager.Current;
    private int _selectedIndex;
    private int _hoveredIndex = -1;

    public SegmentedControl()
    {
        // C#
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent; BackColor = Color.Transparent;
        Font = Typography.Subheading;
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = 36;
    }

    public event EventHandler? SelectedIndexChanged;

    public IReadOnlyList<Segment> Segments => _segments;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(_segments.Count - 1, 0));
            if (clamped == _selectedIndex) return;

            _selectedIndex = clamped;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetSegments(params Segment[] segments)
    {
        _segments.Clear();
        _segments.AddRange(segments);
        _selectedIndex = 0;
        Invalidate();
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int index = IndexAt(e.X);
        if (index != _hoveredIndex) { _hoveredIndex = index; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoveredIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        int index = IndexAt(e.X);
        if (index >= 0) SelectedIndex = index;
        base.OnMouseDown(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Arrow keys move between options, matching how radio groups behave everywhere else.
        if (e.KeyCode == Keys.Left) { SelectedIndex = Math.Max(0, SelectedIndex - 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Right) { SelectedIndex = Math.Min(_segments.Count - 1, SelectedIndex + 1); e.Handled = true; }

        base.OnKeyDown(e);
    }

    protected override void OnEnter(EventArgs e) { Invalidate(); base.OnEnter(e); }

    protected override void OnLeave(EventArgs e) { Invalidate(); base.OnLeave(e); }

    private int IndexAt(int x)
    {
        if (_segments.Count == 0) return -1;
        int width = Width / _segments.Count;
        if (width <= 0) return -1;
        return Math.Clamp(x / width, 0, _segments.Count - 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(PaintHelpers.EffectiveBackColor(this));

        if (_segments.Count == 0) return;

        float radius = Height / 2f;
        PaintHelpers.DrawRoundedRectangle(
            g, new RectangleF(0, 0, Width, Height), radius, _palette.SurfaceMuted, _palette.Border, LogicalToDeviceUnits(1));

        int segmentWidth = Width / _segments.Count;
        int inset = LogicalToDeviceUnits(3);

        for (int i = 0; i < _segments.Count; i++)
        {
            var cell = new Rectangle(i * segmentWidth, 0, segmentWidth, Height);
            bool selected = i == _selectedIndex;

            if (selected)
            {
                PaintHelpers.DrawRoundedRectangle(
                    g, RectangleF.Inflate(cell, -inset, -inset), (Height - inset * 2) / 2f,
                    _palette.Surface, _palette.BorderStrong, LogicalToDeviceUnits(1));
            }
            else if (i == _hoveredIndex)
            {
                PaintHelpers.DrawRoundedRectangle(
                    g, RectangleF.Inflate(cell, -inset, -inset), (Height - inset * 2) / 2f,
                    PaintHelpers.Blend(_palette.SurfaceMuted, _palette.TextPrimary, 0.05), null);
            }

            Color foreground = selected ? _palette.TextPrimary : _palette.TextSecondary;
            DrawSegmentContent(g, _segments[i], cell, foreground);
        }

        if (Focused)
        {
            PaintHelpers.DrawRoundedRectangle(
                g, new RectangleF(0, 0, Width, Height), radius, null, _palette.Accent, LogicalToDeviceUnits(2));
        }
    }

    private void DrawSegmentContent(Graphics g, Segment segment, Rectangle cell, Color foreground)
    {
        const TextFormatFlags Centered =
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;

        if (string.IsNullOrEmpty(segment.Glyph))
        {
            TextRenderer.DrawText(g, segment.Text, Font, cell, foreground, Centered);
            return;
        }

        using Font glyphFont = Typography.Icon(Font.SizeInPoints);
        Size glyphSize = TextRenderer.MeasureText(g, segment.Glyph, glyphFont, Size.Empty, TextFormatFlags.NoPadding);
        Size textSize = TextRenderer.MeasureText(g, segment.Text, Font, Size.Empty, TextFormatFlags.NoPadding);

        int gap = LogicalToDeviceUnits(6);
        int left = cell.X + (cell.Width - (glyphSize.Width + gap + textSize.Width)) / 2;

        TextRenderer.DrawText(g, segment.Glyph, glyphFont,
            new Rectangle(left, cell.Y, glyphSize.Width, cell.Height), foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(g, segment.Text, Font,
            new Rectangle(left + glyphSize.Width + gap, cell.Y, textSize.Width, cell.Height), foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }
}
