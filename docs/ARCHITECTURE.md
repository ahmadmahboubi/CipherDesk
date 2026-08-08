# Architecture

## The shape of it

```text
┌──────────────────────────────────────────────────────────┐
│  CipherDesk.App          net8.0-windows                  │
│                                                          │
│  Forms/      MainForm — shell, status bar, shortcuts     │
│  Views/      TextCipherView, FileCipherView              │
│  Controls/   owner-drawn button, card, text box, toast…  │
│  Theming/    ThemePalette, ThemeManager, DwmWindowTheme  │
│  Dialogs/    ModernMessageBox, AboutDialog               │
│  Services/   AppSettings, ErrorPresenter, IAppShell     │
└───────────────────────────┬──────────────────────────────┘
                            │ depends on
                            ▼
┌──────────────────────────────────────────────────────────┐
│  CipherDesk.Core         net8.0  (no UI, no Windows)     │
│                                                          │
│  Abstractions/  ITextCipher, IFileCipher                 │
│  Text/          ModernTextCipher, CbcTextCipher,         │
│                 TextCipherRouter                         │
│  Files/         FileCipher, CryptoProgress               │
│  Passwords/     strength evaluation, generation          │
│  Internal/      CipherHeader, KeyDerivation, SecureBuffer│
└──────────────────────────────────────────────────────────┘
```

The dependency arrow points one way and never the other. `CipherDesk.Core` has no reference to
`System.Windows.Forms`, no reference to `System.Drawing`, and targets plain `net8.0` rather than
`net8.0-windows`.

That separation provides three concrete benefits:

1. **The tests run independently of the UI.** The cryptography engine can be tested without loading
   Windows Forms or any Windows-specific UI component.
2. **A CLI is a small addition.** A future command-line front end can reference `CipherDesk.Core`
   directly without requiring a refactor of the encryption engine.
3. **The compiler enforces the boundary.** Core code cannot accidentally depend on UI types such as
   `Form`, `MessageBox`, or `System.Windows.Forms`.

## Layers and responsibilities

| Layer                   | Responsibility                                       | Must not                                                                 |
| ----------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------ |
| `Internal/`             | Header parsing, key derivation, secure buffers       | Know about text vs. files                                                |
| `Text/`, `Files/`       | Encryption formats and file/stream orchestration     | Format user-facing messages or access the clipboard                      |
| `Passwords/`            | Password strength estimation and generation          | Contain UI-specific policy decisions                                     |
| `Controls/`, `Theming/` | Rendering and visual behavior                        | Contain cryptography or business rules                                   |
| `Views/`                | Own workspace inputs, outputs and validation         | Reach directly into `MainForm` or call cryptographic primitives directly |
| `Forms/MainForm`        | Application shell, navigation, status, shortcuts     | Know how encryption itself works                                         |
| `Services/`             | Settings, error presentation, secure password access | Draw UI controls directly                                                |

## Cipher abstraction

Text encryption is exposed through `ITextCipher`, allowing the UI to remain independent of the
specific encryption implementation.

The current text formats are:

* **Modern** — AES-256-GCM authenticated encryption and the recommended format for new data.
* **CBC** — AES-256-CBC compatibility format retained for data produced by the original encryption
  implementation.

`TextCipherRouter` is the single entry point used by the UI. It selects the appropriate implementation
without containing cryptographic operations itself.

The router also keeps encryption and decryption format selection predictable:

* `Auto` resolves to the Modern format when encrypting.
* Decryption may use format detection when the payload contains a recognizable Modern format marker.
* CBC must not be selected automatically merely because Modern detection fails unless the payload format
  is explicitly defined to make that fallback safe.
* The CBC implementation remains isolated behind `ITextCipher`, so it can be maintained for
  compatibility without affecting the Modern encryption path.

The important rule is that format selection must never depend on blindly trying several decryption
algorithms with the supplied password. Format identification and cryptographic verification are
separate concerns.

## Views depend on `IAppShell`, not on `MainForm`

```csharp
public interface IAppShell
{
    void Notify(string message, ToastSeverity severity);
    void SetStatus(string message);
    void SetBusy(bool busy);
    void SetFormatBadge(string? text);
}
```

A view needs only a small set of services from the surrounding application shell. Passing the complete
`MainForm` would allow a view to reach unrelated application state.

Using `IAppShell` keeps the dependency narrow and makes the views easier to test with a lightweight
stub implementation.

## Theming

`ThemeManager.Apply(Control root)` walks the control tree and applies the active palette to controls
that participate in the application's theming system.

The application supports:

* Light mode
* Dark mode
* Follow-system mode

The title bar is also updated through the DWM integration so the native window chrome remains visually
consistent with the application theme.

Keeping theme application centralized avoids spreading color decisions throughout the UI and makes
theme changes predictable.

## Owner-drawn controls

Controls such as `ModernButton`, cards and text boxes retain their native WinForms foundations while
providing CipherDesk-specific rendering.

For example, `ModernButton` derives from `Button` rather than from a generic `Control`. This preserves
important WinForms behavior such as:

* keyboard interaction,
* tab navigation,
* `AcceptButton` and `CancelButton` participation,
* standard `Click` semantics,
* accessibility information.

The application therefore gets a custom visual appearance without throwing away the framework behavior
that users and assistive technologies expect.

`ModernMessageBox` follows the same principle. It remains a real modal `Form` rather than a borderless
window pretending to be one.

## Error handling

Cryptographic failures are intentionally presented through a centralized `ErrorPresenter`.

Errors such as:

* incorrect passwords,
* corrupted ciphertext,
* authentication failures,
* malformed encrypted payloads,
* invalid encryption headers,

should not expose unnecessary implementation details to the user.

The UI therefore receives a safe `UserFacingError` rather than displaying raw exception messages.

Unknown exceptions are also sanitized before presentation. This prevents implementation details such as
file paths, internal state, or framework-specific exception text from being unnecessarily exposed.

## Atomic file operations

`FileCipher` does not write decrypted or encrypted output directly to the final destination.

Instead, operations use a temporary file and move the completed result into place only after the operation
has successfully completed.

This prevents a cancelled or failed operation from leaving a partially written output file that could
later be mistaken for valid data.

Temporary files are removed on failure or cancellation.

## Progress and cancellation

File encryption and decryption support progress reporting and cancellation.

The Core layer exposes progress through:

```csharp
IProgress<CryptoProgress>
```

and cancellation through:

```csharp
CancellationToken
```

The cryptography engine does not know how progress is displayed. It reports state, while the UI decides
whether that state should appear as a progress bar, byte counter, percentage or elapsed-time indicator.

This keeps presentation concerns outside the Core layer.

## Threading

Expensive operations are kept away from the UI thread.

### Text operations

Text encryption and decryption are executed asynchronously from the UI perspective. Password-based key
derivation can take a noticeable amount of time, so performing it directly inside a button-click handler
would make the application appear frozen.

### File operations

File processing is asynchronous and supports cancellation.

The UI remains responsive while large files are processed, and progress is reported back to the
workspace through the application shell.

The UI disables conflicting actions while an operation is running to prevent accidental re-entrancy.

## Memory handling of secrets

CipherDesk attempts to minimize the lifetime of sensitive material in managed memory.

`SecureBuffer` owns sensitive byte arrays and clears them with:

```csharp
CryptographicOperations.ZeroMemory
```

when they are disposed.

Derived encryption keys are stored in wipeable buffers where possible.

Passwords are read into temporary character buffers and cleared after use rather than being deliberately
kept alive as application state.

These techniques reduce unnecessary exposure, but they do not provide absolute protection. The native
text control, operating system memory management, paging, debugging tools and malware remain outside the
application's control.

This is therefore treated as a mitigation rather than a claim of perfect memory secrecy.

## Performance considerations

| Area                 | Approach                                   |
| -------------------- | ------------------------------------------ |
| File I/O             | Buffered sequential access                 |
| File processing      | Streaming/chunked processing               |
| Chunk buffers        | Reusable buffers where appropriate         |
| Key derivation       | `Rfc2898DeriveBytes.Pbkdf2`                |
| Text encryption      | Performed away from the UI thread          |
| File encryption      | Asynchronous processing with cancellation  |
| UI rendering         | Cached drawing resources where appropriate |
| Runtime dependencies | No third-party runtime UI dependencies     |

The architecture intentionally favors straightforward .NET cryptography and WinForms primitives over
large third-party UI frameworks.

## Testing

`tests/CipherDesk.Core.Tests` focuses on the parts that carry the security and business rules.

Tests cover areas such as:

### Modern encryption

* Encryption/decryption round trips
* Different ciphertext for repeated encryption
* Wrong-password handling
* Ciphertext tampering
* Header tampering
* Truncated payloads
* Invalid or unsupported versions

### CBC compatibility

* Compatibility with the original CBC implementation
* Known compatibility vectors
* Supported password constraints where required by the original format
* Successful decryption of existing CBC data

The compatibility tests are especially important because the CBC implementation exists primarily to
preserve access to existing encrypted data.

### File encryption

* Small files
* Empty files where supported
* Chunk-boundary sizes
* Large files
* Authentication failures
* Truncated files
* Cancellation
* Temporary-file cleanup
* Progress reporting

### Password handling

* Strength evaluation
* Entropy estimation
* Password generation
* Generator character constraints

The UI layer intentionally contains little business logic. Its primary responsibility is presentation,
input handling and orchestration. This allows most important application behavior to remain in
`CipherDesk.Core`, where it can be tested without depending on a graphical environment.

## Architectural goals

CipherDesk is intentionally built around a few simple principles:

1. **Keep cryptography independent from the UI.**
2. **Prefer standard .NET cryptographic primitives.**
3. **Keep compatibility code isolated.**
4. **Make security-sensitive behavior explicit and testable.**
5. **Keep the application usable without third-party runtime dependencies.**
6. **Avoid coupling views to the main application window.**
7. **Keep file processing streaming and cancellable.**
8. **Prefer maintainable code over architectural complexity.**

The goal is not to create an elaborate framework around a small encryption utility. The goal is to keep
the security-critical code understandable, isolated and testable while allowing the Windows UI to evolve
independently.
