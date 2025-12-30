using System.IO;
using System.Drawing;
using Updater.Core.Models;
using Updater.Core.Services;
using Updater.Cli.Services;

namespace Updater.Cli;

public partial class MainForm : Form
{
    private readonly FormsLogger _logger;
    private readonly ArchiveExtractor _extractor;
    private readonly FileLockDetector _lockDetector;
    private readonly UpdateService _updateService;
    private readonly RollbackService _rollbackService;
    private readonly VersionLister _versionLister;

    private TabControl _tabControl = null!;
    private TextBox _logTextBox = null!;

    // Update Tab Controls
    private TextBox _updateSourceTextBox = null!;
    private TextBox _updateDestTextBox = null!;
    private TextBox _updateBackupRootTextBox = null!;
    private TextBox _updateConfigFileTextBox = null!;
    private TextBox _updateInnerFolderTextBox = null!;
    private CheckBox _updatePreserveConfigCheckBox = null!;
    private CheckBox _updateRequireConfigCheckBox = null!;
    private CheckBox _updateDryRunCheckBox = null!;
    private TextBox _updateSevenZipPathTextBox = null!;
    private Button _updateBrowseSourceButton = null!;
    private Button _updateBrowseDestButton = null!;
    private Button _updateBrowseBackupRootButton = null!;
    private Button _updateBrowseSevenZipButton = null!;
    private Button _updateButton = null!;

    // Rollback Tab Controls
    private TextBox _rollbackDestTextBox = null!;
    private TextBox _rollbackBackupRootTextBox = null!;
    private RadioButton _rollbackLastRadioButton = null!;
    private RadioButton _rollbackToVersionRadioButton = null!;
    private ComboBox _rollbackVersionComboBox = null!;
    private Button _rollbackBrowseDestButton = null!;
    private Button _rollbackBrowseBackupRootButton = null!;
    private Button _rollbackRefreshButton = null!;
    private Button _rollbackButton = null!;

    // List Versions Tab Controls
    private TextBox _listVersionsBackupRootTextBox = null!;
    private ListView _versionsListView = null!;
    private Button _listVersionsBrowseBackupRootButton = null!;
    private Button _listVersionsRefreshButton = null!;

    public MainForm()
    {
        InitializeComponent();
        
        _logTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9)
        };

        _logger = new FormsLogger(_logTextBox);
        _extractor = new ArchiveExtractor(_logger);
        _lockDetector = new FileLockDetector(_logger);
        _updateService = new UpdateService(_logger, _extractor, _lockDetector);
        _rollbackService = new RollbackService(_logger, _lockDetector);
        _versionLister = new VersionLister(_logger);

        InitializeUI();
        LoadDefaults();
        
        // Refresh lists after form is shown
        this.Load += (s, e) =>
        {
            RefreshVersionsList();
            RefreshRollbackVersions();
        };
    }

    private void InitializeComponent()
    {
        this.Text = "Updater - Installation Manager";
        this.Size = new Size(900, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(800, 600);
    }

    private void InitializeUI()
    {
        // Main layout: Split container with tabs on top and log on bottom
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 450,
            FixedPanel = FixedPanel.Panel2
        };

        // Tab Control
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        // Update Tab
        var updateTab = CreateUpdateTab();
        _tabControl.TabPages.Add(updateTab);

        // Rollback Tab
        var rollbackTab = CreateRollbackTab();
        _tabControl.TabPages.Add(rollbackTab);

        // List Versions Tab
        var listVersionsTab = CreateListVersionsTab();
        _tabControl.TabPages.Add(listVersionsTab);

        splitContainer.Panel1.Controls.Add(_tabControl);

        // Log Panel
        var logPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        var logLabel = new Label
        {
            Text = "Activity Log:",
            Dock = DockStyle.Top,
            Height = 25,
            Padding = new Padding(5, 5, 0, 0)
        };
        logPanel.Controls.Add(_logTextBox);
        logPanel.Controls.Add(logLabel);
        logPanel.Controls.SetChildIndex(logLabel, 0);

        splitContainer.Panel2.Controls.Add(logPanel);

        this.Controls.Add(splitContainer);
    }

    private TabPage CreateUpdateTab()
    {
        var tab = new TabPage("Update");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 10,
            Padding = new Padding(10)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        int row = 0;

        // Source
        panel.Controls.Add(new Label { Text = "Source:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateSourceTextBox = new TextBox { Dock = DockStyle.Fill, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        panel.Controls.Add(_updateSourceTextBox, 1, row);
        _updateBrowseSourceButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _updateBrowseSourceButton.Click += (s, e) => BrowseFileOrFolder(_updateSourceTextBox, "Select Source (ZIP, RAR, or Folder)");
        panel.Controls.Add(_updateBrowseSourceButton, 2, row++);

        // Destination
        panel.Controls.Add(new Label { Text = "Destination:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateDestTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_updateDestTextBox, 1, row);
        _updateBrowseDestButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _updateBrowseDestButton.Click += (s, e) => BrowseFolder(_updateDestTextBox, "Select Destination Folder");
        panel.Controls.Add(_updateBrowseDestButton, 2, row++);

        // Backup Root
        panel.Controls.Add(new Label { Text = "Backup Root:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateBackupRootTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_updateBackupRootTextBox, 1, row);
        _updateBrowseBackupRootButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _updateBrowseBackupRootButton.Click += (s, e) => BrowseFolder(_updateBackupRootTextBox, "Select Backup Root Folder");
        panel.Controls.Add(_updateBrowseBackupRootButton, 2, row++);

        // Config File
        panel.Controls.Add(new Label { Text = "Config File:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateConfigFileTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_updateConfigFileTextBox, 1, row);
        panel.Controls.Add(new Label(), 2, row++);

        // Inner Folder
        panel.Controls.Add(new Label { Text = "Inner Folder:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateInnerFolderTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_updateInnerFolderTextBox, 1, row);
        panel.Controls.Add(new Label(), 2, row++);

        // Preserve Config
        _updatePreserveConfigCheckBox = new CheckBox { Text = "Preserve Config File", Checked = true, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        panel.Controls.Add(_updatePreserveConfigCheckBox, 0, row);
        panel.SetColumnSpan(_updatePreserveConfigCheckBox, 3);
        row++;

        // Require Config
        _updateRequireConfigCheckBox = new CheckBox { Text = "Require Config File", Anchor = AnchorStyles.Left | AnchorStyles.Top };
        panel.Controls.Add(_updateRequireConfigCheckBox, 0, row);
        panel.SetColumnSpan(_updateRequireConfigCheckBox, 3);
        row++;

        // Dry Run
        _updateDryRunCheckBox = new CheckBox { Text = "Dry Run (Preview Only)", Anchor = AnchorStyles.Left | AnchorStyles.Top };
        panel.Controls.Add(_updateDryRunCheckBox, 0, row);
        panel.SetColumnSpan(_updateDryRunCheckBox, 3);
        row++;

        // 7-Zip Path
        panel.Controls.Add(new Label { Text = "7-Zip Path:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _updateSevenZipPathTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_updateSevenZipPathTextBox, 1, row);
        _updateBrowseSevenZipButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _updateBrowseSevenZipButton.Click += (s, e) => BrowseFile(_updateSevenZipPathTextBox, "Select 7z.exe", "7z.exe|7z.exe");
        panel.Controls.Add(_updateBrowseSevenZipButton, 2, row++);

        // Update Button
        _updateButton = new Button { Text = "Start Update", Dock = DockStyle.Fill, Height = 40 };
        _updateButton.Click += UpdateButton_Click;
        panel.Controls.Add(_updateButton, 0, row);
        panel.SetColumnSpan(_updateButton, 3);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateRollbackTab()
    {
        var tab = new TabPage("Rollback");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 6,
            Padding = new Padding(10)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        // Set row styles - make the options row taller
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Destination row
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Backup Root row
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // Rollback Options row - increased height
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Rollback Button row
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Remaining space
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0)); // Extra row

        int row = 0;

        // Destination
        panel.Controls.Add(new Label { Text = "Destination:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _rollbackDestTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_rollbackDestTextBox, 1, row);
        _rollbackBrowseDestButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _rollbackBrowseDestButton.Click += (s, e) => BrowseFolder(_rollbackDestTextBox, "Select Destination Folder");
        panel.Controls.Add(_rollbackBrowseDestButton, 2, row++);

        // Backup Root
        panel.Controls.Add(new Label { Text = "Backup Root:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _rollbackBackupRootTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_rollbackBackupRootTextBox, 1, row);
        _rollbackBrowseBackupRootButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _rollbackBrowseBackupRootButton.Click += (s, e) => BrowseFolder(_rollbackBackupRootTextBox, "Select Backup Root Folder");
        panel.Controls.Add(_rollbackBrowseBackupRootButton, 2, row++);

        // Rollback Options - Using GroupBox for better visual clarity with scrolling
        var rollbackGroupBox = new GroupBox 
        { 
            Text = "Select Rollback Option",
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 20, 10, 10),
            ForeColor = Color.FromArgb(33, 33, 33), // Dark gray text
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            BackColor = Color.FromArgb(250, 250, 250) // Light gray background
        };
        
        // Scrollable panel to contain the options
        var scrollablePanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(250, 250, 250),
            Padding = new Padding(5)
        };
        
        var rollbackOptionsPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8,0,8,0),
            BackColor = Color.FromArgb(250, 250, 250),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        rollbackOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rollbackOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rollbackOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        
        _rollbackLastRadioButton = new RadioButton 
        { 
            Text = "Rollback to Last Backup", 
            Checked = true, 
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.FromArgb(33, 33, 33), // Dark text for better readability
            BackColor = Color.FromArgb(250, 250, 250), // Light background
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Padding = new Padding(5,0,5,0),
            UseVisualStyleBackColor = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        _rollbackToVersionRadioButton = new RadioButton 
        { 
            Text = "Rollback to Specific Version:", 
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.FromArgb(33, 33, 33), // Dark text for better readability
            BackColor = Color.FromArgb(250, 250, 250), // Light background
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Padding = new Padding(5,0,5,0),
            UseVisualStyleBackColor = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        _rollbackVersionComboBox = new ComboBox 
        { 
            Dock = DockStyle.Fill, 
            DropDownStyle = ComboBoxStyle.DropDownList, 
            Height = 28,
            Margin = new Padding(25, 8, 5, 10),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(33, 33, 33),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        
        _rollbackLastRadioButton.CheckedChanged += (s, e) => _rollbackVersionComboBox.Enabled = !_rollbackLastRadioButton.Checked;
        _rollbackToVersionRadioButton.CheckedChanged += (s, e) => _rollbackVersionComboBox.Enabled = _rollbackToVersionRadioButton.Checked;
        _rollbackVersionComboBox.Enabled = false;
        
        rollbackOptionsPanel.Controls.Add(_rollbackLastRadioButton, 0, 0);
        rollbackOptionsPanel.Controls.Add(_rollbackToVersionRadioButton, 0, 1);
        rollbackOptionsPanel.Controls.Add(_rollbackVersionComboBox, 0, 2);
        
        // Add the TableLayoutPanel to the scrollable panel
        scrollablePanel.Controls.Add(rollbackOptionsPanel);
        
        // Add the scrollable panel to the GroupBox
        rollbackGroupBox.Controls.Add(scrollablePanel);
        
        panel.Controls.Add(new Label { Text = "Rollback To:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        panel.Controls.Add(rollbackGroupBox, 1, row);
        _rollbackRefreshButton = new Button { Text = "Refresh", Dock = DockStyle.Fill };
        _rollbackRefreshButton.Click += (s, e) => RefreshRollbackVersions();
        panel.Controls.Add(_rollbackRefreshButton, 2, row++);

        // Rollback Button
        _rollbackButton = new Button { Text = "Start Rollback", Dock = DockStyle.Fill, Height = 40 };
        _rollbackButton.Click += RollbackButton_Click;
        panel.Controls.Add(_rollbackButton, 0, row);
        panel.SetColumnSpan(_rollbackButton, 3);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateListVersionsTab()
    {
        var tab = new TabPage("List Versions");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(10)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        int row = 0;

        // Backup Root
        panel.Controls.Add(new Label { Text = "Backup Root:", Anchor = AnchorStyles.Left | AnchorStyles.Top, AutoSize = true }, 0, row);
        _listVersionsBackupRootTextBox = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_listVersionsBackupRootTextBox, 1, row);
        _listVersionsBrowseBackupRootButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        _listVersionsBrowseBackupRootButton.Click += (s, e) => BrowseFolder(_listVersionsBackupRootTextBox, "Select Backup Root Folder");
        panel.Controls.Add(_listVersionsBrowseBackupRootButton, 2, row++);

        // Refresh Button
        _listVersionsRefreshButton = new Button { Text = "Refresh List", Dock = DockStyle.Fill, Height = 35 };
        _listVersionsRefreshButton.Click += (s, e) => RefreshVersionsList();
        panel.Controls.Add(_listVersionsRefreshButton, 0, row);
        panel.SetColumnSpan(_listVersionsRefreshButton, 3);
        row++;

        // Versions List
        _versionsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _versionsListView.Columns.Add("Version", 400);
        _versionsListView.Columns.Add("Date", 200);
        _versionsListView.Columns.Add("Status", 100);
        panel.Controls.Add(_versionsListView, 0, row);
        panel.SetColumnSpan(_versionsListView, 3);

        tab.Controls.Add(panel);
        return tab;
    }

    private void LoadDefaults()
    {
        _updateSourceTextBox.Text = @".\net7.0-windows.rar";
        _updateDestTextBox.Text = @".\maquinasdispensadorasnuevosoftware";
        _updateBackupRootTextBox.Text = @".\secur";
        _updateConfigFileTextBox.Text = "appsettings.json";
        _updatePreserveConfigCheckBox.Checked = true;
        _updateRequireConfigCheckBox.Checked = false;
        _updateDryRunCheckBox.Checked = false;

        _rollbackDestTextBox.Text = @".\maquinasdispensadorasnuevosoftware";
        _rollbackBackupRootTextBox.Text = @".\secur";

        _listVersionsBackupRootTextBox.Text = @".\secur";
    }

    private void BrowseFileOrFolder(TextBox textBox, string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "All Supported|*.zip;*.rar|ZIP Files|*.zip|RAR Files|*.rar|All Files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBox.Text = dialog.FileName;
        }
    }

    private void BrowseFolder(TextBox textBox, string description)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrEmpty(textBox.Text) && Directory.Exists(textBox.Text))
        {
            dialog.SelectedPath = Path.GetFullPath(textBox.Text);
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseFile(TextBox textBox, string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBox.Text = dialog.FileName;
        }
    }

    private void RefreshRollbackVersions()
    {
        _rollbackVersionComboBox.Items.Clear();
        
        if (!Directory.Exists(_rollbackBackupRootTextBox.Text))
        {
            return;
        }

        var backups = Directory.GetDirectories(_rollbackBackupRootTextBox.Text)
            .Select(d => new
            {
                Path = d,
                Name = Path.GetFileName(d),
                LastWriteTime = Directory.GetLastWriteTime(d)
            })
            .OrderByDescending(b => b.LastWriteTime)
            .ToList();

        foreach (var backup in backups)
        {
            _rollbackVersionComboBox.Items.Add(backup.Name);
        }
    }

    private void RefreshVersionsList()
    {
        _versionsListView.Items.Clear();

        if (!Directory.Exists(_listVersionsBackupRootTextBox.Text))
        {
            _logger.LogWarn($"Backup root not found: {_listVersionsBackupRootTextBox.Text}");
            return;
        }

        var backups = Directory.GetDirectories(_listVersionsBackupRootTextBox.Text)
            .Select(d => new
            {
                Path = d,
                Name = Path.GetFileName(d),
                LastWriteTime = Directory.GetLastWriteTime(d)
            })
            .OrderByDescending(b => b.LastWriteTime)
            .ToList();

        if (backups.Count == 0)
        {
            _logger.LogInfo("No backups found.");
            return;
        }

        var lastBackup = backups.First();
        foreach (var backup in backups)
        {
            var status = backup.Path == lastBackup.Path ? "(latest)" : "";
            var item = new ListViewItem(backup.Name);
            item.SubItems.Add(backup.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            item.SubItems.Add(status);
            _versionsListView.Items.Add(item);
        }

        _logger.LogInfo($"Found {backups.Count} backup(s). Latest: {lastBackup.Name}");
    }

    private async void UpdateButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _updateButton.Enabled = false;
            
            // Validate inputs before proceeding
            if (string.IsNullOrWhiteSpace(_updateSourceTextBox.Text))
            {
                MessageBox.Show("Please specify a source file or folder.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _updateButton.Enabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(_updateDestTextBox.Text))
            {
                MessageBox.Show("Please specify a destination folder.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _updateButton.Enabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(_updateBackupRootTextBox.Text))
            {
                MessageBox.Show("Please specify a backup root folder.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _updateButton.Enabled = true;
                return;
            }

            // Check if source exists
            var sourcePath = Path.GetFullPath(_updateSourceTextBox.Text);
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                MessageBox.Show($"Source file or folder does not exist:\n{sourcePath}", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _updateButton.Enabled = true;
                return;
            }

            _logger.LogInfo("Starting update process...");

            var options = new UpdateOptions
            {
                Source = sourcePath,
                Destination = Path.GetFullPath(_updateDestTextBox.Text),
                BackupRoot = Path.GetFullPath(_updateBackupRootTextBox.Text),
                ConfigFile = _updateConfigFileTextBox.Text,
                InnerFolder = string.IsNullOrWhiteSpace(_updateInnerFolderTextBox.Text) ? null : _updateInnerFolderTextBox.Text,
                PreserveConfig = _updatePreserveConfigCheckBox.Checked,
                RequireConfig = _updateRequireConfigCheckBox.Checked,
                DryRun = _updateDryRunCheckBox.Checked,
                SevenZipPath = string.IsNullOrWhiteSpace(_updateSevenZipPathTextBox.Text) ? null : _updateSevenZipPathTextBox.Text
            };

            await Task.Run(() => _updateService.Update(options));

            MessageBox.Show("Update completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshVersionsList();
            RefreshRollbackVersions();
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (IOException ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}\n\nMake sure no files are locked or in use.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Update failed: {ex.Message}");
            MessageBox.Show($"Update failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _updateButton.Enabled = true;
        }
    }

    private async void RollbackButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _rollbackButton.Enabled = false;
            
            // Validate inputs before proceeding
            if (string.IsNullOrWhiteSpace(_rollbackDestTextBox.Text))
            {
                MessageBox.Show("Please specify a destination folder.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _rollbackButton.Enabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(_rollbackBackupRootTextBox.Text))
            {
                MessageBox.Show("Please specify a backup root folder.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _rollbackButton.Enabled = true;
                return;
            }

            // Check if backup root exists
            var backupRoot = Path.GetFullPath(_rollbackBackupRootTextBox.Text);
            if (!Directory.Exists(backupRoot))
            {
                var result = MessageBox.Show(
                    $"Backup root directory does not exist:\n{backupRoot}\n\n" +
                    "No backups are available. You need to perform at least one update before you can rollback.\n\n" +
                    "Would you like to create the backup directory?",
                    "Backup Directory Not Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        Directory.CreateDirectory(backupRoot);
                        _logger.LogInfo($"Created backup directory: {backupRoot}");
                        MessageBox.Show("Backup directory created. However, there are no backups available yet. Please perform an update first.", 
                            "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to create backup directory: {ex.Message}");
                        MessageBox.Show($"Failed to create backup directory:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                _rollbackButton.Enabled = true;
                return;
            }

            // Check if there are any backups
            var backups = Directory.GetDirectories(backupRoot);
            if (backups.Length == 0)
            {
                MessageBox.Show(
                    $"No backups found in:\n{backupRoot}\n\n" +
                    "You need to perform at least one update before you can rollback.",
                    "No Backups Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _rollbackButton.Enabled = true;
                return;
            }

            var options = new RollbackOptions
            {
                Destination = Path.GetFullPath(_rollbackDestTextBox.Text),
                BackupRoot = backupRoot,
                UseLast = _rollbackLastRadioButton.Checked,
                ToVersion = _rollbackToVersionRadioButton.Checked && _rollbackVersionComboBox.SelectedItem != null
                    ? _rollbackVersionComboBox.SelectedItem.ToString()
                    : null
            };

            if (_rollbackToVersionRadioButton.Checked && string.IsNullOrEmpty(options.ToVersion))
            {
                MessageBox.Show("Please select a version to rollback to.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _rollbackButton.Enabled = true;
                return;
            }

            _logger.LogInfo("Starting rollback process...");

            await Task.Run(() => _rollbackService.Rollback(options));

            MessageBox.Show("Rollback completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshVersionsList();
            RefreshRollbackVersions();
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (IOException ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}\n\nMake sure no files are locked or in use.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Rollback failed: {ex.Message}");
            MessageBox.Show($"Rollback failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _rollbackButton.Enabled = true;
        }
    }
}

