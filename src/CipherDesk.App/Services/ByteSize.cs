namespace CipherDesk.App.Services;

/// <summary>Formats byte counts for display. One implementation, used by every view.</summary>
public static class ByteSize
{
    public static string Format(long bytes) => bytes switch
    {
        < 0 => "0 B",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024d):F1} MB",
        _ => $"{bytes / (1024d * 1024d * 1024d):F2} GB"
    };
}
