using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CipherDesk.App.Controls;
using CipherDesk.App.Dialogs;
using CipherDesk.App.Services;
using CipherDesk.App.Theming;
using CipherDesk.Core;
using CipherDesk.Core.Passwords;
using CipherDesk.Core.Text;

namespace CipherDesk.App.Views;

/// <summary>
/// The text workflow: type or paste something, encrypt or decrypt it, copy the result out.
/// </summary>
public sealed class TextCipherView : UserControl
{
    private const int MaxTextFileBytes = 8 * 1024 * 1024;

    private readonly IAppShell _shell;
    private readonly AppSettings _settings;
    private readonly TextCipherRouter _cipher;
    private readonly ToolTip _toolTip;

    private readonly ModernTextBox _inputBox;
    private readonly ModernTextBox _outputBox;
    private readonly PasswordPanel _passwordPanel;
    private readonly SegmentedControl _formatSelector;
    private readonly ModernButton _encryptButton;
    private readonly ModernButton _decryptButton;
    private readonly Label _inputCounter;

    private bool _busy;

    public TextCipherView(
        IAppShell shell,
        AppSettings settings,
        TextCipherRouter cipher)
    {
        _shell = shell;
        _settings = settings;
        _cipher = cipher;

        BackColor = Color.Transparent;
        Dock = DockStyle.Fill;
        AllowDrop = true;

        _toolTip = new ToolTip
        {
            InitialDelay = 400,
            ReshowDelay = 150,
            AutoPopDelay = 8000
        };

        _inputCounter = new Label
        {
            AutoSize = true,
            Font = Typography.Caption,
            Tag = "secondary",
            Margin = new Padding(0, 8, 8, 0),
            Text = "0 characters"
        };

        _inputBox = CreateTextArea(
            "Type or paste text here, or drop a file onto the window");

        _inputBox.Inner.TextChanged +=
            (_, _) => UpdateInputState();

        _outputBox = CreateTextArea(
            "The result appears here");

        _outputBox.ReadOnly = true;
        _outputBox.UseMonospace = true;

        _passwordPanel = new PasswordPanel
        {
            Dock = DockStyle.Fill
        };

        _passwordPanel.PasswordChanged +=
            (_, _) => UpdateInputState();

        _formatSelector = new SegmentedControl
        {
            Dock = DockStyle.Fill,
            Height = 38
        };

        _formatSelector.SetSegments(
            new Segment(
                "AES-256-GCM",
                Glyphs.Shield,
                "Recommended. Salted, authenticated and tamper evident."),

            new Segment(
                "Legacy v1",
                Glyphs.Warning,
                "Legacy CipherDesk format. Only for compatibility with older data."),

            new Segment(
                "AES-256-CBC",
                Glyphs.Files,
                "Legacy compatibility format used by the original external application. " +
                "Use only when required.")
        );

        _formatSelector.SelectedIndex =
            GetInitialFormatIndex();

        _formatSelector.SelectedIndexChanged +=
            OnFormatChanged;

        _encryptButton = new ModernButton
        {
            Text = "Encrypt",
            Glyph = Glyphs.Lock,
            Variant = ButtonVariant.Primary,
            Size = new Size(140, 40),
            Margin = new Padding(8, 0, 0, 0)
        };

        _encryptButton.Click +=
            async (_, _) =>
                await RunAsync(encrypting: true)
                    .ConfigureAwait(true);

        _toolTip.SetToolTip(
            _encryptButton,
            "Encrypt the input (Ctrl+E)");

        _decryptButton = new ModernButton
        {
            Text = "Decrypt",
            Glyph = Glyphs.Unlock,
            Variant = ButtonVariant.Secondary,
            Size = new Size(140, 40),
            Margin = new Padding(8, 0, 0, 0)
        };

        _decryptButton.Click +=
            async (_, _) =>
                await RunAsync(encrypting: false)
                    .ConfigureAwait(true);

        _toolTip.SetToolTip(
            _decryptButton,
            "Decrypt the input (Ctrl+D)");

        Controls.Add(
            BuildLayout());

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        UpdateInputState();
        UpdateFormatBadge();
    }

    // ---- construction --------------------------------------------------------------------

    private ModernTextBox CreateTextArea(
        string placeholder) =>
        new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            PlaceholderText = placeholder,
            Margin = Padding.Empty
        };

    private Control BuildLayout()
    {
        CardPanel inputCard =
            BuildInputCard();

        CardPanel passwordCard =
            BuildPasswordCard();

        Control actionBar =
            BuildActionBar();

        CardPanel outputCard =
            BuildOutputCard();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100f));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                50f));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.AutoSize));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                50f));

        layout.Controls.Add(
            inputCard,
            0,
            0);

        layout.Controls.Add(
            passwordCard,
            0,
            1);

        layout.Controls.Add(
            actionBar,
            0,
            2);

        layout.Controls.Add(
            outputCard,
            0,
            3);

        return layout;
    }

    private CardPanel BuildInputCard()
    {
        var card = new CardPanel
        {
            Title = "Input",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12)
        };

        card.AddAction(
            SmallAction(
                Glyphs.Clear,
                "Clear the input (Ctrl+L)",
                ClearInput));

        card.AddAction(
            SmallAction(
                Glyphs.OpenFile,
                "Load text from a file (Ctrl+O)",
                LoadTextFile));

        card.AddAction(
            SmallAction(
                Glyphs.Paste,
                "Paste from the clipboard (Ctrl+V)",
                PasteIntoInput));

        card.AddAction(
            _inputCounter);

        card.Body.Controls.Add(
            _inputBox);

        return card;
    }

    private CardPanel BuildPasswordCard()
    {
        var card = new CardPanel
        {
            Title = "Password",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12),
            Enabled = true
        };

        card.Body.Controls.Add(
            _passwordPanel);

        _passwordPanel.Dock = DockStyle.Top;
        _passwordPanel.AutoSize = true;
        _passwordPanel.AutoSizeMode =
            AutoSizeMode.GrowAndShrink;
        _passwordPanel.Enabled = true;

        return card;
    }

    private Control BuildActionBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode =
                AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12)
        };

        bar.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100f));

        bar.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        var formatHost = new Panel
        {
            Dock = DockStyle.Left,
            Width = 420,
            Height = 40,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 1, 0, 1)
        };

        formatHost.Controls.Add(
            _formatSelector);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection =
                FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode =
                AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        buttons.Controls.Add(
            _decryptButton);

        buttons.Controls.Add(
            _encryptButton);

        bar.Controls.Add(
            formatHost,
            0,
            0);

        bar.Controls.Add(
            buttons,
            1,
            0);

        return bar;
    }

    private CardPanel BuildOutputCard()
    {
        var card = new CardPanel
        {
            Title = "Result",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        card.AddAction(
            SmallAction(
                Glyphs.Swap,
                "Move the result into the input box",
                SwapOutputToInput));

        card.AddAction(
            SmallAction(
                Glyphs.Save,
                "Save the result to a file (Ctrl+S)",
                SaveOutputToFile));

        card.AddAction(
            SmallAction(
                Glyphs.Copy,
                "Copy the result (Ctrl+Shift+C)",
                CopyOutput));

        card.Body.Controls.Add(
            _outputBox);

        AttachOutputContextMenu();

        return card;
    }

    private ModernButton SmallAction(
        string glyph,
        string tooltip,
        Action action)
    {
        var button = new ModernButton
        {
            Glyph = glyph,
            Variant = ButtonVariant.Ghost,
            Size = new Size(34, 30),
            TabStop = false
        };

        button.Click +=
            (_, _) => action();

        _toolTip.SetToolTip(
            button,
            tooltip);

        return button;
    }

    private void AttachOutputContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            Font = Typography.Body
        };

        menu.Items.Add(
            "Copy",
            null,
            (_, _) => CopyOutput());

        menu.Items.Add(
            "Select all",
            null,
            (_, _) => _outputBox.SelectAllText());

        menu.Items.Add(
            new ToolStripSeparator());

        menu.Items.Add(
            "Save to file...",
            null,
            (_, _) => SaveOutputToFile());

        menu.Items.Add(
            "Use as input",
            null,
            (_, _) => SwapOutputToInput());

        _outputBox.Inner.ContextMenuStrip =
            menu;
    }

    // ---- format ---------------------------------------------------------------------------

    private int GetInitialFormatIndex()
    {
        return _settings.PreferredFormat switch
        {
            CipherFormat.Legacy => 1,
            CipherFormat.CBC => 2,
            _ => 0
        };
    }

    public CipherFormat SelectedFormat =>
        _formatSelector.SelectedIndex switch
        {
            1 => CipherFormat.Legacy,
            2 => CipherFormat.CBC,
            _ => CipherFormat.Modern
        };

    // ---- public commands ------------------------------------------------------------------

    public Task EncryptAsync() =>
        RunAsync(encrypting: true);

    public Task DecryptAsync() =>
        RunAsync(encrypting: false);

    public void FocusInput() =>
        _inputBox.Inner.Focus();

    public void ClearAll()
    {
        _inputBox.Clear();
        _outputBox.Clear();
        _passwordPanel.ClearPassword();

        _shell.SetStatus(
            "Cleared.");

        FocusInput();
    }

    public void ClearInput()
    {
        _inputBox.Clear();
        FocusInput();
    }

    public void PasteIntoInput()
    {
        if (!Clipboard.ContainsText())
        {
            _shell.Notify(
                "There is no text on the clipboard.",
                ToastKind.Warning);

            return;
        }

        _inputBox.Text =
            Clipboard.GetText();

        _inputBox.Inner.SelectionStart =
            _inputBox.TextLength;

        FocusInput();
    }

    public void CopyOutput()
    {
        if (_outputBox.TextLength == 0)
        {
            _shell.Notify(
                "There is nothing to copy yet.",
                ToastKind.Warning);

            return;
        }

        Clipboard.SetText(
            _outputBox.Text);

        _shell.Notify(
            "Result copied to the clipboard.",
            ToastKind.Success);
    }

    public void SwapOutputToInput()
    {
        if (_outputBox.TextLength == 0)
            return;

        _inputBox.Text =
            _outputBox.Text;

        _outputBox.Clear();

        _shell.SetStatus(
            "Result moved to the input box.");

        FocusInput();
    }

    public void LoadTextFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open a text file",
            Filter =
                "Text files (*.txt;*.md;*.json;*.xml;*.csv)|*.txt;*.md;*.json;*.xml;*.csv|" +
                "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) ==
            DialogResult.OK)
        {
            LoadTextFile(
                dialog.FileName);
        }
    }

    public void SaveOutputToFile()
    {
        if (_outputBox.TextLength == 0)
        {
            _shell.Notify(
                "There is nothing to save yet.",
                ToastKind.Warning);

            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save the result",
            Filter =
                "Text file (*.txt)|*.txt|" +
                "All files (*.*)|*.*",
            FileName = "cipherdesk-output.txt"
        };

        if (dialog.ShowDialog(this) !=
            DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(
                dialog.FileName,
                _outputBox.Text,
                new UTF8Encoding(false));

            _shell.Notify(
                "Saved to " +
                Path.GetFileName(dialog.FileName),
                ToastKind.Success);
        }
        catch (Exception ex)
            when (ex is IOException ||
                  ex is UnauthorizedAccessException)
        {
            UserFacingError error =
                ErrorPresenter.Describe(ex);

            ModernMessageBox.Error(
                FindForm(),
                error.Title,
                error.Message);
        }
    }

    /// <summary>
    /// Loads a dropped or chosen file into the input box,
    /// guarding against huge or binary files.
    /// </summary>
    public void LoadTextFile(string path)
    {
        try
        {
            var info = new FileInfo(path);

            if (info.Length >
                MaxTextFileBytes)
            {
                ModernMessageBox.Info(
                    FindForm(),
                    "File is too large",
                    $"{info.Name} is {ByteSize.Format(info.Length)}. " +
                    "The text workflow keeps everything in memory, " +
                    "so please use the Files tab for anything this size.");

                return;
            }

            _inputBox.Text =
                File.ReadAllText(path);

            _shell.SetStatus(
                $"Loaded {info.Name} ({ByteSize.Format(info.Length)}).");

            FocusInput();
        }
        catch (Exception ex)
        {
            UserFacingError error =
                ErrorPresenter.Describe(ex);

            ModernMessageBox.Error(
                FindForm(),
                error.Title,
                error.Message);
        }
    }

    // ---- actual operation -----------------------------------------------------------------

    private async Task RunAsync(bool encrypting)
    {
        if (_busy)
            return;

        if (_inputBox.TextLength == 0)
        {
            _shell.Notify("Enter some text first.", ToastKind.Warning);
            FocusInput();
            return;
        }

        if (!_passwordPanel.HasPassword)
        {
            _shell.Notify("Enter a password first.", ToastKind.Warning);
            _passwordPanel.FocusPassword();
            return;
        }

        if (encrypting && !await ConfirmWeakChoicesAsync().ConfigureAwait(true))
            return;

        string input = _inputBox.Text;
        CipherFormat format = SelectedFormat;

        SetBusy(
            true,
            encrypting
                ? "Encrypting..."
                : "Decrypting...");

        try
        {
            using SecureTextBoxReader.PasswordScope scope =
                _passwordPanel.AcquirePassword();

            char[] password = scope.Password;

            string result = await Task.Run(() =>
            {
                return encrypting
                    ? _cipher.Encrypt(
                        input,
                        password,
                        format)

                    : _cipher.Decrypt(
                        input,
                        password,
                        format);
            }).ConfigureAwait(true);

            _outputBox.Text = result;

            if (_settings.AutoSelectOutput)
                _outputBox.SelectAllText();

            if (encrypting && _settings.AutoCopyOnEncrypt)
            {
                Clipboard.SetText(result);

                _shell.Notify(
                    "Encrypted and copied to the clipboard.",
                    ToastKind.Success);
            }
            else
            {
                _shell.Notify(
                    encrypting
                        ? "Encrypted."
                        : "Decrypted.",
                    ToastKind.Success);
            }

            _shell.SetStatus(
                $"{(encrypting ? "Encrypted" : "Decrypted")} " +
                $"{input.Length:N0} characters into {result.Length:N0}.");
        }
        catch (Exception ex)
        {
            UserFacingError error = ErrorPresenter.Describe(ex);

            _shell.SetStatus(error.Title + ".");

            ModernMessageBox.Error(
                FindForm(),
                error.Title,
                error.Message);
        }
        finally
        {
            SetBusy(false, "Ready.");
        }
    }

    /// <summary>
    /// Warns before using formats or passwords that provide weaker security.
    /// </summary>
    private Task<bool> ConfirmWeakChoicesAsync()
    {
        if (SelectedFormat == CipherFormat.Legacy)
        {
            bool proceed =
                ModernMessageBox.Confirm(
                    FindForm(),
                    "Encrypt with the legacy format?",
                    "The legacy format has no salt, uses a fixed IV and provides no integrity protection. " +
                    "It exists only so older CipherDesk data can still be read.\n\n" +
                    "Use AES-256-GCM unless you specifically need compatibility with an older version.",
                    "Use legacy anyway",
                    destructive: true);

            if (!proceed)
            {
                return Task.FromResult(false);
            }
        }

        if (_passwordPanel.Strength <=
            PasswordStrength.Weak)
        {
            bool proceed =
                ModernMessageBox.Confirm(
                    FindForm(),
                    "Continue with a weak password?",
                    "This password would not survive an offline guessing attack. " +
                    "The encryption itself is only ever as strong as the password behind it.\n\n" +
                    "The dice button generates a strong one.",
                    "Encrypt anyway");

            if (!proceed)
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    private void SetBusy(
        bool busy,
        string status)
    {
        _busy = busy;

        _encryptButton.Enabled =
            !busy;

        _decryptButton.Enabled =
            !busy;

        _shell.SetBusy(
            busy);

        _shell.SetStatus(
            status);

        Cursor =
            busy
                ? Cursors.AppStarting
                : Cursors.Default;
    }

    private void UpdateInputState()
    {
        int length =
            _inputBox.TextLength;

        _inputCounter.Text =
            length == 1
                ? "1 character"
                : $"{length:N0} characters";

        bool ready =
            length > 0 &&
            _passwordPanel.HasPassword &&
            !_busy;

        _encryptButton.Enabled =
            ready;

        _decryptButton.Enabled =
            ready;
    }

    private void OnFormatChanged(
        object? sender,
        EventArgs e)
    {
        _settings.PreferredFormat =
            SelectedFormat;

        UpdateFormatBadge();
    }

    private void UpdateFormatBadge() =>
        _shell.SetFormatBadge(GetFormatBadgeText(SelectedFormat));

    private static string GetFormatBadgeText(CipherFormat format) =>
        format switch
        {
            CipherFormat.Modern => "AES-256-GCM",
            CipherFormat.Legacy => "Legacy v1 (insecure)",
            CipherFormat.CBC => "AES-256-CBC",
            _ => "AES-256-GCM"
        };

    // ---- drag and drop --------------------------------------------------------------------

    private void OnDragEnter(
        object? sender,
        DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(
                DataFormats.FileDrop) == true)
        {
            e.Effect =
                DragDropEffects.Copy;

            return;
        }

        if (e.Data?.GetDataPresent(
                DataFormats.Text) == true)
        {
            e.Effect =
                DragDropEffects.Copy;

            return;
        }

        e.Effect =
            DragDropEffects.None;
    }

    private void OnDragDrop(
        object? sender,
        DragEventArgs e)
    {
        if (e.Data?.GetData(
                DataFormats.FileDrop)
            is string[] { Length: > 0 } files)
        {
            LoadTextFile(
                files[0]);

            return;
        }

        if (e.Data?.GetData(
                DataFormats.Text)
            is string text)
        {
            _inputBox.Text =
                text;
        }
    }

    protected override void Dispose(
        bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
