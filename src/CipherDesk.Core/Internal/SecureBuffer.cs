using System;
using System.Security.Cryptography;

namespace CipherDesk.Core.Internal;

/// <summary>
/// A byte buffer that zeroes itself when disposed. Used for derived keys so they do not
/// linger in the managed heap until a garbage collection happens to overwrite them.
/// </summary>
/// <remarks>
/// This is defence in depth, not a guarantee: the CLR may still move or copy the array.
/// It removes the easy wins (heap dumps, crash dumps, page files) rather than all of them.
/// </remarks>
internal sealed class SecureBuffer : IDisposable
{
    private byte[]? _buffer;

    public SecureBuffer(int length) => _buffer = new byte[length];

    public SecureBuffer(byte[] takeOwnership) => _buffer = takeOwnership;

    public byte[] Bytes => _buffer ?? throw new ObjectDisposedException(nameof(SecureBuffer));

    public Span<byte> Span => Bytes.AsSpan();

    public void Dispose()
    {
        if (_buffer is null) return;
        CryptographicOperations.ZeroMemory(_buffer);
        _buffer = null;
    }
}
