using System.Management;
using System.Diagnostics.CodeAnalysis;
using BitLockerDescriptionChanger.Models;

namespace BitLockerDescriptionChanger.Services;

public sealed class WmiBitLockerReader : IVolumeSecurityStateReader
{
    private const string NamespacePath = @"root\CIMV2\Security\MicrosoftVolumeEncryption";

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The reader is an injectable service boundary.")]
    public IReadOnlyList<(string DeviceId, string DriveLetter, VolumeSecurityState State)> ReadAll()
    {
        var scope = CreateConnectedScope();
        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_EncryptableVolume"));
        using var collection = searcher.Get();
        var result = new List<(string, string, VolumeSecurityState)>();
        foreach (ManagementObject volume in collection)
        {
            using (volume)
            {
                var deviceId = Convert.ToString(volume["DeviceID"], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    continue;
                }

                var driveLetter = Convert.ToString(volume["DriveLetter"], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                var state = ReadState(volume);
                if (state.ConversionStatus == 0 && state.KeyProtectors.Count == 0)
                {
                    continue;
                }

                result.Add((deviceId, driveLetter, state));
            }
        }

        return result;
    }

    public VolumeSecurityState Read(string deviceId)
    {
        var match = ReadAll().SingleOrDefault(item => string.Equals(item.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(match.DeviceId)
            ? throw new InvalidOperationException($"The BitLocker volume '{deviceId}' was not returned by WMI.")
            : match.State;
    }

    private static VolumeSecurityState ReadState(ManagementObject volume)
    {
        var lockStatus = InvokeUInt32NoInput(volume, "GetLockStatus", "LockStatus");
        var protectionStatus = ReadUInt32Property(volume, "ProtectionStatus");
        var conversionStatus = ReadUInt32Property(volume, "ConversionStatus");
        var encryptionMethod = ReadUInt32Property(volume, "EncryptionMethod");
        var volumeType = ReadUInt32Property(volume, "VolumeType");
        return new VolumeSecurityState(
            lockStatus,
            conversionStatus,
            protectionStatus,
            encryptionMethod,
            volumeType,
            ReadProtectors(volume));
    }

    private static KeyProtectorInfo[] ReadProtectors(ManagementObject volume)
    {
        using var input = CreateMethodInput(volume, "GetKeyProtectors");
        input["KeyProtectorType"] = 0u;
        using var output = volume.InvokeMethod("GetKeyProtectors", input, null)
            ?? throw new InvalidOperationException("GetKeyProtectors returned no WMI result.");
        EnsureWmiSuccess(output, "GetKeyProtectors");
        var ids = output["VolumeKeyProtectorID"] as string[] ?? [];
        var protectors = new List<KeyProtectorInfo>(ids.Length);
        foreach (var id in ids)
        {
            using var typeInput = CreateMethodInput(volume, "GetKeyProtectorType");
            typeInput["VolumeKeyProtectorID"] = id;
            using var typeOutput = volume.InvokeMethod("GetKeyProtectorType", typeInput, null)
                ?? throw new InvalidOperationException("GetKeyProtectorType returned no WMI result.");
            EnsureWmiSuccess(typeOutput, "GetKeyProtectorType");
            protectors.Add(new KeyProtectorInfo(id, Convert.ToUInt32(typeOutput["KeyProtectorType"], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return protectors.OrderBy(protector => protector.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static uint InvokeUInt32NoInput(
        ManagementObject volume,
        string methodName,
        string outputName)
    {
        using var output = volume.InvokeMethod(methodName, null, null)
            ?? throw new InvalidOperationException($"{methodName} returned no WMI result.");
        EnsureWmiSuccess(output, methodName);
        return Convert.ToUInt32(output[outputName], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static uint ReadUInt32Property(ManagementObject volume, string propertyName)
    {
        var value = volume[propertyName]
            ?? throw new InvalidOperationException($"Win32_EncryptableVolume.{propertyName} returned no value.");
        return Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static ManagementBaseObject CreateMethodInput(ManagementObject volume, string methodName)
    {
        using var volumeClass = new ManagementClass(
            volume.Scope,
            new ManagementPath(volume.ClassPath.ClassName),
            null);
        return volumeClass.GetMethodParameters(methodName);
    }

    private static void EnsureWmiSuccess(ManagementBaseObject output, string methodName)
    {
        var returnValue = Convert.ToUInt32(output["ReturnValue"], System.Globalization.CultureInfo.InvariantCulture);
        if (returnValue != 0)
        {
            throw new InvalidOperationException($"{methodName} failed with 0x{returnValue:X8}.");
        }
    }

    private static ManagementScope CreateConnectedScope()
    {
        var scope = new ManagementScope($@"\\.\{NamespacePath}")
        {
            Options = new ConnectionOptions
            {
                EnablePrivileges = true,
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.PacketPrivacy
            }
        };
        scope.Connect();
        return scope;
    }
}
