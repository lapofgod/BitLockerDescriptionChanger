using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed partial class DiskExtentReader
{
    private const uint IoctlVolumeGetVolumeDiskExtents = 0x00560000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The reader is an injectable service boundary.")]
    public DiskExtent GetSingleExtent(string nativeVolumePath)
    {
        using var handle = CreateFileW(
            nativeVolumePath,
            0,
            FileShareRead | FileShareWrite | FileShareDelete,
            0,
            OpenExisting,
            0,
            0);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The volume could not be opened for extent discovery.");
        }

        var buffer = new byte[16 * 1024];
        if (!DeviceIoControl(
                handle,
                IoctlVolumeGetVolumeDiskExtents,
                0,
                0,
                buffer,
                (uint)buffer.Length,
                out _,
                0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS failed.");
        }

        var extentCount = BitConverter.ToUInt32(buffer, 0);
        if (extentCount != 1)
        {
            throw new NotSupportedException($"The volume has {extentCount} disk extents; exactly one is required.");
        }

        const int firstExtentOffset = 8;
        var diskNumber = BitConverter.ToInt32(buffer, firstExtentOffset);
        var startingOffset = BitConverter.ToInt64(buffer, firstExtentOffset + 8);
        var extentLength = BitConverter.ToInt64(buffer, firstExtentOffset + 16);
        if (diskNumber < 0 || startingOffset < 0 || extentLength <= 0)
        {
            throw new InvalidDataException("The volume returned invalid disk extent geometry.");
        }

        return new DiskExtent(diskNumber, startingOffset, extentLength);
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

    [LibraryImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        nint inputBuffer,
        uint inputBufferSize,
        [Out] byte[] outputBuffer,
        uint outputBufferSize,
        out uint bytesReturned,
        nint overlapped);
}
