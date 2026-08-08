# Contributing to CipherDesk

Thanks for taking the time to contribute to CipherDesk.

Bug reports, small fixes, documentation improvements, tests, and well-scoped feature contributions are welcome.

Because CipherDesk is a security-focused application, changes to the cryptography engine receive additional scrutiny. A small-looking crypto change can have consequences far beyond the few lines it modifies.

## Getting started

```bash
git clone https://github.com/ahmadmahboubi/cipherdesk.git
cd cipherdesk

dotnet restore
dotnet build
dotnet test
```

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

The application project targets Windows because it uses Windows Forms. `CipherDesk.Core` and its tests are platform-independent and can be built and tested on Windows, Linux, and macOS.

To run the desktop application on Windows:

```bash
dotnet run --project src/CipherDesk.App
```

## Project structure

```text
CipherDesk.sln
├── src/
│   ├── CipherDesk.Core/
│   │   └── Cryptography engine and shared application logic
│   │
│   └── CipherDesk.App/
│       └── Windows Forms desktop application
│
└── tests/
    └── CipherDesk.Core.Tests/
        └── Unit and cryptographic compatibility tests
```

The dependency direction is intentionally one-way:

```text
CipherDesk.App → CipherDesk.Core
```

The core project must not reference the UI project or `System.Windows.Forms`.

---

## Ground rules for cryptography changes

This is the most important section to read before opening a PR that touches `CipherDesk.Core`.

### 1. Never change the CBC compatibility format

The **AES-256-CBC** implementation exists for backward compatibility with data produced by the original application.

Its behaviour is pinned by compatibility tests and known test vectors.

Do not modify the algorithm, key construction, IV handling, padding behaviour, or ciphertext representation unless the compatibility requirements themselves are intentionally being changed.

If a compatibility test fails, assume the implementation has changed incorrectly before assuming the test is wrong.

**Do not regenerate compatibility vectors from CipherDesk's own output.**

The purpose of a golden vector is to compare CipherDesk against an independent, known-good reference.

### 2. Never silently change the modern format

The current modern format uses **AES-256-GCM** and a versioned payload structure.

If the binary format, header, key derivation parameters, nonce construction, authentication data, or ciphertext layout must change in a way that breaks existing ciphertext, introduce a new format version.

Do not make an existing version mean something different.

Existing readers must continue to read previously supported versions whenever practical.

### 3. Do not roll your own cryptographic primitives

Use the cryptographic primitives provided by:

```text
System.Security.Cryptography
```

Do not implement AES, GCM, PBKDF2, random-number generation, hashing, or authentication primitives manually.

Security-sensitive code should favour well-tested platform implementations over cleverness.

### 4. Every cryptographic change requires tests

At minimum, changes affecting encryption or decryption should include tests covering:

* Successful encryption and decryption round trips.
* Wrong passwords.
* Modified ciphertext.
* Truncated or malformed payloads.
* Empty and very small inputs.
* Inputs crossing chunk boundaries for file encryption.
* Compatibility with existing supported formats.
* Cancellation and atomic file-output behaviour where applicable.

For format changes, add explicit compatibility tests rather than relying only on round-trip tests.

A round-trip test can happily prove that two broken implementations agree with each other. Computers are very cooperative that way.

### 5. Never weaken the default security level for convenience

New encryption functionality should use the modern authenticated encryption format unless there is a documented interoperability requirement.

Compatibility formats must remain clearly separated from recommended formats.

---

## Password and secret handling

CipherDesk deals with passwords and plaintext, so avoid unnecessary copies of sensitive data.

When working with passwords or cryptographic keys:

* Prefer `ReadOnlySpan<char>` or other appropriate non-copying APIs where practical.
* Avoid converting passwords into immutable `string` instances unnecessarily.
* Clear mutable key buffers after use.
* Do not log passwords, keys, plaintext, or decrypted content.
* Do not include sensitive values in exceptions, telemetry, diagnostics, or test output.
* Tests should use synthetic data rather than real credentials or confidential information.

If a change requires a different secret-handling strategy, document why.

---

## Style

The `.editorconfig` file is authoritative. Your IDE should apply these rules automatically.

The general conventions are:

* File-scoped namespaces.
* Four-space indentation.
* `_camelCase` for private fields.
* Clear, descriptive names.
* Explicit types where they improve readability; `var` is appropriate when the right-hand side already makes the type obvious.
* Small methods with a single clear responsibility.
* No unnecessary abstractions.

Comments should explain **why**, not what.

Good:

```csharp
// Keep the compatibility IV unchanged so existing CBC ciphertext remains readable.
```

Not useful:

```csharp
// Set the IV.
```

Security-sensitive assumptions should be documented even when the code appears obvious.

---

## UI changes

CipherDesk intentionally avoids third-party UI frameworks and runtime dependencies.

Please follow these rules when modifying the Windows Forms application:

* Do not add runtime dependencies without discussion.
* Keep the application dependency-free where practical.
* Colours must come from `ThemePalette`.
* Do not hard-code application colours.
* Use the existing typography system.
* Reuse existing controls before introducing new ones.
* Avoid absolute positioning.
* Prefer `TableLayoutPanel`, `FlowLayoutPanel`, docking, anchoring, and autosizing.
* Check both light and dark themes.
* Check at 100%, 150%, and 200% Windows display scaling.
* Check narrow and wide window sizes.
* Anything clickable should have a useful tooltip.
* Primary actions should have keyboard shortcuts.
* Avoid modal dialogs for routine notifications.
* Keep accessibility and keyboard navigation in mind.

For UI pull requests, screenshots are strongly encouraged and should include both light and dark themes where applicable.

---

## Tests

Tests live under:

```text
tests/CipherDesk.Core.Tests
```

Run the complete test suite with:

```bash
dotnet test
```

Before submitting a pull request, make sure:

```bash
dotnet restore
dotnet build -c Release
dotnet test
```

all complete successfully.

Cryptographic compatibility tests should be deterministic. Tests involving randomness should validate the properties that matter rather than asserting a specific random value.

For example, encryption should generally be tested to ensure that encrypting identical plaintext twice produces different ciphertext when the format requires randomized nonces or salts.

---

## Documentation

Documentation is part of the security model.

If a change affects:

* Encryption formats
* File formats
* Key derivation
* Password handling
* Compatibility
* Threat assumptions
* User-visible security behaviour

update the relevant documentation in the same pull request.

Do not claim that a feature is "secure", "unbreakable", or "military-grade". Security claims should describe the actual construction and its limitations.

---

## Commits

Use short, imperative commit messages.

Good:

```text
Add CBC compatibility tests
Fix file cancellation cleanup
Improve password strength feedback
Add dark theme support
Document GCM file format
```

Avoid:

```text
Added some stuff
Fixes
Changes
Update
final-final-real
```

One logical change per commit is preferred.

---

## Pull requests

Keep pull requests focused.

A good pull request should explain:

1. **What changed**
2. **Why it changed**
3. **How it was implemented**
4. **How it was tested**
5. **Whether compatibility is affected**

For UI changes, include screenshots when useful.

For cryptographic changes, explicitly describe:

* The affected format.
* Whether existing ciphertext remains readable.
* Any changed parameters.
* The tests added or modified.
* Why the change does not weaken the security model.

Avoid combining unrelated refactoring, UI changes, and cryptographic changes into one large pull request.

---

## Reporting bugs

When reporting a bug, include:

* CipherDesk version.
* Windows version.
* CPU architecture if relevant.
* What you expected to happen.
* What actually happened.
* Steps to reproduce the problem.
* Relevant error messages.
* A minimal example if possible.

For UI bugs, include a screenshot when it helps explain the problem.

### Never include sensitive data

Do **not** attach:

* Real passwords.
* Private encryption keys.
* Real confidential files.
* Production data.
* Decrypted sensitive content.
* Encrypted files containing sensitive information.

Instead, construct a minimal synthetic example that reproduces the problem.

For cryptographic bugs, a small reproducible test case is far more useful than uploading someone's actual encrypted archive and hoping nobody opens it.

---

## Security vulnerabilities

Please do not publicly disclose security vulnerabilities through GitHub Issues or ordinary pull requests.

See [`SECURITY.md`](SECURITY.md) for the preferred vulnerability-reporting process.

When reporting a vulnerability, provide enough technical information to reproduce and assess the issue, but do not include real credentials, passwords, private keys, or confidential user data.

---

## Code of conduct

Please be respectful and constructive.

Technical disagreement is expected, especially around cryptography. Personal attacks are not.

The goal is to build a small, understandable, maintainable encryption tool that people can inspect and trust.

Thank you for helping make CipherDesk better.
