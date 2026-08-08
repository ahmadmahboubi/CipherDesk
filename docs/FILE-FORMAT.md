# CipherDesk file formats

This document is normative. If the code and this document disagree, one of them is a bug.

All multi-byte integers are **big-endian**. Byte offsets are zero-based and inclusive.

---

## Format v2 — the current format

Two containers share one header: a Base64 text payload and a chunked binary file payload. The `kind`
byte in the header says which.

### Header (32 bytes)

Present at the start of every v2 payload, and **authenticated as GCM associated data** rather than
encrypted. It is readable by anyone, and unalterable by anyone without the password.

| Offset | Size | Field | Value |
| --- | --- | --- | --- |
| 0 | 4 | `magic` | ASCII `CDSK` = `43 44 53 4B` |
| 4 | 1 | `version` | `0x02` |
| 5 | 1 | `kind` | `0x01` text, `0x02` file stream |
| 6 | 1 | `kdfId` | `0x01` = PBKDF2-HMAC-SHA256 |
| 7 | 1 | `cipherId` | `0x01` = AES-256-GCM |
| 8 | 4 | `iterations` | PBKDF2 iteration count, uint32 |
| 12 | 16 | `salt` | PBKDF2 salt, from a CSPRNG, unique per payload |
| 28 | 4 | `chunkSize` | Plaintext bytes per chunk. `0` for `kind = 0x01` |

The iteration count is stored rather than assumed, so raising the work factor for new data does not
break the ability to read old data. A reader accepts any value in the documented range and uses what
the payload declares.

Parsing is strict. A reader **must** reject the payload if the magic does not match, the version is
unrecognised, `kind`/`kdfId`/`cipherId` are unknown, `iterations` falls outside 10,000–10,000,000, or
`chunkSize` falls outside 64 KiB–64 MiB for a stream payload.

### Key derivation

```
key = PBKDF2-HMAC-SHA256(password = UTF-8(password), salt = header.salt,
                         iterations = header.iterations, dkLen = 32)
```

The default iteration count for new payloads is **210,000**, following current OWASP guidance for
PBKDF2-HMAC-SHA256. The password is processed as a UTF-8 byte span; it is never interned as a string.

### Text payload (`kind = 0x01`)

```
header (32) || nonce (12) || ciphertext (n) || tag (16)
```

The whole thing is Base64-encoded for display. The plaintext is the UTF-8 encoding of the message with
no byte-order mark. GCM associated data is the 32-byte header.

Minimum length is 60 bytes before Base64. A payload shorter than that is malformed.

### File payload (`kind = 0x02`)

```
header (32) || noncePrefix (8) || chunk[0] || chunk[1] || … || chunk[k]
```

Each chunk is a 5-byte plaintext chunk header followed by the sealed body:

```
length  (4)       — uint32, plaintext bytes in this chunk, 0 … chunkSize
isFinal (1)       — 0x01 on the last chunk, 0x00 otherwise
ciphertext (length)
tag     (16)
```

The chunk header is not encrypted, but both of its fields are covered by the chunk's associated data,
so neither can be altered without the tag check failing.

The nonce for chunk *i* is the 8-byte random `noncePrefix` followed by *i* as a big-endian uint32. The
prefix is random per file, so nonces never repeat across files under the same key, and the counter
guarantees they never repeat within one.

Associated data for chunk *i* is:

```
header (32) || noncePrefix (8) || i (4, big-endian) || isFinal (1)
```

`isFinal` is `0x01` for the last chunk and `0x00` otherwise. Binding all four things is what makes the
container safe against structural attacks: an attacker cannot reorder chunks (the index is bound), move
a chunk into another file (the header and prefix are bound), duplicate a chunk (the index is bound), or
truncate the file (the final chunk is the only one that authenticates with `isFinal = 1`).

Chunk indices start at 0. A zero-length file encrypts to a header, a nonce prefix, and a single chunk
whose `length` is 0 and whose `isFinal` is 1 — the tag still authenticates, so an empty file is
distinguishable from a truncated one.

The default `chunkSize` is 1 MiB (1,048,576), which keeps memory flat regardless of file size. The
conventional extension is `.cdsk`.

### Decryption

Any authentication failure — wrong password, altered ciphertext, altered header, altered chunk order —
surfaces as one indistinguishable error. Readers must not report *which* check failed, and must not
attempt a partial or best-effort decryption of a payload that fails to authenticate.

---

## Format v1 — legacy, read for compatibility

> **This format is cryptographically broken.** It is implemented so that data encrypted by version 1.x
> of the application is not lost. Do not use it for anything new. `LegacyTextCipher` is pinned by golden
> test vectors and must never be modified.

### Layout

```
Base64( AES-256-CBC-PKCS7( UTF-8-without-BOM(plaintext) ) )
```

There is no header, no magic, no salt, no nonce and no authentication tag. The ciphertext is
indistinguishable from random Base64, which is why CipherDesk detects format by looking for the v2
`CDSK` signature and treats anything else as v1 — never by trial decryption, which would make a wrong
password indistinguishable from a wrong format.

### Key derivation

```
s = Base64(UTF-8(password))
d = 0
while length(s) < 32:
    s = s + decimal_string(d)
    d = d + 1
key = ASCII(s)[0 … 31]
```

The IV is **16 zero bytes**.

### Why this is broken

| Problem | Consequence |
| --- | --- |
| Base64 is an encoding, not a hash | The key decodes straight back to the password. There is no one-way step at all. |
| No stretching | Guessing costs one Base64 encode and one AES operation. A commodity GPU tries billions of candidates per second. |
| No salt | Rainbow tables work, and the same password always yields the same key across every user and message. |
| Fixed all-zero IV | Encrypting the same plaintext twice yields byte-identical output. Equal prefixes are visible as equal ciphertext blocks. |
| CBC with no MAC | Ciphertext can be altered undetectably, and the padding check is a padding oracle. |

### The 24-byte password limit

Base64 expands *n* bytes to `4·ceil(n/3)` characters. For a 25-byte password that is 36 characters,
which already exceeds 32, so the padding loop never runs and the key ends up 36 bytes — not a legal AES
key length. **The original application threw an unhandled `CryptographicException` and terminated.**

`LegacyTextCipher` therefore exposes `MaxPasswordBytes = 24` and raises a clear, explanatory error
instead of crashing. The v2 format has no such limit.

### Migrating

Decrypt once with v1, re-encrypt with v2, and destroy the v1 copy. See
[`MIGRATION.md`](MIGRATION.md). Because a v1 password may be recoverable from any leaked v1 key
material, treat a password that has ever been used with v1 as compromised and choose a new one.

---

## Test vectors

The v1 vectors in `tests/CipherDesk.Core.Tests/LegacyCompatibilityTests.cs` were produced by an
**independent implementation** of the algorithm above, written against a different cryptography library
specifically so that it could not inherit a bug from this codebase.

**Never regenerate those vectors from CipherDesk's own output.** Doing so converts a compatibility test
into a tautology, and the next accidental change to key derivation will pass silently.

v2 has no fixed vectors, because every payload uses a random salt and nonce and is therefore
non-deterministic by design. It is tested by round trip and by tamper detection instead.
