using System;
using System.IO;
using System.Security.Cryptography;
using CipherDesk.Core;

namespace CipherDesk.App.Services;

/// <summary>A message pair ready to show to the user.</summary>
public readonly record struct UserFacingError(string Title, string Message);

/// <summary>
/// Turns exceptions into something a person can act on.
/// </summary>
/// <remarks>
/// This is a security control as much as a usability one. Raw exception text can leak file paths,
/// user names and internal state, and the difference between "bad padding" and "bad tag" is exactly
/// the signal a padding-oracle attack feeds on. Every crypto failure is therefore collapsed into
/// one indistinguishable message, and unexpected exceptions never reach the screen verbatim.
/// </remarks>
public static class ErrorPresenter
{
    public static UserFacingError Describe(Exception exception) => exception switch
    {
        InvalidPasswordException => new UserFacingError(
            "Could not decrypt",
            "The password is incorrect, or the data was changed after it was encrypted.\n\n" +
            "Check for stray spaces or line breaks if you pasted the text from elsewhere."),

        MalformedPayloadException malformed => new UserFacingError(
            "This does not look like CipherDesk data",
            malformed.Message),

        UnsupportedOperationException unsupported => new UserFacingError(
            "Not supported in this mode",
            unsupported.Message),

        OperationCanceledException => new UserFacingError(
            "Cancelled",
            "The operation was cancelled. Nothing was written."),

        FileNotFoundException => new UserFacingError(
            "File not found",
            "The file no longer exists at that location. It may have been moved or deleted."),

        DirectoryNotFoundException => new UserFacingError(
            "Folder not found",
            "The destination folder does not exist."),

        UnauthorizedAccessException => new UserFacingError(
            "Access denied",
            "Windows would not allow access to that file. It may be read-only, in use, or owned by another user."),

        IOException io when IsDiskFull(io) => new UserFacingError(
            "Not enough space",
            "There is not enough free disk space to write the result."),

        IOException => new UserFacingError(
            "File error",
            "The file could not be read or written. It may be open in another program."),

        CryptographicException => new UserFacingError(
            "Could not decrypt",
            "The password is incorrect, or the data was changed after it was encrypted."),

        OutOfMemoryException => new UserFacingError(
            "Not enough memory",
            "The input is too large to process in memory. Use the Files tab, which streams instead."),

        // Anything unanticipated: acknowledge it without echoing internal detail to the screen.
        _ => new UserFacingError(
            "Something went wrong",
            "An unexpected error stopped the operation. Nothing was written.\n\n" +
            "If this keeps happening, please open an issue on GitHub with the steps that trigger it.")
    };

    /// <summary>HRESULTs for ERROR_DISK_FULL and ERROR_HANDLE_DISK_FULL.</summary>
    private static bool IsDiskFull(IOException exception)
    {
        const int DiskFull = unchecked((int)0x80070070);
        const int HandleDiskFull = unchecked((int)0x80070027);
        return exception.HResult == DiskFull || exception.HResult == HandleDiskFull;
    }
}
