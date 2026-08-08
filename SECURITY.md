# Security Policy

## Supported versions

| Version | Supported                             |
| ------- | ------------------------------------- |
| 2.x     | Yes                                   |
| 1.x     | No. Please migrate to CipherDesk 2.x. |

CipherDesk 2.x uses the modern **AES-256-GCM** format for new encryption.

The **AES-256-CBC** format is retained only for compatibility with data created by the original application.

---

## Reporting a vulnerability

Please **do not open a public GitHub issue** for a security vulnerability.

Use GitHub's private vulnerability reporting:

https://github.com/ahmadmahboubi/cipherdesk/security/advisories/new

If private vulnerability reporting is enabled for the repository, this is the preferred reporting channel.

Please include:

* A clear description of the vulnerability.
* The potential security impact and an approximate severity.
* Steps to reproduce the issue.
* A proof of concept, if available.
* The affected CipherDesk version.
* The affected Windows version.
* Whether existing encrypted data is affected.
* Whether you intend to disclose the vulnerability publicly and, if so, your expected timeline.

Please do not include real passwords, private keys, confidential files, or sensitive user data in the report.

### Response timeline

We aim to:

* Acknowledge reports within **72 hours**.
* Assess confirmed reports within **7 days**.
* Release fixes as soon as a safe and tested solution is available.

Security researchers will be credited in the changelog when appropriate, unless they prefer to remain anonymous.

---

## Scope

### In scope

Security reports involving:

* Cryptographic implementation.
* AES-256-GCM encryption.
* AES-256-CBC compatibility handling.
* File encryption and authenticated chunking.
* Ciphertext authentication and integrity verification.
* Key derivation.
* Nonce and salt generation.
* Secret and key memory handling.
* Input validation affecting security.
* Format parsing and downgrade attacks.
* Authentication bypasses.
* Issues that allow an attacker to read protected data.
* Issues that allow an attacker to modify protected data without detection.
* Issues that cause encrypted files to be incorrectly accepted as valid data.

### Out of scope

The following are generally outside the security scope:

* Attacks requiring an already-compromised machine.
* Keyloggers or malware running inside the user's session.
* Memory scraping requiring local debugger-level access.
* Malicious local users who already have unrestricted access to the machine.
* Passwords that are intentionally weak or easily guessable.
* Data recovery when the user has forgotten their password.
* The known weaknesses of the AES-256-CBC compatibility format.

The CBC compatibility format is intentionally retained for backward compatibility. Its cryptographic limitations are documented and should not be interpreted as security properties of the modern GCM format.

---

## Security model

CipherDesk's modern encryption is designed to protect encrypted text and files against an attacker who obtains the ciphertext but does not know the encryption password.

The modern format provides:

* **AES-256-GCM** authenticated encryption.
* **PBKDF2-HMAC-SHA256** password-based key derivation.
* A random **128-bit salt** for each encrypted message.
* Random nonces for modern text encryption.
* Authenticated metadata and ciphertext.
* Authenticated file chunks for large-file encryption.
* Detection of ciphertext modification, truncation, and invalid authentication data.

CipherDesk does **not** attempt to protect against an attacker who already controls the machine while encryption or decryption is taking place.

---

## Known limitations

These are known design limitations and should not be reported as undiscovered vulnerabilities unless they can be used to cross the intended security boundary.

### 1. Passwords can exist in multiple memory locations

CipherDesk reads passwords from the native password control into a mutable character buffer and attempts to clear that buffer after use.

Derived cryptographic keys are also stored in mutable buffers and cleared when they are no longer required.

However, Windows and the .NET runtime may create additional copies of data internally, and the native edit control necessarily holds the password while it is being entered.

Windows may also page memory to disk.

Therefore, CipherDesk cannot guarantee that a password or key exists in exactly one physical memory location at all times.

A stronger guarantee would require a custom password-entry implementation with substantially tighter control over native memory.

### 2. The clipboard is outside CipherDesk's security boundary

When the user copies encrypted or decrypted text, it is placed on the Windows clipboard.

Other applications may be able to read clipboard contents.

Do not leave sensitive decrypted content on the clipboard longer than necessary.

CipherDesk does not attempt to globally clear or control the system clipboard because doing so could unexpectedly destroy clipboard data belonging to other applications.

### 3. No independent security audit

CipherDesk uses cryptographic primitives provided by the .NET platform and attempts to use them according to established cryptographic practices.

The implementation, however, has **not been independently audited by a professional security firm or cryptographer**.

The cryptographic format is documented, and automated tests cover important correctness and compatibility properties, but testing is not equivalent to an independent security audit.

For high-risk or life-critical threat models, use a mature, independently reviewed encryption tool appropriate for that environment.

### 4. Plaintext length is not hidden

AES-GCM does not inherently hide plaintext length.

Ciphertext length therefore reveals the approximate plaintext length, subject to the fixed metadata and authentication overhead of the format.

File sizes are similarly observable.

CipherDesk does not implement traffic-flow obfuscation, padding to fixed sizes, or steganographic techniques.

### 5. Metadata is not encrypted by the application

CipherDesk protects the encrypted content itself, but does not attempt to hide all surrounding metadata.

Depending on the workflow, an observer may still learn information such as:

* The existence of an encrypted file.
* The file size.
* The approximate plaintext size.
* File-system metadata maintained by Windows.

### 6. Password recovery is intentionally unavailable

CipherDesk does not provide a password recovery mechanism or master recovery key.

If the encryption password is lost, the encrypted data may be permanently unrecoverable.

This is an intentional property of the encryption model, not a missing feature.

---

## Compatibility format warning

CipherDesk 2.x can read the original **AES-256-CBC** format for backward compatibility.

The compatibility format uses the original application's cryptographic construction and does not provide the authenticated encryption guarantees of AES-256-GCM.

In particular, it does not provide modern authenticated integrity protection.

For this reason:

> **AES-256-CBC is a compatibility format, not the recommended format for new encryption.**

Users with existing CBC-encrypted data should decrypt it and re-encrypt it using **AES-256-GCM** whenever possible.

The CBC implementation should not be modified merely to make it "more secure", because changing its behaviour would break compatibility with existing ciphertext. Security improvements belong in the modern format.

---

## Disclosure

Please allow reasonable time for a vulnerability to be investigated and fixed before publicly disclosing technical details.

If a vulnerability affects existing encrypted data or requires an urgent workaround, please mention this clearly in the initial report.

We will coordinate disclosure timing with the reporter when practical.

---

## Security disclaimer

CipherDesk has not been independently audited.

No software can guarantee absolute security, and CipherDesk is not intended to replace professionally audited encryption systems in high-risk environments.

The security of encrypted data ultimately depends on the cryptographic implementation, the password chosen by the user, the security of the host operating system, and the environment in which the application is used.
