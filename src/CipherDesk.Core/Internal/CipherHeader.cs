using System;
using System.Buffers.Binary;

namespace CipherDesk.Core.Internal;

/// <summary>
/// The fixed 32-byte preamble that starts every CipherDesk v2 payload.
/// It is authenticated as additional data, so tampering with any field fails the tag check.
/// The layout is documented in docs/FILE-FORMAT.md and must not change without a version bump.
/// </summary>
internal readonly struct CipherHeader
{
    public const int Size = 32;
    public const int SaltSize = 16;
    public const byte CurrentVersion = 2;

    public const byte KindText = 1;    // single-shot payload, one nonce and one tag
    public const byte KindStream = 2;  // chunked payload for files

    public const byte KdfPbkdf2Sha256 = 1;
    public const byte CipherAesGcm256 = 1;

    /// <summary>ASCII "CDSK". Lets us auto-detect the format without trial decryption.</summary>
    private static ReadOnlySpan<byte> Magic => new byte[] { 0x43, 0x44, 0x53, 0x4B };

    public byte Version { get; }
    public byte Kind { get; }
    public byte KdfId { get; }
    public byte CipherId { get; }
    public int Iterations { get; }
    public byte[] Salt { get; }
    public int ChunkSize { get; }

    public CipherHeader(byte kind, int iterations, byte[] salt, int chunkSize)
    {
        Version = CurrentVersion;
        Kind = kind;
        KdfId = KdfPbkdf2Sha256;
        CipherId = CipherAesGcm256;
        Iterations = iterations;
        Salt = salt;
        ChunkSize = chunkSize;
    }

    private CipherHeader(byte version, byte kind, byte kdfId, byte cipherId, int iterations, byte[] salt, int chunkSize)
    {
        Version = version;
        Kind = kind;
        KdfId = kdfId;
        CipherId = cipherId;
        Iterations = iterations;
        Salt = salt;
        ChunkSize = chunkSize;
    }

    /// <summary>Serialises the header into a freshly allocated 32-byte array.</summary>
    public byte[] ToBytes()
    {
        var buffer = new byte[Size];
        Magic.CopyTo(buffer.AsSpan(0, 4));
        buffer[4] = Version;
        buffer[5] = Kind;
        buffer[6] = KdfId;
        buffer[7] = CipherId;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(8, 4), Iterations);
        Salt.CopyTo(buffer, 12);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(28, 4), ChunkSize);
        return buffer;
    }

    /// <summary>Cheap format probe used by auto-detection. Never throws.</summary>
    public static bool HasMagic(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && data[0] == 0x43 && data[1] == 0x44 && data[2] == 0x53 && data[3] == 0x4B;

    /// <summary>Parses and validates a header, throwing <see cref="MalformedPayloadException"/> on anything unexpected.</summary>
    public static CipherHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
            throw new MalformedPayloadException("The data is too short to be a CipherDesk payload.");

        if (!HasMagic(data))
            throw new MalformedPayloadException("The data does not carry the CipherDesk signature.");

        byte version = data[4];
        if (version != CurrentVersion)
            throw new MalformedPayloadException(
                $"This payload uses CipherDesk format v{version}, which this build cannot read. Please update the application.");

        byte kind = data[5];
        if (kind is not (KindText or KindStream))
            throw new MalformedPayloadException($"Unknown payload kind 0x{kind:X2}.");

        byte kdfId = data[6];
        if (kdfId != KdfPbkdf2Sha256)
            throw new MalformedPayloadException($"Unsupported key derivation function 0x{kdfId:X2}.");

        byte cipherId = data[7];
        if (cipherId != CipherAesGcm256)
            throw new MalformedPayloadException($"Unsupported cipher 0x{cipherId:X2}.");

        int iterations = BinaryPrimitives.ReadInt32BigEndian(data.Slice(8, 4));
        if (iterations is < 1_000 or > 20_000_000)
            throw new MalformedPayloadException("The declared iteration count is outside the accepted range.");

        byte[] salt = data.Slice(12, SaltSize).ToArray();

        int chunkSize = BinaryPrimitives.ReadInt32BigEndian(data.Slice(28, 4));
        if (chunkSize < 0 || chunkSize > 64 * 1024 * 1024)
            throw new MalformedPayloadException("The declared chunk size is outside the accepted range.");

        return new CipherHeader(version, kind, kdfId, cipherId, iterations, salt, chunkSize);
    }
}
