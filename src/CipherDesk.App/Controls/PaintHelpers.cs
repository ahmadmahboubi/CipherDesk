using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CipherDesk.App.Controls;

/// <summary>Small drawing utilities shared by the custom controls.</summary>
internal static class PaintHelpers
{
    /// <summary>Builds a rounded rectangle path, degenerating safely to a plain rectangle at radius 0.</summary>
    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();

        if (radius <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        // Clamp so a small control cannot produce an inverted arc.
        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
        float diameter = radius * 2f;

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    /// <summary>Fills, then optionally strokes, a rounded rectangle with anti-aliasing enabled.</summary>
    public static void DrawRoundedRectangle(
        Graphics graphics, RectangleF bounds, float radius, Color? fill, Color? border, float borderWidth = 1f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        SmoothingMode previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Inset by half the stroke so the border sits inside the bounds instead of straddling them.
        float inset = border.HasValue ? borderWidth / 2f : 0f;
        RectangleF adjusted = RectangleF.Inflate(bounds, -inset, -inset);

        using GraphicsPath path = RoundedRectangle(adjusted, radius);

        if (fill.HasValue)
        {
            using var brush = new SolidBrush(fill.Value);
            graphics.FillPath(brush, path);
        }

        if (border.HasValue)
        {
            using var pen = new Pen(border.Value, borderWidth);
            graphics.DrawPath(pen, path);
        }

        graphics.SmoothingMode = previous;
    }

    /// <summary>Blends <paramref name="overlay"/> onto <paramref name="baseColor"/> at the given opacity.</summary>
    public static Color Blend(Color baseColor, Color overlay, double opacity)
    {
        opacity = Math.Clamp(opacity, 0d, 1d);
        return Color.FromArgb(
            baseColor.A,
            (int)Math.Round(baseColor.R + (overlay.R - baseColor.R) * opacity),
            (int)Math.Round(baseColor.G + (overlay.G - baseColor.G) * opacity),
            (int)Math.Round(baseColor.B + (overlay.B - baseColor.B) * opacity));
    }

    /// <summary>The colour this control should treat as "behind me" when painting rounded corners.</summary>
    public static Color EffectiveBackColor(System.Windows.Forms.Control control)
    {
        System.Windows.Forms.Control? parent = control.Parent;
        while (parent is not null)
        {
            if (parent.BackColor.A == 255) return parent.BackColor;
            parent = parent.Parent;
        }

        return Theming.ThemeManager.Current.Background;
    }
}
