#Requires -Version 5.1
#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [ValidatePattern('^[D-Z]$')]
    [string]$DriveLetter = 'R',

    [string]$DotNetPath = 'dotnet',

    [string]$TranscriptPath,

    [switch]$KeepVhd
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $projectRoot 'artifacts\integration'
$runId = [Guid]::NewGuid().ToString('N')
$runDirectory = Join-Path $artifactRoot $runId
$backupDirectory = Join-Path $runDirectory 'backups'
$vhdPath = Join-Path $runDirectory 'BitLockerDescriptionChanger-Test.vhdx'
$integrationDll = Join-Path $projectRoot 'tests\BitLockerDescriptionChanger.IntegrationTests\bin\Release\net10.0-windows\win-x64\BitLockerDescriptionChanger.IntegrationTests.dll'
$mountPoint = "${DriveLetter}:"
$finalDescription = 'BDC FINAL LOCK UNLOCK'
$mounted = $false
$transcribing = $false

if (-not [string]::IsNullOrWhiteSpace($TranscriptPath)) {
    Start-Transcript -LiteralPath $TranscriptPath -Force | Out-Null
    $transcribing = $true
}

function Invoke-IntegrationExecutable {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & $DotNetPath $integrationDll @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "The integration executable failed with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$resolvedArtifactRoot = [IO.Path]::GetFullPath($artifactRoot) + [IO.Path]::DirectorySeparatorChar
$resolvedRunDirectory = [IO.Path]::GetFullPath($runDirectory) + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedRunDirectory.StartsWith($resolvedArtifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The integration run directory escaped the project artifact directory.'
}

if (Get-Volume -DriveLetter $DriveLetter -ErrorAction SilentlyContinue) {
    throw "Drive letter $mountPoint is already in use."
}

if (-not (Test-Path -LiteralPath $integrationDll -PathType Leaf)) {
    throw "Build the integration project in Release before running this script: $integrationDll"
}

try {
    $env:BDC_INTEGRATION_TEST = '1'
    Invoke-IntegrationExecutable -Arguments @('read-only-system-discovery')

    New-VHD -Path $vhdPath -SizeBytes 256MB -Fixed | Out-Null
    $disk = Mount-VHD -Path $vhdPath -Passthru
    $mounted = $true
    $disk = $disk | Get-Disk
    Initialize-Disk -Number $disk.Number -PartitionStyle GPT -PassThru | Out-Null
    $partition = New-Partition -DiskNumber $disk.Number -UseMaximumSize -DriveLetter $DriveLetter
    Format-Volume -Partition $partition -FileSystem NTFS -NewFileSystemLabel 'BDC_INTEGRATION' -Confirm:$false | Out-Null

    Enable-BitLocker `
        -MountPoint $mountPoint `
        -EncryptionMethod XtsAes128 `
        -RecoveryPasswordProtector `
        -UsedSpaceOnly `
        -SkipHardwareTest `
        -WarningAction SilentlyContinue | Out-Null

    $deadline = [DateTime]::UtcNow.AddMinutes(5)
    do {
        $bitLockerVolume = Get-BitLockerVolume -MountPoint $mountPoint
        if ($bitLockerVolume.VolumeStatus -eq 'FullyEncrypted' -and $bitLockerVolume.ProtectionStatus -eq 'On') {
            break
        }

        if ([DateTime]::UtcNow -ge $deadline) {
            throw 'Timed out waiting for the disposable VHD to become fully encrypted with protection enabled.'
        }

        Start-Sleep -Milliseconds 500
    } while ($true)

    $exerciseArguments = @(
        'exercise',
        '--drive', $mountPoint,
        '--backup-directory', $backupDirectory,
        '--final-description', $finalDescription
    )
    Invoke-IntegrationExecutable -Arguments $exerciseArguments

    $recoveryProtector = $bitLockerVolume.KeyProtector |
        Where-Object KeyProtectorType -EQ 'RecoveryPassword' |
        Select-Object -First 1
    if (-not $recoveryProtector -or [string]::IsNullOrWhiteSpace($recoveryProtector.RecoveryPassword)) {
        throw 'The disposable VHD does not have a usable recovery password protector.'
    }

    $recoveryPassword = $recoveryProtector.RecoveryPassword
    Lock-BitLocker -MountPoint $mountPoint -ForceDismount -Confirm:$false
    if ((Get-BitLockerVolume -MountPoint $mountPoint).LockStatus -ne 'Locked') {
        throw 'The disposable VHD did not enter the locked state.'
    }

    Unlock-BitLocker -MountPoint $mountPoint -RecoveryPassword $recoveryPassword | Out-Null
    if ((Get-BitLockerVolume -MountPoint $mountPoint).LockStatus -ne 'Unlocked') {
        throw 'The disposable VHD did not unlock with its recovery password.'
    }

    Invoke-IntegrationExecutable -Arguments @(
        'verify',
        '--drive', $mountPoint,
        '--backup-directory', $backupDirectory,
        '--final-description', $finalDescription
    )

    Write-Host "Integration lab passed. Disposable VHD: $vhdPath"
}
finally {
    try {
        Remove-Item Env:BDC_INTEGRATION_TEST -ErrorAction SilentlyContinue
        if ($mounted) {
            Dismount-VHD -Path $vhdPath -ErrorAction SilentlyContinue
        }

        if (-not $KeepVhd -and (Test-Path -LiteralPath $runDirectory)) {
            $resolvedDeleteTarget = [IO.Path]::GetFullPath($runDirectory) + [IO.Path]::DirectorySeparatorChar
            if (-not $resolvedDeleteTarget.StartsWith($resolvedArtifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Refusing to remove an integration run directory outside the project artifact directory.'
            }

            Remove-Item -LiteralPath $runDirectory -Recurse -Force
        }
    }
    finally {
        if ($transcribing) {
            Stop-Transcript | Out-Null
        }
    }
}
