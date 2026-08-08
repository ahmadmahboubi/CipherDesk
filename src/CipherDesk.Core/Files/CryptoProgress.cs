using System;

namespace CipherDesk.Core.Files;

/// <summary>Progress snapshot reported while a file is being processed.</summary>
public readonly record struct CryptoProgress(long BytesProcessed, long TotalBytes)
{
    public double Fraction => TotalBytes <= 0 ? 0d : Math.Clamp((double)BytesProcessed / TotalBytes, 0d, 1d);

    public int Percent => (int)Math.Round(Fraction * 100d);
}
