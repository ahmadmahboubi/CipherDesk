using System;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Theming;
using CipherDesk.Core.Passwords;

namespace CipherDesk.App.Controls;

/// <summary>
/// Five-segment strength meter with a label and one line of advice.
/// </summary>
/// <remarks>
/// Deliberately advisory: it never blocks a weak password, because a tool that refuses to encrypt
/// pushes people towards not encrypting at all. It tells the truth and gets out of the way.
/// </remarks>
public sealed class PasswordStrengthMeter : Control, IThemedControl
{
    private const int SegmentCount = 5;

    private ThemePalette _palette = ThemeManager.Current;
    private PasswordAssessment _assessment = new(PasswordStrength.Empty, 0, null);

    public PasswordStrengthMeter()
    {
        // C#
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent;
        Font = Typography.Caption;
        Height = 38;
        TabStop = false;
    }

    /// <summary>Re-evaluates from the live password buffer without copying it into a string.</summary>
    public void Update(ReadOnlySpan<char> password)
    {
        _assessment = PasswordStrengthEvaluator.Evaluate(password);
        Invalidate();
    }

    public PasswordAssessment Assessment => _assessment;

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(PaintHelpers.EffectiveBackColor(this));

        int filled = (int)_assessment.Strength;
        Color activeColor = ColorFor(_assessment.Strength);

        int gap = LogicalToDeviceUnits(4);
        int barHeight = LogicalToDeviceUnits(4);
        int totalWidth = Width - gap * (SegmentCount - 1);
        int segmentWidth = Math.Max(totalWidth / SegmentCount, 1);

        for (int i = 0; i < SegmentCount; i++)
        {
            var bar = new RectangleF(i * (segmentWidth + gap), 0, segmentWidth, barHeight);
            Color color = i < filled ? activeColor : _palette.SurfaceMuted;
            PaintHelpers.DrawRoundedRectangle(g, bar, barHeight / 2f, color, null);
        }

        var textArea = new Rectangle(0, barHeight + LogicalToDeviceUnits(6), Width, Height - barHeight - LogicalToDeviceUnits(6));

        string label = _assessment.Strength == PasswordStrength.Empty
            ? " Password is empty!"
            : $"{_assessment.Label}  ~{_assessment.EntropyBits:F0} bits";

        TextRenderer.DrawText(g, label, Typography.Caption, textArea,
            _assessment.Strength == PasswordStrength.Empty ? _palette.TextSecondary : activeColor,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);

        if (_assessment.Advice is { Length: > 0 } advice)
        {
            TextRenderer.DrawText(g, advice, Typography.Caption, textArea, _palette.TextSecondary,
                TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    private Color ColorFor(PasswordStrength strength) => strength switch
    {
        PasswordStrength.Empty => _palette.SurfaceMuted,
        PasswordStrength.VeryWeak or PasswordStrength.Weak => _palette.Danger,
        PasswordStrength.Fair => _palette.Warning,
        _ => _palette.Success
    };
}
