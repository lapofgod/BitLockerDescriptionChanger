using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public static partial class FveApiIdentityService
{
    private static readonly string[] RequiredExports =
    [
        "FveOpenVolumeW",
        "FveGetDescriptionW",
        "FveSetDescriptionW",
        "FveCommitChanges",
        "FveDiscardChanges",
        "FveCloseVolume"
    ];

    public static FveApiIdentity Inspect()
    {
        var filePath = Path.Combine(Environment.SystemDirectory, "fveapi.dll");
        if (!File.Exists(filePath))
        {
            return new FveApiIdentity(
                filePath,
                "Not found",
                string.Empty,
                false,
                FveCompatibilityLevel.Unavailable,
                "The system BitLocker API library was not found.");
        }

        var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
        var version = versionInfo.ProductVersion?.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
            ?? versionInfo.FileVersion?.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
            ?? "Unknown";
        var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)));

        nint module = LoadLibraryExW(filePath, 0, LoadLibrarySearchSystem32);
        if (module == 0)
        {
            var error = Marshal.GetLastWin32Error();
            return new FveApiIdentity(
                filePath,
                version,
                sha256,
                false,
                FveCompatibilityLevel.Unavailable,
                $"fveapi.dll could not be loaded (Win32 error {error}).");
        }

        try
        {
            var missing = RequiredExports.Where(name => GetProcAddress(module, name) == 0).ToArray();
            if (missing.Length != 0)
            {
                return new FveApiIdentity(
                    filePath,
                    version,
                    sha256,
                    false,
                    FveCompatibilityLevel.Unavailable,
                    $"Required private exports are missing: {string.Join(", ", missing)}.");
            }
        }
        finally
        {
            _ = FreeLibrary(module);
        }

        return new FveApiIdentity(
            filePath,
            version,
            sha256,
            true,
            FveCompatibilityLevel.Experimental,
            "The required private exports are present. Per-volume read-only ABI checks and explicit risk acceptance are still required.");
    }

    private const uint LoadLibrarySearchSystem32 = 0x00000800;

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibraryExW(string fileName, nint file, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetProcAddress(nint module, string name);

    [LibraryImport("kernel32.dll", EntryPoint = "FreeLibrary", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeLibrary(nint module);
}
