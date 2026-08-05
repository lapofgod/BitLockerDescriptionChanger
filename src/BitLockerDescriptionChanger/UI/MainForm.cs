using System.Diagnostics;
using System.Globalization;
using System.Text;
using BitLockerDescriptionChanger.Models;
using BitLockerDescriptionChanger.Services;

namespace BitLockerDescriptionChanger.UI;

public sealed partial class MainForm : Form
{
    private readonly FveApiIdentity _identity;
    private readonly IFveApi _fveApi;
    private readonly IVolumeDiscoveryService _discoveryService;
    private readonly DescriptionChangeCoordinator _changeCoordinator;
    private readonly AppLogger _logger;
    private bool _busy;
    private bool _writesDisabled;
    private string? _criticalDiagnostic;

    public MainForm()
    {
        InitializeComponent();

        _logger = new AppLogger();
        _identity = FveApiIdentityService.Inspect();
        _fveApi = _identity.RequiredExportsAvailable
            ? new FveNativeApi(_identity)
            : new UnavailableFveApi(_identity);
        var wmiReader = new WmiBitLockerReader();
        var snapshotService = new MetadataSnapshotService(new PhysicalDiskReader());
        _discoveryService = new VolumeDiscoveryService(
            wmiReader,
            new DiskExtentReader(),
            snapshotService,
            _fveApi,
            _logger);
        _changeCoordinator = new DescriptionChangeCoordinator(
            _fveApi,
            snapshotService,
            new BackupService(),
            wmiReader,
            _logger);

        apiValueLabel.Text = $"fveapi.dll {_identity.FileVersion} - {_identity.CompatibilityLevel}";
        toolTip.SetToolTip(apiValueLabel, $"{_identity.Diagnostic}\r\nSHA-256: {_identity.Sha256}");
        Shown += async (_, _) => await RefreshVolumesAsync().ConfigureAwait(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fveApi.Dispose();
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private async void RefreshButton_Click(object? sender, EventArgs eventArgs) =>
        await RefreshVolumesAsync().ConfigureAwait(true);

    private async void ChangeButton_Click(object? sender, EventArgs eventArgs) =>
        await ChangeSelectedDescriptionAsync().ConfigureAwait(true);

    private async void RestoreButton_Click(object? sender, EventArgs eventArgs) =>
        await RestoreSelectedBackupAsync().ConfigureAwait(true);

    private void VolumeComboBox_SelectedIndexChanged(object? sender, EventArgs eventArgs)
    {
        newDescriptionTextBox.Clear();
        UpdateSelectionMetadata();
    }

    private void VolumeComboBox_Format(object? sender, ListControlConvertEventArgs eventArgs)
    {
        if (eventArgs.ListItem is BitLockerVolumeInfo volume)
        {
            eventArgs.Value = $"{volume.DisplayDrive} - {volume.DeviceId}";
        }
    }

    private void NewDescriptionTextBox_TextChanged(object? sender, EventArgs eventArgs) =>
        UpdateDescriptionValidation();

    private void OpenBackupButton_Click(object? sender, EventArgs eventArgs)
    {
        Directory.CreateDirectory(BackupService.DefaultBackupDirectory);
        Process.Start(new ProcessStartInfo(BackupService.DefaultBackupDirectory) { UseShellExecute = true });
    }

    private async Task RefreshVolumesAsync()
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true, "Reading BitLocker metadata...");
        try
        {
            var volumes = await Task.Run(_discoveryService.DiscoverVolumes).ConfigureAwait(true);
            volumeComboBox.BeginUpdate();
            try
            {
                volumeComboBox.Items.Clear();
                foreach (var volume in volumes)
                {
                    volumeComboBox.Items.Add(volume);
                }
            }
            finally
            {
                volumeComboBox.EndUpdate();
            }

            if (volumeComboBox.Items.Count > 0)
            {
                volumeComboBox.SelectedIndex = 0;
            }

            statusLabel.Text = $"Found {volumes.Count} BitLocker volume(s).";
            _logger.Information($"Read-only discovery completed with {volumes.Count} volume(s). fveapi={_identity.FileVersion}.");
        }
        catch (Exception exception) when (exception is not AccessViolationException)
        {
            statusLabel.Text = "Discovery failed.";
            _logger.Error($"Volume discovery failed: {exception.Message}");
            MessageBox.Show(this, exception.Message, "Discovery failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false, statusLabel.Text);
            UpdateSelectionMetadata();
        }
    }

    private async Task ChangeSelectedDescriptionAsync()
    {
        var volume = SelectedVolume;
        if (volume is null || _writesDisabled)
        {
            return;
        }

        var blockReason = volume.GetWriteBlockReason(_identity);
        if (blockReason is not null)
        {
            MessageBox.Show(this, blockReason, "Volume is read-only", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var newDescription = newDescriptionTextBox.Text;
        var validationError = DescriptionValidator.Validate(newDescription, volume.Description);
        if (validationError is not null)
        {
            MessageBox.Show(this, validationError, "Invalid Description", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using (var riskDialog = new RiskAcceptanceDialog(_identity.FileVersion, _identity.Sha256))
        {
            if (riskDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
        }

        var systemVolumeWarning = volume.VolumeType == 0
            ? "\n\nWARNING: This is the operating-system volume. Verify that a recovery method is stored safely before continuing."
            : string.Empty;
        var confirmation = MessageBox.Show(
            this,
            $"Volume: {volume.DisplayDrive}\n\n" +
            $"Current Description: {volume.Description}\nNew Description: {newDescription}\n\n" +
            $"A verified raw metadata backup will be created in:\n{BackupService.DefaultBackupDirectory}" +
            systemVolumeWarning +
            "\n\nContinue?",
            "Confirm BitLocker Description change",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        var refreshAfterChange = false;
        SetBusy(true, "Backing up metadata and changing the Description...");
        try
        {
            var result = await Task.Run(() => _changeCoordinator.ChangeDescription(
                volume,
                newDescription,
                BackupService.DefaultBackupDirectory)).ConfigureAwait(true);
            ShowTransactionResult(result, "Description changed", "Description change failed", "Change rolled back");
            refreshAfterChange = true;
        }
        finally
        {
            SetBusy(false, _writesDisabled ? "Writes disabled after a critical verification failure." : "Ready.");
        }

        if (refreshAfterChange)
        {
            await RefreshVolumesAsync().ConfigureAwait(true);
        }
    }

    private async Task RestoreSelectedBackupAsync()
    {
        var volume = SelectedVolume;
        if (volume is null || _writesDisabled)
        {
            return;
        }

        var blockReason = volume.GetWriteBlockReason(_identity);
        if (blockReason is not null)
        {
            MessageBox.Show(this, blockReason, "Volume is read-only", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var fileDialog = new OpenFileDialog
        {
            Title = "Select a BitLocker Description backup",
            Filter = "BitLocker Description backup (*.bdcbackup)|*.bdcbackup|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = BackupService.DefaultBackupDirectory
        };
        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        BackupManifest manifest;
        SetBusy(true, "Verifying the selected backup...");
        try
        {
            manifest = await Task.Run(() => BackupService.VerifyArchive(fileDialog.FileName)).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not AccessViolationException)
        {
            MessageBox.Show(this, exception.Message, "Backup verification failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            SetBusy(false, "Ready.");
        }

        var restoreError = BackupRestoreValidator.Validate(volume, manifest);
        if (restoreError is not null)
        {
            MessageBox.Show(this, restoreError, "Backup does not match", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using (var riskDialog = new RiskAcceptanceDialog(_identity.FileVersion, _identity.Sha256))
        {
            if (riskDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
        }

        var confirmation = MessageBox.Show(
            this,
            $"Volume: {volume.DisplayDrive}\n\n" +
            $"Current Description: {volume.Description}\nBackup Description: {manifest.OriginalDescription}\n" +
            $"Backup created (UTC): {manifest.CreatedUtc:O}\n\n" +
            "Only the Description will be restored through the private API. Raw metadata blocks will not be written.\n\n" +
            $"A fresh backup of the current state will be created first in:\n{BackupService.DefaultBackupDirectory}\n\nContinue?",
            "Confirm Description restore",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        var refreshAfterRestore = false;
        SetBusy(true, "Backing up current metadata and restoring the Description...");
        try
        {
            var result = await Task.Run(() => _changeCoordinator.RestoreDescription(
                volume,
                manifest,
                BackupService.DefaultBackupDirectory)).ConfigureAwait(true);
            ShowTransactionResult(result, "Description restored", "Description restore failed", "Restore rolled back");
            refreshAfterRestore = true;
        }
        finally
        {
            SetBusy(false, _writesDisabled ? "Writes disabled after a critical verification failure." : "Ready.");
        }

        if (refreshAfterRestore)
        {
            await RefreshVolumesAsync().ConfigureAwait(true);
        }
    }

    private void UpdateSelectionMetadata()
    {
        var volume = SelectedVolume;
        var snapshot = volume?.Snapshot;

        currentDescriptionTextBox.Text = volume?.Description ?? string.Empty;
        volumeIdTextBox.Text = snapshot?.Primary.VolumeId.ToString("D", CultureInfo.InvariantCulture) ?? "Unavailable";
        creationTimeTextBox.Text = snapshot is null
            ? "Unavailable"
            : snapshot.Primary.CreationTimeUtc.ToString("O", CultureInfo.InvariantCulture);
        metadataOffsetsTextBox.Text = snapshot is null
            ? "Unavailable"
            : string.Join(", ", snapshot.Primary.MetadataOffsets.Select(offset => $"0x{offset:X}"));
        metadataCopiesTextBox.Text = snapshot is null
            ? "Unavailable"
            : snapshot.CopiesAreStructurallyConsistent ? "3 readable, structurally consistent copies" : "Inconsistent";
        metadataVersionTextBox.Text = snapshot?.Primary.MetadataVersion.ToString(CultureInfo.InvariantCulture) ?? "Unavailable";

        UpdateDescriptionValidation();
    }

    private void UpdateDescriptionValidation()
    {
        descriptionCounterLabel.Text = $"{newDescriptionTextBox.Text.Length}/{DescriptionValidator.MaximumUtf16CodeUnits}";

        var volume = SelectedVolume;
        if (volume is null)
        {
            readinessLabel.Text = "Select a volume.";
            readinessLabel.ForeColor = SystemColors.GrayText;
            changeButton.Enabled = false;
            restoreButton.Enabled = false;
            return;
        }

        var blockReason = volume.GetWriteBlockReason(_identity);
        var inputError = DescriptionValidator.Validate(newDescriptionTextBox.Text, volume.Description);
        var message = _criticalDiagnostic ?? blockReason ?? inputError ?? "Ready to create a backup and change the Description.";
        readinessLabel.Text = message;
        readinessLabel.ForeColor = _criticalDiagnostic is not null
            ? Color.DarkRed
            : blockReason is not null || inputError is not null ? Color.DarkGoldenrod : Color.DarkGreen;
        toolTip.SetToolTip(readinessLabel, message);

        changeButton.Enabled = !_busy && !_writesDisabled && blockReason is null && inputError is null;
        restoreButton.Enabled = !_busy && !_writesDisabled && blockReason is null;
    }

    private BitLockerVolumeInfo? SelectedVolume => volumeComboBox.SelectedItem as BitLockerVolumeInfo;

    private void SetBusy(bool busy, string status)
    {
        _busy = busy;
        UseWaitCursor = busy;
        volumeComboBox.Enabled = !busy;
        refreshButton.Enabled = !busy;
        openBackupButton.Enabled = !busy;
        statusLabel.Text = status;
        UpdateDescriptionValidation();
    }

    private static string BuildResultDiagnostic(DescriptionChangeResult result)
    {
        var builder = new StringBuilder(result.Diagnostic);
        if (!string.IsNullOrWhiteSpace(result.BackupPath))
        {
            builder.AppendLine().AppendLine().Append("Backup: ").Append(result.BackupPath);
        }

        if (result.WriteResult is not null)
        {
            builder.AppendLine().AppendLine().Append("Write: ").Append(result.WriteResult.Diagnostic);
        }

        if (result.RollbackResult is not null)
        {
            builder.AppendLine().Append("Rollback: ").Append(result.RollbackResult.Diagnostic);
        }

        return builder.ToString();
    }

    private void ShowTransactionResult(
        DescriptionChangeResult result,
        string successTitle,
        string failureTitle,
        string rolledBackTitle)
    {
        var icon = result.Status switch
        {
            DescriptionChangeStatus.Succeeded => MessageBoxIcon.Information,
            DescriptionChangeStatus.RolledBack => MessageBoxIcon.Warning,
            DescriptionChangeStatus.CriticalRollbackFailure => MessageBoxIcon.Stop,
            _ => MessageBoxIcon.Error
        };
        var title = result.Status switch
        {
            DescriptionChangeStatus.Succeeded => successTitle,
            DescriptionChangeStatus.RolledBack => rolledBackTitle,
            DescriptionChangeStatus.CriticalRollbackFailure => "Critical rollback failure",
            _ => failureTitle
        };
        var diagnostic = BuildResultDiagnostic(result);
        MessageBox.Show(this, diagnostic, title, MessageBoxButtons.OK, icon);
        if (result.Status == DescriptionChangeStatus.CriticalRollbackFailure)
        {
            _writesDisabled = true;
            _criticalDiagnostic = diagnostic;
            UpdateDescriptionValidation();
        }
    }
}
