# Migrating from the Legacy CBC Format

If you have data encrypted by version 1.x of the original application, CipherDesk can still read it.
This guide explains how to migrate that data to the recommended Modern format.

## Why migrate

The original v1 format used **AES-256-CBC** with a fixed all-zero IV and without authentication.

Its key derivation was also fundamentally weak. The password was Base64-encoded and padded to produce
the AES key. Base64 is an encoding, not a password hashing or key derivation function.

The format therefore provided:

* No random salt
* No password stretching
* No authentication tag
* No integrity protection
* A fixed IV
* Deterministic ciphertext for identical plaintext and password combinations

In practice, this means an attacker who obtains an old ciphertext can attempt password guesses without
the protection provided by a modern password-based key derivation function. Ciphertext can also be
modified without the original format being able to reliably detect the modification.

The Modern format addresses these problems by using AES-256-GCM with password-based key derivation,
random per-message parameters and authenticated ciphertext.

See [`FILE-FORMAT.md`](FILE-FORMAT.md) for the detailed format specification.

## Migrating text

To migrate an existing CBC-encrypted text payload:

1. Open CipherDesk and switch to the **Text** workspace with `Ctrl+1`.
2. Paste the existing encrypted text into the input box.
3. Select **CBC** as the decryption format if automatic detection does not identify it.
4. Enter the password that was used to encrypt the original data.
5. Press **Decrypt** or use `Ctrl+D`.
6. Verify that the decrypted plaintext is correct.
7. Press **Use result as input** to move the plaintext into the input box.
8. Enter a new, strong password.
9. Select the **Modern / AES-256-GCM** format.
10. Press **Encrypt** or use `Ctrl+E`.
11. Save the new ciphertext with `Ctrl+S`.
12. Verify the migration by clearing the workspace, loading the new ciphertext and decrypting it with
    the new password.
13. Keep the original CBC ciphertext until you have successfully verified the migrated data.

Do not delete the original encrypted data immediately after encryption. Verification first, deletion
later. Computers are extremely good at following instructions and extremely bad at feeling remorse.

## Choose a new password

If a password was used with the original v1 CBC format, consider it compromised.

The original format did not use a proper password-based key derivation function, so passwords could be
tested much more efficiently than with the Modern format.

If the same password was used elsewhere, change it there as well.

For new encryption, use a strong, unique password. CipherDesk includes a password-strength indicator
and a password generator based on a cryptographically secure random number generator.

## Modern format

New encrypted data should use the **Modern** format.

The Modern format uses:

* AES-256-GCM
* PBKDF2-HMAC-SHA256
* A random 128-bit salt
* A random 96-bit nonce for text messages
* Authenticated ciphertext
* Versioned format metadata
* Authentication of the encrypted payload and associated format data

The Modern format is the default encryption format and should be preferred for all new data.

## File encryption

The original v1 application did not provide a dedicated file-encryption format.

CipherDesk's Files workspace uses the Modern encryption system and processes files as authenticated
chunks rather than loading the entire file into memory.

There is therefore no v1 file format that needs to be migrated.

If you previously encrypted file contents by copying them into the text workspace, decrypt and migrate
those contents as text first. For future files, use the **Files** workspace.

## Batch migration

CipherDesk does not provide an automatic bulk migration operation.

This is intentional. A bulk migration process that decrypts and re-encrypts an archive without
verification can turn one incorrect assumption into a very efficient data-loss machine.

If automation is required, use `CipherDesk.Core` and verify every migrated payload before deleting the
original.

The conceptual process is:

```csharp
var cbc = new CbcTextCipher();
var modern = new ModernTextCipher();

string plaintext = cbc.Decrypt(oldCiphertext, oldPassword);
string migrated = modern.Encrypt(plaintext, newPassword);

string verification = modern.Decrypt(migrated, newPassword);

if (verification != plaintext)
{
    throw new InvalidOperationException(
        "Migration verification failed. Keep the original ciphertext.");
}
```

The recommended workflow for batch migration is:

1. Read the original CBC ciphertext.
2. Decrypt it with the original password.
3. Encrypt the plaintext using the Modern format and a new password.
4. Decrypt the newly generated ciphertext.
5. Compare the verified plaintext with the original plaintext.
6. Write the new ciphertext alongside the original.
7. Keep the original until the complete migration has been verified.
8. Delete old ciphertext only in a separate cleanup operation.

Never overwrite the only copy of the original encrypted data during migration.

## Password length limitation in the original format

The original implementation had an important limitation caused by its password-to-key conversion.

Passwords longer than 24 bytes could not produce a valid 32-byte AES key after Base64 encoding. The
original application could therefore fail with a `CryptographicException` instead of producing a valid
ciphertext.

CipherDesk handles this condition explicitly rather than allowing an unhandled exception to terminate
the application.

If no ciphertext was successfully produced by the original application, there is no encrypted data to
recover.

## After migration

Once you have verified that your migrated data can be decrypted successfully:

* Use **Modern / AES-256-GCM** for all new encryption.
* Use a new password rather than reusing the old CBC password.
* Keep backups of important encrypted data.
* Do not assume encryption removes the need for backups.
* Treat the old CBC format as a compatibility feature, not a recommended encryption format.

The CBC implementation remains available so existing data can be recovered. It is not intended to be
the default or preferred format for new encrypted data.
