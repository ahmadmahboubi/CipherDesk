using System;

namespace CipherDesk.Core;

/// <summary>Base type for every error the cipher layer raises on purpose.</summary>
public class CipherDeskCryptoException : Exception
{
    public CipherDeskCryptoException(string message) : base(message) { }
    public CipherDeskCryptoException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The payload could not be authenticated or unpadded - almost always a wrong password.</summary>
public sealed class InvalidPasswordException : CipherDeskCryptoException
{
    public InvalidPasswordException()
        : base("The password is incorrect, or the data has been modified since it was encrypted.") { }

    public InvalidPasswordException(Exception inner)
        : base("The password is incorrect, or the data has been modified since it was encrypted.", inner) { }
}

/// <summary>The payload is not valid CipherDesk data (bad Base64, truncated, unknown version...).</summary>
public sealed class MalformedPayloadException : CipherDeskCryptoException
{
    public MalformedPayloadException(string message) : base(message) { }
    public MalformedPayloadException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The request is valid CipherDesk usage but violates a documented constraint of the chosen format.</summary>
public sealed class UnsupportedOperationException : CipherDeskCryptoException
{
    public UnsupportedOperationException(string message) : base(message) { }
}
