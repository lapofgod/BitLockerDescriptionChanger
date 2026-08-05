using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BitLockerDescriptionChanger.Services;

public sealed partial class PhysicalDiskReader : IPhysicalDiskReader
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The reader is an injectable service boundary.")]
    public byte[] Read(int diskNumber, long absoluteOffset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(diskNumber);
        ArgumentOutOfRangeException.ThrowIfNegative(absoluteOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        var path = $"\\\\.\\PhysicalDrive{diskNumber}";
        using var handle = CreateFileW(
            path,
            GenericRead,
            FileShareRead | FileShareWrite | FileShareDelete,
            0,
            OpenExisting,
            0,
            0);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"{path} could not be opened for read-only metadata access.");
        }

        var result = new byte[length];
        var totalRead = 0;
        while (totalRead < result.Length)
        {
            var read = RandomAccess.Read(handle, result.AsSpan(totalRead), absoluteOffset + totalRead);
            if (read == 0)
            {
                throw new EndOfStreamException($"A short read occurred at physical offset {absoluteOffset + totalRead}.");
            }

            totalRead += read;
        }

        return result;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);
}
