# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-08-06

CipherDesk 2.0 was rebuilt around a separate cryptography engine and a new Windows desktop interface.

Ciphertext written by version 1.x remains readable through the **CBC compatibility format**. The runtime requirement changed from .NET Framework 4.8 to .NET 8.

### Added

* **AES-256-GCM format (v2)** with PBKDF2-HMAC-SHA256 key derivation at 210,000 iterations, a random 128-bit salt, and a random 96-bit nonce per message.
* **Authenticated, versioned header.** Salt, iteration count, and format version are authenticated and cannot be modified without detection.
* **AES-256-CBC compatibility format** for reading and maintaining compatibility with data created by the original application.
* **File encryption** for files of any size, streamed in 1 MiB authenticated chunks with flat memory usage.
* **Progress reporting and cancellation** for file operations.
* **Atomic writes.** Results are written to a temporary file and moved into place only after successful completion, preventing cancelled or failed operations from leaving partial output files.
* **Password strength meter** with an entropy estimate.
* **Cryptographically secure password generator** using the operating system's secure random number generator.
* **Light and dark themes**, including a follow-the-system mode that reacts to Windows theme changes.
* **Toast notifications**, tooltips, context menus, and a status bar.
* **Drag and drop** support for text files and encrypted files.
* **Keyboard shortcuts** for primary operations.
* Copy, paste, clear, save, and "use result as input" actions.
* **Unit and compatibility tests**, including golden vectors for the CBC compatibility format.

### Changed

* Rewritten as a two-project solution:

  * `CipherDesk.Core` provides the platform-independent cryptography engine.
  * `CipherDesk.App` provides the Windows Forms desktop interface.
* The dependency direction is now strictly **App → Core**.
* The user interface was rebuilt with custom owner-drawn controls, per-monitor V2 DPI awareness, and responsive `TableLayoutPanel` layouts.
* Encryption and decryption operations run away from the UI thread, preventing expensive key derivation from freezing the application.
* Error messages were rewritten to be actionable while avoiding unnecessary disclosure of cryptographic or internal implementation details.
* The product was renamed from `encrypt_decrypt` to **CipherDesk**.
* Added a new application icon, typography system, colour palette, themed controls, and redesigned workspace layout.
* Text and file encryption are now exposed through separate workflows while sharing the same cryptography engine.
* The text workflow now supports automatic detection of the modern encrypted format.
* Modern encryption is the default and recommended format.

### Fixed

* **Passwords longer than 24 bytes crashed the original application.** The original implementation encoded the password using Base64 and attempted to use the result directly as an AES key, which could produce an invalid key length. The modern format no longer has this artificial limitation.
* **Empty passwords were silently accepted** and could result in predictable cryptographic material.
* **Dead code in the decrypt handler** recomputed a key that was subsequently discarded.
* **Unhandled exceptions could terminate the application.** Operation failures are now caught and presented through the application's error handling layer.
* The main window could not be moved or closed reliably because of the previous borderless-window implementation.
* The theme system previously failed to apply the intended dark colour palette consistently.
* File operations could leave incomplete output files after cancellation or failure. File encryption now uses temporary files and atomic completion.
* Large files previously required loading the complete file into memory. File encryption now uses streaming and authenticated chunks.

### Security

* The modern encryption format uses **AES-256-GCM authenticated encryption**.
* Key derivation uses **PBKDF2-HMAC-SHA256 with 210,000 iterations** and a random 128-bit salt.
* Random nonces are generated for modern encryption, preventing identical plaintexts from producing identical ciphertexts.
* Authentication tags detect ciphertext and header modification during decryption.
* File chunks are authenticated individually and bound to their position within the encrypted file.
* Derived cryptographic keys are stored in mutable buffers and cleared after use where practical.
* Passwords are read from the native password control without unnecessarily materialising them as immutable managed strings.
* Raw cryptographic and internal exception details are not displayed directly to the user.

### Compatibility

CipherDesk 2.0 supports the following encryption formats:

| Format          | Encryption         | Decryption                        | Recommended |
| --------------- | ------------------ | --------------------------------- | ----------- |
| **AES-256-GCM** | Yes                | Yes                               | **Yes**     |
| **AES-256-CBC** | Compatibility only | Yes                               | No          |
| **Auto**        | Resolves to GCM    | Detects supported modern payloads | Yes         |

The **CBC** format exists primarily for compatibility with data created by the original CipherDesk implementation.

It uses the original AES-256-CBC construction and does not provide authenticated encryption. It should therefore not be selected for new data unless compatibility with an existing CBC-encrypted payload is required.

When existing CBC data is successfully decrypted, it should be re-encrypted using **AES-256-GCM** whenever possible.

### Deprecated

* The original AES-256-CBC encryption format is retained for compatibility but is **not recommended for new encryption**.
* CBC encryption should only be used when interoperability with existing legacy data is required.
* The previous `Custom` format name has been replaced by **CBC** to accurately describe the underlying encryption scheme.

## [1.0.0]

Initial release.

* Single-window text encryption and decryption.
* AES-256-CBC encryption.
* Fixed IV.
* Unsalted password-derived key.
* No authenticated integrity protection.
* .NET Framework 4.8 desktop application.
