using BitLockerDescriptionChanger.Services;

#nullable enable

namespace BitLockerDescriptionChanger.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer? components;
    private TableLayoutPanel mainLayout = null!;
    private TableLayoutPanel volumeLayout = null!;
    private Label volumeLabel = null!;
    private ComboBox volumeComboBox = null!;
    private Button refreshButton = null!;
    private Button restoreButton = null!;
    private Button openBackupButton = null!;
    private GroupBox metadataGroupBox = null!;
    private TableLayoutPanel metadataLayout = null!;
    private Label currentDescriptionLabel = null!;
    private TextBox currentDescriptionTextBox = null!;
    private Label volumeIdLabel = null!;
    private TextBox volumeIdTextBox = null!;
    private Label creationTimeLabel = null!;
    private TextBox creationTimeTextBox = null!;
    private Label metadataOffsetsLabel = null!;
    private TextBox metadataOffsetsTextBox = null!;
    private Label metadataCopiesLabel = null!;
    private TextBox metadataCopiesTextBox = null!;
    private Label metadataVersionLabel = null!;
    private TextBox metadataVersionTextBox = null!;
    private GroupBox editGroupBox = null!;
    private TableLayoutPanel editLayout = null!;
    private Label newDescriptionLabel = null!;
    private TextBox newDescriptionTextBox = null!;
    private Label descriptionCounterLabel = null!;
    private Button changeButton = null!;
    private Label readinessLabel = null!;
    private TableLayoutPanel statusLayout = null!;
    private Label statusLabel = null!;
    private Label apiValueLabel = null!;
    private Label experimentalWarningLabel = null!;
    private ToolTip toolTip = null!;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        mainLayout = new TableLayoutPanel();
        volumeLayout = new TableLayoutPanel();
        volumeLabel = new Label();
        volumeComboBox = new ComboBox();
        refreshButton = new Button();
        restoreButton = new Button();
        openBackupButton = new Button();
        metadataGroupBox = new GroupBox();
        metadataLayout = new TableLayoutPanel();
        currentDescriptionLabel = new Label();
        currentDescriptionTextBox = new TextBox();
        volumeIdLabel = new Label();
        volumeIdTextBox = new TextBox();
        creationTimeLabel = new Label();
        creationTimeTextBox = new TextBox();
        metadataOffsetsLabel = new Label();
        metadataOffsetsTextBox = new TextBox();
        metadataCopiesLabel = new Label();
        metadataCopiesTextBox = new TextBox();
        metadataVersionLabel = new Label();
        metadataVersionTextBox = new TextBox();
        editGroupBox = new GroupBox();
        editLayout = new TableLayoutPanel();
        newDescriptionLabel = new Label();
        newDescriptionTextBox = new TextBox();
        descriptionCounterLabel = new Label();
        changeButton = new Button();
        readinessLabel = new Label();
        statusLayout = new TableLayoutPanel();
        statusLabel = new Label();
        apiValueLabel = new Label();
        experimentalWarningLabel = new Label();
        toolTip = new ToolTip(components);
        mainLayout.SuspendLayout();
        volumeLayout.SuspendLayout();
        metadataGroupBox.SuspendLayout();
        metadataLayout.SuspendLayout();
        editGroupBox.SuspendLayout();
        editLayout.SuspendLayout();
        statusLayout.SuspendLayout();
        SuspendLayout();

        mainLayout.ColumnCount = 1;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(volumeLayout, 0, 0);
        mainLayout.Controls.Add(metadataGroupBox, 0, 1);
        mainLayout.Controls.Add(editGroupBox, 0, 2);
        mainLayout.Controls.Add(statusLayout, 0, 3);
        mainLayout.Controls.Add(experimentalWarningLabel, 0, 4);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Padding = new Padding(12);
        mainLayout.RowCount = 6;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 255F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        volumeLayout.AutoSize = true;
        volumeLayout.ColumnCount = 5;
        volumeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 202F));
        volumeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        volumeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        volumeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        volumeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        volumeLayout.Controls.Add(volumeLabel, 0, 0);
        volumeLayout.Controls.Add(volumeComboBox, 1, 0);
        volumeLayout.Controls.Add(refreshButton, 2, 0);
        volumeLayout.Controls.Add(restoreButton, 3, 0);
        volumeLayout.Controls.Add(openBackupButton, 4, 0);
        volumeLayout.Dock = DockStyle.Fill;
        volumeLayout.Margin = new Padding(0, 0, 0, 8);
        volumeLayout.MinimumSize = new Size(0, 32);
        volumeLayout.RowCount = 1;
        volumeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        volumeLabel.Anchor = AnchorStyles.Left;
        volumeLabel.AutoSize = true;
        volumeLabel.Margin = new Padding(0);
        volumeLabel.Text = "Volume";

        volumeComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        volumeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        volumeComboBox.FormattingEnabled = true;
        volumeComboBox.Margin = new Padding(0, 0, 8, 0);
        volumeComboBox.SelectedIndexChanged += VolumeComboBox_SelectedIndexChanged;
        volumeComboBox.Format += VolumeComboBox_Format;

        refreshButton.Anchor = AnchorStyles.None;
        refreshButton.AutoSize = true;
        refreshButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        refreshButton.Margin = new Padding(0, 0, 8, 0);
        refreshButton.MinimumSize = new Size(0, 28);
        refreshButton.Padding = new Padding(12, 0, 12, 0);
        refreshButton.Text = "Refresh";
        refreshButton.UseVisualStyleBackColor = true;
        refreshButton.Click += RefreshButton_Click;

        restoreButton.Anchor = AnchorStyles.None;
        restoreButton.AutoSize = true;
        restoreButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        restoreButton.Enabled = false;
        restoreButton.Margin = new Padding(0, 0, 8, 0);
        restoreButton.MinimumSize = new Size(0, 28);
        restoreButton.Padding = new Padding(12, 0, 12, 0);
        restoreButton.Text = "Restore Backup...";
        restoreButton.UseVisualStyleBackColor = true;
        restoreButton.Click += RestoreButton_Click;

        openBackupButton.Anchor = AnchorStyles.None;
        openBackupButton.AutoSize = true;
        openBackupButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        openBackupButton.Margin = new Padding(0, 0, 8, 0);
        openBackupButton.MinimumSize = new Size(0, 28);
        openBackupButton.Padding = new Padding(12, 0, 12, 0);
        openBackupButton.Text = "Open Backups";
        openBackupButton.UseVisualStyleBackColor = true;
        openBackupButton.Click += OpenBackupButton_Click;

        metadataGroupBox.Controls.Add(metadataLayout);
        metadataGroupBox.Dock = DockStyle.Fill;
        metadataGroupBox.Margin = new Padding(0, 0, 0, 8);
        metadataGroupBox.MinimumSize = new Size(0, 247);
        metadataGroupBox.Padding = new Padding(12, 8, 12, 10);
        metadataGroupBox.Text = "FVE metadata not shown by the standard Windows interface";

        metadataLayout.ColumnCount = 2;
        metadataLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        metadataLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        metadataLayout.Controls.Add(currentDescriptionLabel, 0, 0);
        metadataLayout.Controls.Add(currentDescriptionTextBox, 1, 0);
        metadataLayout.Controls.Add(volumeIdLabel, 0, 1);
        metadataLayout.Controls.Add(volumeIdTextBox, 1, 1);
        metadataLayout.Controls.Add(creationTimeLabel, 0, 2);
        metadataLayout.Controls.Add(creationTimeTextBox, 1, 2);
        metadataLayout.Controls.Add(metadataOffsetsLabel, 0, 3);
        metadataLayout.Controls.Add(metadataOffsetsTextBox, 1, 3);
        metadataLayout.Controls.Add(metadataCopiesLabel, 0, 4);
        metadataLayout.Controls.Add(metadataCopiesTextBox, 1, 4);
        metadataLayout.Controls.Add(metadataVersionLabel, 0, 5);
        metadataLayout.Controls.Add(metadataVersionTextBox, 1, 5);
        metadataLayout.Dock = DockStyle.Fill;
        metadataLayout.Margin = new Padding(0);
        metadataLayout.RowCount = 6;
        metadataLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        metadataLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        metadataLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        metadataLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        metadataLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        metadataLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        currentDescriptionLabel.Anchor = AnchorStyles.Left;
        currentDescriptionLabel.AutoSize = true;
        currentDescriptionLabel.Margin = new Padding(0, 3, 3, 3);
        currentDescriptionLabel.Text = "BitLocker Description";

        currentDescriptionTextBox.BackColor = SystemColors.Window;
        currentDescriptionTextBox.BorderStyle = BorderStyle.FixedSingle;
        currentDescriptionTextBox.Dock = DockStyle.Fill;
        currentDescriptionTextBox.Margin = new Padding(0, 5, 0, 5);
        currentDescriptionTextBox.ReadOnly = true;
        currentDescriptionTextBox.TabStop = false;

        volumeIdLabel.Anchor = AnchorStyles.Left;
        volumeIdLabel.AutoSize = true;
        volumeIdLabel.Margin = new Padding(0, 3, 3, 3);
        volumeIdLabel.Text = "FVE volume UUID";

        volumeIdTextBox.BackColor = SystemColors.Window;
        volumeIdTextBox.BorderStyle = BorderStyle.FixedSingle;
        volumeIdTextBox.Dock = DockStyle.Fill;
        volumeIdTextBox.Margin = new Padding(0, 5, 0, 5);
        volumeIdTextBox.ReadOnly = true;
        volumeIdTextBox.TabStop = false;

        creationTimeLabel.Anchor = AnchorStyles.Left;
        creationTimeLabel.AutoSize = true;
        creationTimeLabel.Margin = new Padding(0, 3, 3, 3);
        creationTimeLabel.Text = "FVE creation time (UTC)";

        creationTimeTextBox.BackColor = SystemColors.Window;
        creationTimeTextBox.BorderStyle = BorderStyle.FixedSingle;
        creationTimeTextBox.Dock = DockStyle.Fill;
        creationTimeTextBox.Margin = new Padding(0, 5, 0, 5);
        creationTimeTextBox.ReadOnly = true;
        creationTimeTextBox.TabStop = false;

        metadataOffsetsLabel.Anchor = AnchorStyles.Left;
        metadataOffsetsLabel.AutoSize = true;
        metadataOffsetsLabel.Margin = new Padding(0, 3, 3, 3);
        metadataOffsetsLabel.Text = "Metadata block offsets";

        metadataOffsetsTextBox.BackColor = SystemColors.Window;
        metadataOffsetsTextBox.BorderStyle = BorderStyle.FixedSingle;
        metadataOffsetsTextBox.Dock = DockStyle.Fill;
        metadataOffsetsTextBox.Margin = new Padding(0, 5, 0, 5);
        metadataOffsetsTextBox.ReadOnly = true;
        metadataOffsetsTextBox.TabStop = false;

        metadataCopiesLabel.Anchor = AnchorStyles.Left;
        metadataCopiesLabel.AutoSize = true;
        metadataCopiesLabel.Margin = new Padding(0, 3, 3, 3);
        metadataCopiesLabel.Text = "Metadata copies";

        metadataCopiesTextBox.BackColor = SystemColors.Window;
        metadataCopiesTextBox.BorderStyle = BorderStyle.FixedSingle;
        metadataCopiesTextBox.Dock = DockStyle.Fill;
        metadataCopiesTextBox.Margin = new Padding(0, 5, 0, 5);
        metadataCopiesTextBox.ReadOnly = true;
        metadataCopiesTextBox.TabStop = false;

        metadataVersionLabel.Anchor = AnchorStyles.Left;
        metadataVersionLabel.AutoSize = true;
        metadataVersionLabel.Margin = new Padding(0, 3, 3, 3);
        metadataVersionLabel.Text = "Metadata version";

        metadataVersionTextBox.BackColor = SystemColors.Window;
        metadataVersionTextBox.BorderStyle = BorderStyle.FixedSingle;
        metadataVersionTextBox.Dock = DockStyle.Fill;
        metadataVersionTextBox.Margin = new Padding(0, 5, 0, 5);
        metadataVersionTextBox.ReadOnly = true;
        metadataVersionTextBox.TabStop = false;

        editGroupBox.AutoSize = false;
        editGroupBox.Controls.Add(editLayout);
        editGroupBox.Dock = DockStyle.Fill;
        editGroupBox.Margin = new Padding(0, 0, 0, 8);
        editGroupBox.Padding = new Padding(12, 8, 12, 10);
        editGroupBox.Text = "Change Description";

        editLayout.AutoSize = false;
        editLayout.ColumnCount = 3;
        editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editLayout.Controls.Add(newDescriptionLabel, 0, 0);
        editLayout.Controls.Add(newDescriptionTextBox, 1, 0);
        editLayout.Controls.Add(changeButton, 2, 0);
        editLayout.Controls.Add(readinessLabel, 0, 1);
        editLayout.Controls.Add(descriptionCounterLabel, 2, 1);
        editLayout.Dock = DockStyle.Fill;
        editLayout.Margin = new Padding(0);
        editLayout.RowCount = 2;
        editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        editLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        editLayout.SetColumnSpan(readinessLabel, 2);

        newDescriptionLabel.Anchor = AnchorStyles.Left;
        newDescriptionLabel.AutoSize = true;
        newDescriptionLabel.Margin = new Padding(0, 3, 3, 3);
        newDescriptionLabel.Text = "New Description";

        newDescriptionTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        newDescriptionTextBox.MaxLength = DescriptionValidator.MaximumUtf16CodeUnits;
        newDescriptionTextBox.Margin = new Padding(0, 3, 8, 3);
        newDescriptionTextBox.TextChanged += NewDescriptionTextBox_TextChanged;

        descriptionCounterLabel.Anchor = AnchorStyles.Right;
        descriptionCounterLabel.AutoSize = true;
        descriptionCounterLabel.Margin = new Padding(0, 8, 8, 0);
        descriptionCounterLabel.Text = "0/127";
        descriptionCounterLabel.TextAlign = ContentAlignment.MiddleRight;

        changeButton.Anchor = AnchorStyles.None;
        changeButton.AutoSize = true;
        changeButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        changeButton.Enabled = false;
        changeButton.Margin = new Padding(0, 0, 8, 0);
        changeButton.MinimumSize = new Size(0, 28);
        changeButton.Padding = new Padding(12, 0, 12, 0);
        changeButton.Text = "Change";
        changeButton.UseVisualStyleBackColor = true;
        changeButton.Click += ChangeButton_Click;

        readinessLabel.AutoEllipsis = true;
        readinessLabel.Dock = DockStyle.Fill;
        readinessLabel.ForeColor = SystemColors.GrayText;
        readinessLabel.Margin = new Padding(0, 8, 0, 0);
        readinessLabel.Text = "Select a volume.";

        statusLayout.AutoSize = true;
        statusLayout.ColumnCount = 2;
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        statusLayout.Controls.Add(statusLabel, 0, 0);
        statusLayout.Controls.Add(apiValueLabel, 1, 0);
        statusLayout.Dock = DockStyle.Fill;
        statusLayout.Margin = new Padding(0, 0, 0, 4);

        statusLabel.Anchor = AnchorStyles.Left;
        statusLabel.AutoSize = true;
        statusLabel.Text = "Ready.";

        apiValueLabel.Anchor = AnchorStyles.Right;
        apiValueLabel.AutoSize = true;
        apiValueLabel.ForeColor = SystemColors.GrayText;
        apiValueLabel.Text = "fveapi.dll";

        experimentalWarningLabel.Dock = DockStyle.Fill;
        experimentalWarningLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        experimentalWarningLabel.ForeColor = Color.DarkRed;
        experimentalWarningLabel.Margin = new Padding(0);
        experimentalWarningLabel.Text = "EXPERIMENTAL FEATURE: Uses undocumented BitLocker private APIs. No compatibility or data-safety guarantee is provided. You accept all consequences of use.";
        experimentalWarningLabel.TextAlign = ContentAlignment.MiddleLeft;

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = false;
        ClientSize = new Size(900, 510);
        Controls.Add(mainLayout);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "MainForm";
        SizeGripStyle = SizeGripStyle.Hide;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "BitLocker Description Changer";
        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        volumeLayout.ResumeLayout(false);
        volumeLayout.PerformLayout();
        metadataGroupBox.ResumeLayout(false);
        metadataLayout.ResumeLayout(false);
        metadataLayout.PerformLayout();
        editGroupBox.ResumeLayout(false);
        editGroupBox.PerformLayout();
        editLayout.ResumeLayout(false);
        editLayout.PerformLayout();
        statusLayout.ResumeLayout(false);
        statusLayout.PerformLayout();
        ResumeLayout(false);
    }

}
