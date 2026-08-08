using System;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Theming;

namespace CipherDesk.App.Controls;

/// <summary>A slim, rounded, themed progress bar with an optional indeterminate sweep.</summary>
public sealed class ModernProgressBar : Control, IThemedControl
{
    private readonly Timer _animation;
    private ThemePalette _palette = ThemeManager.Current;
    private double _value;
    private bool _indeterminate;
    private float _sweepPosition;

    public ModernProgressBar()
    {
        // C#
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent; BackColor = Color.Transparent;
        Height = 6;
        TabStop = false;

        _animation = new Timer { Interval = 16 }; // ~60 fps
        _animation.Tick += (_, _) =>
        {
            _sweepPosition = (_sweepPosition + 0.012f) % 1.4f;
            Invalidate();
        };
    }

    /// <summary>Progress from 0 to 1.</summary>
    public double Value
    {
        get => _value;
        set
        {
            double clamped = Math.Clamp(value, 0d, 1d);
            if (Math.Abs(clamped - _value) < 0.0005) return;
            _value = clamped;
            Invalidate();
        }
    }

    public bool Indeterminate
    {
        get => _indeterminate;
        set
        {
            if (_indeterminate == value) return;
            _indeterminate = value;
            _animation.Enabled = value;
            Invalidate();
        }
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(PaintHelpers.EffectiveBackColor(this));

        float radius = Height / 2f;
        var track = new RectangleF(0, 0, Width, Height);
        PaintHelpers.DrawRoundedRectangle(g, track, radius, _palette.SurfaceMuted, null);

        if (_indeterminate)
        {
            float sweepWidth = Width * 0.28f;
            float x = (_sweepPosition - 0.2f) * Width;
            var sweep = new RectangleF(Math.Max(x, 0), 0, Math.Min(sweepWidth, Width - Math.Max(x, 0)), Height);
            if (sweep.Width > 0)
                PaintHelpers.DrawRoundedRectangle(g, sweep, radius, _palette.Accent, null);

            return;
        }

        float filled = (float)(Width * _value);
        if (filled > 0.5f)
            PaintHelpers.DrawRoundedRectangle(g, new RectangleF(0, 0, filled, Height), radius, _palette.Accent, null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animation.Dispose();
        base.Dispose(disposing);
    }
}
