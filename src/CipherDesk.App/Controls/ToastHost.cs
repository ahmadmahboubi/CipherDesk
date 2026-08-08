using System;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Theming;

namespace CipherDesk.App.Controls;

public enum ToastKind { Info, Success, Warning, Error }

/// <summary>
/// Transient, non-blocking feedback shown in the bottom right of the window.
/// </summary>
/// <remarks>
/// Replaces the modal message box for successful outcomes. A confirmation the user has to dismiss
/// costs a click and a context switch for information they already expected; a toast does not.
/// Modal dialogs are reserved for things that genuinely need a decision.
/// </remarks>
public sealed class ToastHost : Control, IThemedControl
{
    private const int SlideDistance = 12;
    private const int MaxWidth = 460;

    private readonly Timer _dismissTimer;
    private readonly Timer _animationTimer;

    private ThemePalette _palette = ThemeManager.Current;
    private string _message = string.Empty;
    private ToastKind _kind = ToastKind.Info;
    private float _animation;      // 0 = hidden, 1 = fully shown
    private bool _closing;

    public ToastHost()
    {
        // C#
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent; BackColor = Color.Transparent;
        Font = Typography.Body;
        Visible = false;
        TabStop = false;
        Cursor = Cursors.Hand;

        _dismissTimer = new Timer();
        _dismissTimer.Tick += (_, _) => { _dismissTimer.Stop(); BeginClose(); };

        _animationTimer = new Timer { Interval = 15 };
        _animationTimer.Tick += OnAnimationTick;
    }

    /// <summary>Docks the toast into a parent and keeps it anchored to the bottom right.</summary>
    public void Attach(Control parent)
    {
        parent.Controls.Add(this);
        BringToFront();
        parent.Resize += (_, _) => Reposition();
    }

    public void ShowToast(string message, ToastKind kind = ToastKind.Info, int durationMilliseconds = 3000)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        _message = message;
        _kind = kind;
        _closing = false;

        Measure();
        Reposition();

        Visible = true;
        BringToFront();

        _dismissTimer.Stop();
        _dismissTimer.Interval = Math.Max(durationMilliseconds, 1200);
        _dismissTimer.Start();
        _animationTimer.Start();
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        BeginClose();
        base.OnMouseClick(e);
    }

    private void BeginClose()
    {
        _closing = true;
        _animationTimer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        _animation += _closing ? -0.12f : 0.12f;

        if (_animation >= 1f)
        {
            _animation = 1f;
            _animationTimer.Stop();
        }
        else if (_animation <= 0f)
        {
            _animation = 0f;
            _animationTimer.Stop();
            Visible = false;
        }

        Reposition();
        Invalidate();
    }

    private void Measure()
    {
        int glyphWidth = LogicalToDeviceUnits(26);
        int horizontalPadding = LogicalToDeviceUnits(32);

        Size textSize = TextRenderer.MeasureText(
            _message, Font, new Size(LogicalToDeviceUnits(MaxWidth), int.MaxValue), TextFormatFlags.WordBreak);

        Width = Math.Min(textSize.Width + glyphWidth + horizontalPadding, LogicalToDeviceUnits(MaxWidth));
        Height = Math.Max(textSize.Height + LogicalToDeviceUnits(22), LogicalToDeviceUnits(46));
    }

    private void Reposition()
    {
        if (Parent is null) return;

        int margin = LogicalToDeviceUnits(18);
        int offset = (int)((1f - _animation) * LogicalToDeviceUnits(SlideDistance));

        Location = new Point(
            Parent.ClientSize.Width - Width - margin,
            Parent.ClientSize.Height - Height - margin + offset);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(PaintHelpers.EffectiveBackColor(this));

        (Color accent, string glyph) = _kind switch
        {
            ToastKind.Success => (_palette.Success, Glyphs.Success),
            ToastKind.Warning => (_palette.Warning, Glyphs.Warning),
            ToastKind.Error => (_palette.Danger, Glyphs.Error),
            _ => (_palette.Accent, Glyphs.Info)
        };

        var bounds = new RectangleF(0, 0, Width, Height);
        PaintHelpers.DrawRoundedRectangle(g, bounds, LogicalToDeviceUnits(10),
            _palette.Surface, PaintHelpers.Blend(_palette.Border, accent, 0.45), LogicalToDeviceUnits(1));

        // A coloured rail on the left carries the semantics without shouting.
        using (var railPath = PaintHelpers.RoundedRectangle(
                   new RectangleF(0, 0, LogicalToDeviceUnits(4), Height), LogicalToDeviceUnits(2)))
        using (var brush = new SolidBrush(accent))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillPath(brush, railPath);
        }

        int left = LogicalToDeviceUnits(14);
        using Font iconFont = Typography.Icon(11f);
        Size glyphSize = TextRenderer.MeasureText(g, glyph, iconFont, Size.Empty, TextFormatFlags.NoPadding);

        TextRenderer.DrawText(g, glyph, iconFont,
            new Rectangle(left, 0, glyphSize.Width, Height), accent,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

        int textLeft = left + glyphSize.Width + LogicalToDeviceUnits(10);
        TextRenderer.DrawText(g, _message, Font,
            new Rectangle(textLeft, LogicalToDeviceUnits(2), Width - textLeft - LogicalToDeviceUnits(12), Height - LogicalToDeviceUnits(4)),
            _palette.TextPrimary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dismissTimer.Dispose();
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
