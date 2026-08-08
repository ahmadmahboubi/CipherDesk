using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CipherDesk.App.Controls;
using CipherDesk.App.Dialogs;
using CipherDesk.App.Services;
using CipherDesk.App.Theming;
using CipherDesk.Core.Files;

namespace CipherDesk.App.Views;

/// <summary>
/// The file workflow: pick a file, pick a destination, encrypt or decrypt with live progress.
/// </summary>
/// <remarks>
/// Everything here streams, so a 20 GB file uses the same memory as a 20 KB one. The operation is
/// cancellable at any point and writes through a temporary file, so cancelling never leaves a
/// partially written result behind.
/// </remarks>
public sealed class FileCipherView : UserControl
{
    private readonly IAppShell _shell;
    private readonly FileCipher _cipher;
    private readonly ToolTip _toolTip;

    private readonly ModernTextBox _sourceBox;
    private readonly ModernTextBox _destinationBox;
    private readonly PasswordPanel _passwordPanel;
    private readonly ModernProgressBar _progressBar;
    private readonly Label _progressLabel;
    private readonly Label _dropHint;
    private readonly ModernButton _encryptButton;
    private readonly ModernButton _decryptButton;
    private readonly ModernButton _cancelButton;

    private CancellationTokenSource? _cancellation;
    private bool _busy;

    public FileCipherView(IAppShell shell, FileCipher cipher)
    {
        _shell = shell;
        _cipher = cipher;

        BackColor = Color.Transparent;
        Dock = DockStyle.Fill;
        AllowDrop = true;

        _toolTip = new ToolTip { InitialDelay = 400, ReshowDelay = 150, AutoPopDelay = 8000 };

        _sourceBox = new ModernTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            PlaceholderText = "No file selected",
            Margin = new Padding(0, 0, 8, 0)
        };

        _destinationBox = new ModernTextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Chosen automatically once a source file is picked",
            Margin = new Padding(0, 0, 8, 0)
        };

        _passwordPanel = new PasswordPanel { Dock = DockStyle.Fill };
        _passwordPanel.PasswordChanged += (_, _) => UpdateButtons();

        _progressBar = new ModernProgressBar { Dock = DockStyle.Fill, Height = 6, Margin = new Padding(0, 4, 0, 6) };

        _progressLabel = new Label
        {
            AutoSize = true,
            Font = Typography.Caption,
            Tag = "secondary",
            Text = "Idle",
            Margin = Padding.Empty
        };

        _dropHint = new Label
        {
            AutoSize = true,
            Font = Typography.Caption,
            Tag = "secondary",
            Text = "Tip: drop a file anywhere on this window to select it.",
            Margin = new Padding(0, 10, 0, 0)
        };

        _encryptButton = new ModernButton
        {
            Text = "Encrypt file",
            Glyph = Glyphs.Lock,
            Variant = ButtonVariant.Primary,
            Size = new Size(150, 40),
            Margin = new Padding(8, 0, 0, 0)
        };
        _encryptButton.Click += async (_, _) => await RunAsync(encrypting: true).ConfigureAwait(true);

        _decryptButton = new ModernButton
        {
            Text = "Decrypt file",
            Glyph = Glyphs.Unlock,
            Variant = ButtonVariant.Secondary,
            Size = new Size(150, 40),
            Margin = new Padding(8, 0, 0, 0)
        };
        _decryptButton.Click += async (_, _) => await RunAsync(encrypting: false).ConfigureAwait(true);

        _cancelButton = new ModernButton
        {
            Text = "Cancel",
            Glyph = Glyphs.Cancel,
            Variant = ButtonVariant.Danger,
            Size = new Size(120, 40),
            Margin = new Padding(8, 0, 0, 0),
            Visible = false
        };
        _cancelButton.Click += (_, _) => _cancellation?.Cancel();

        _toolTip.SetToolTip(_encryptButton, "Encrypt the source file (Ctrl+E)");
        _toolTip.SetToolTip(_decryptButton, "Decrypt the source file (Ctrl+D)");
        _toolTip.SetToolTip(_cancelButton, "Stop and discard the partial result (Esc)");
        _toolTip.SetToolTip(_sourceBox, "The file to process. You can also drop a file onto the window.");
        _toolTip.SetToolTip(_destinationBox, "Where the result is written. The original file is never modified.");

        Controls.Add(BuildLayout());

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        UpdateButtons();
    }

    // ---- construction --------------------------------------------------------------------

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // spacer keeps the cards top-aligned

        layout.Controls.Add(BuildFilesCard(), 0, 0);
        layout.Controls.Add(BuildPasswordCard(), 0, 1);
        layout.Controls.Add(BuildProgressCard(), 0, 2);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, 3);

        return layout;
    }

    private CardPanel BuildFilesCard()
    {
        var card = new CardPanel
        {
            Title = "Files",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12)
        };
        card.Body.AutoSize = true;
        card.Body.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var browseSource = new ModernButton { Text = "Browse", Variant = ButtonVariant.Secondary, Size = new Size(100, 38) };
        browseSource.Click += (_, _) => BrowseForSource();

        var browseDestination = new ModernButton { Text = "Change", Variant = ButtonVariant.Secondary, Size = new Size(100, 38) };
        browseDestination.Click += (_, _) => BrowseForDestination();

        grid.Controls.Add(FieldLabel("Source"), 0, 0);
        grid.Controls.Add(_sourceBox, 1, 0);
        grid.Controls.Add(browseSource, 2, 0);

        grid.Controls.Add(FieldLabel("Destination"), 0, 1);
        grid.Controls.Add(_destinationBox, 1, 1);
        grid.Controls.Add(browseDestination, 2, 1);

        grid.Controls.Add(_dropHint, 1, 2);

        card.Body.Controls.Add(grid);
        return card;
    }

    private CardPanel BuildPasswordCard()
    {
        var card = new CardPanel
        {
            Title = "Password",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12)
        };

        card.Body.AutoSize = true;
        card.Body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        card.Body.Controls.Add(_passwordPanel);
        return card;
    }

    private CardPanel BuildProgressCard()
    {
        var card = new CardPanel
        {
            Title = "Progress",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty
        };
        card.Body.AutoSize = true;
        card.Body.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 0)
        };
        buttons.Controls.Add(_cancelButton);
        buttons.Controls.Add(_decryptButton);
        buttons.Controls.Add(_encryptButton);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_progressLabel, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(_progressBar, 0, 0);
        stack.Controls.Add(bottom, 0, 1);

        card.Body.Controls.Add(stack);
        return card;
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Typography.Body,
        Tag = "secondary",
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 12, 14, 12)
    };

    // ---- commands ---------------------------------------------------------------------------

    public void BrowseForSource()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a file",
            Filter = $"All files (*.*)|*.*|CipherDesk files (*{FileCipher.EncryptedExtension})|*{FileCipher.EncryptedExtension}"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK) SelectSource(dialog.FileName);
    }

    public void SelectSource(string path)
    {
        _sourceBox.Text = path;

        // Guess intent from the extension and pre-fill a destination that will not overwrite anything.
        bool looksEncrypted = path.EndsWith(FileCipher.EncryptedExtension, StringComparison.OrdinalIgnoreCase);
        _destinationBox.Text = looksEncrypted
            ? FileCipher.SuggestDecryptedPath(path)
            : FileCipher.SuggestEncryptedPath(path);

        var info = new FileInfo(path);
        _shell.SetStatus($"{info.Name} selected ({ByteSize.Format(info.Exists ? info.Length : 0)}).");
        _progressLabel.Text = looksEncrypted ? "Ready to decrypt" : "Ready to encrypt";

        UpdateButtons();
    }

    private void BrowseForDestination()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Choose where to save",
            Filter = "All files (*.*)|*.*",
            FileName = Path.GetFileName(_destinationBox.Text),
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK) _destinationBox.Text = dialog.FileName;
    }

    private async Task RunAsync(bool encrypting)
    {
        if (_busy) return;

        string source = _sourceBox.Text.Trim();
        string destination = _destinationBox.Text.Trim();

        if (!File.Exists(source))
        {
            _shell.Notify("Choose a source file first.", ToastKind.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            _shell.Notify("Choose where to save the result.", ToastKind.Warning);
            return;
        }

        if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            ModernMessageBox.Error(FindForm(), "Same file",
                "The source and destination are the same file. Choose a different destination so the original is never at risk.");
            return;
        }

        if (!_passwordPanel.HasPassword)
        {
            _shell.Notify("Enter a password first.", ToastKind.Warning);
            _passwordPanel.FocusPassword();
            return;
        }

        if (File.Exists(destination) &&
            !ModernMessageBox.Confirm(FindForm(), "Replace the existing file?",
                $"{Path.GetFileName(destination)} already exists and will be replaced.",
                "Replace", destructive: true))
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        SetBusy(true);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var progress = new Progress<CryptoProgress>(OnProgress);

        try
        {
            using SecureTextBoxReader.PasswordScope scope = _passwordPanel.AcquirePassword();

            if (encrypting)
                await _cipher.EncryptAsync(source, destination, scope.Password, progress, _cancellation.Token).ConfigureAwait(true);
            else
                await _cipher.DecryptAsync(source, destination, scope.Password, progress, _cancellation.Token).ConfigureAwait(true);

            stopwatch.Stop();

            long size = new FileInfo(destination).Length;
            _progressBar.Value = 1;
            _progressLabel.Text = $"Done - {ByteSize.Format(size)} in {stopwatch.Elapsed.TotalSeconds:F1} s";
            _shell.Notify($"{(encrypting ? "Encrypted" : "Decrypted")} to {Path.GetFileName(destination)}.", ToastKind.Success);
            _shell.SetStatus("Finished.");
        }
        catch (OperationCanceledException)
        {
            _progressLabel.Text = "Cancelled";
            _shell.Notify("Cancelled. Nothing was written.", ToastKind.Info);
        }
        catch (Exception ex)
        {
            UserFacingError error = ErrorPresenter.Describe(ex);
            _progressLabel.Text = error.Title;
            ModernMessageBox.Error(FindForm(), error.Title, error.Message);
        }
        finally
        {
            SetBusy(false);
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private void OnProgress(CryptoProgress progress)
    {
        // Progress<T> already marshals back to the UI thread through the captured context.
        _progressBar.Value = progress.Fraction;
        _progressLabel.Text = $"{ByteSize.Format(progress.BytesProcessed)} of {ByteSize.Format(progress.TotalBytes)}  -  {progress.Percent}%";
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _cancelButton.Visible = busy;
        _shell.SetBusy(busy);
        Cursor = busy ? Cursors.AppStarting : Cursors.Default;

        if (!busy) UpdateButtons();
        else { _encryptButton.Enabled = false; _decryptButton.Enabled = false; }
    }

    private void UpdateButtons()
    {
        bool ready = !_busy && _sourceBox.TextLength > 0 && _passwordPanel.HasPassword;
        _encryptButton.Enabled = ready;
        _decryptButton.Enabled = ready;
    }

    private void OnDragEnter(object? sender, DragEventArgs e) =>
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && File.Exists(files[0]))
            SelectSource(files[0]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
