using System;
using System.Drawing;
using System.Windows.Forms;
using CipherDesk.App.Services;
using CipherDesk.App.Theming;
using CipherDesk.Core.Passwords;

namespace CipherDesk.App.Controls;

/// <summary>
/// Password entry with reveal, generation and live strength measurement.
/// </summary>
public sealed class PasswordPanel : UserControl, IThemedControl
{
    private readonly ModernTextBox _passwordBox;
    private readonly ModernButton _revealButton;
    private readonly ModernButton _generateButton;
    private readonly PasswordStrengthMeter _meter;
    private readonly ToolTip _toolTip;

    public PasswordPanel()
    {
        BackColor = Color.Transparent;

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // Explicitly make this control enabled.
        Enabled = true;

        _toolTip = new ToolTip
        {
            InitialDelay = 400,
            ReshowDelay = 150
        };

        _passwordBox = new ModernTextBox
        {
            Dock = DockStyle.Fill,
            Enabled = true,
            IsPassword = true,
            UseMonospace = true,
            PlaceholderText = "Enter a password",
            Margin = new Padding(0, 0, 8, 0)
        };

        _passwordBox.Inner.TextChanged += OnPasswordChanged;

        _revealButton = new ModernButton
        {
            Glyph = Glyphs.Eye,
            Variant = ButtonVariant.Ghost,
            Size = new Size(38, 38),
            Margin = new Padding(0, 0, 4, 0),
            TabStop = false,
            Enabled = true
        };

        _revealButton.Click += (_, _) =>
            TogglePasswordVisibility();

        _toolTip.SetToolTip(
            _revealButton,
            "Show or hide the password");

        _generateButton = new ModernButton
        {
            Glyph = Glyphs.Dice,
            Variant = ButtonVariant.Ghost,
            Size = new Size(38, 38),
            Margin = Padding.Empty,
            TabStop = false,
            Enabled = true
        };

        _generateButton.Click += (_, _) =>
            GenerateStrongPassword();

        _toolTip.SetToolTip(
            _generateButton,
            "Generate a strong random password");

        _meter = new PasswordStrengthMeter
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 0)
        };

        // ------------------------------------------------------------
        // Password row
        // ------------------------------------------------------------

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        row.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100f));

        row.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        row.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        row.Controls.Add(
            _passwordBox,
            0,
            0);

        row.Controls.Add(
            _revealButton,
            1,
            0);

        row.Controls.Add(
            _generateButton,
            2,
            0);

        // ------------------------------------------------------------
        // Main layout
        // ------------------------------------------------------------

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100f));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        layout.Controls.Add(row, 0, 0);
        layout.Controls.Add(_meter, 0, 1);

        Controls.Add(layout);
    }

    public event EventHandler? PasswordChanged;

    public bool HasPassword =>
        _passwordBox.TextLength > 0;

    public PasswordStrength Strength =>
        _meter.Assessment.Strength;

    public SecureTextBoxReader.PasswordScope AcquirePassword() =>
        new(_passwordBox.Inner);

    public void FocusPassword()
    {
        _passwordBox.FocusPassword();
    }

    public void ClearPassword()
    {
        _passwordBox.Clear();

        if (!_passwordBox.IsPassword)
            TogglePasswordVisibility();
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = Color.Transparent;
    }

    private void OnPasswordChanged(
        object? sender,
        EventArgs e)
    {
        using SecureTextBoxReader.PasswordScope scope =
            AcquirePassword();

        _meter.Update(scope.Password);

        PasswordChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void TogglePasswordVisibility()
    {
        _passwordBox.IsPassword =
            !_passwordBox.IsPassword;

        _revealButton.Variant =
            _passwordBox.IsPassword
                ? ButtonVariant.Ghost
                : ButtonVariant.Secondary;

        _revealButton.Invalidate();

        _passwordBox.FocusPassword();
    }

    private void GenerateStrongPassword()
    {
        char[] generated =
            PasswordGenerator.Generate();

        try
        {
            _passwordBox.Text =
                new string(generated);

            if (_passwordBox.IsPassword)
                TogglePasswordVisibility();
        }
        finally
        {
            SecureTextBoxReader.Wipe(generated);
        }
    }
}
