using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Navlight.Installer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var installerContext = InstallerContext.Create();
        var defaultInstallRoot = ResolveDefaultInstallRoot(args);
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm(defaultInstallRoot, installerContext.BundleRoot));
    }

    private static string ResolveDefaultInstallRoot(string[] args)
    {
        var argumentOverride = ParseDefaultInstallRootArgument(args);
        if (!string.IsNullOrWhiteSpace(argumentOverride))
        {
            return Path.Combine(Path.GetFullPath(argumentOverride), "NavlightRegistration");
        }

        var executablePath = Environment.ProcessPath;
        var executableDirectory = !string.IsNullOrWhiteSpace(executablePath)
            ? Path.GetDirectoryName(executablePath)
            : AppContext.BaseDirectory;

        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            executableDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(Path.GetFullPath(executableDirectory), "NavlightRegistration");
    }

    private static string? ParseDefaultInstallRootArgument(string[] args)
    {
        const string prefix = "--default-install-root=";
        foreach (var arg in args)
        {
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg[prefix.Length..].Trim('"');
            }
        }

        return null;
    }
}

internal sealed class InstallerContext : IDisposable
{
    public required string BundleRoot { get; init; }
    public string? TemporaryBundleRoot { get; init; }

    public static InstallerContext Create()
    {
        var embeddedBundlePath = ExtractEmbeddedBundleIfPresent();
        var baseDirectory = AppContext.BaseDirectory;
        var bundleRoot = !string.IsNullOrWhiteSpace(embeddedBundlePath) ? embeddedBundlePath : baseDirectory;

        return new InstallerContext
        {
            BundleRoot = bundleRoot,
            TemporaryBundleRoot = embeddedBundlePath
        };
    }

    private static string? ExtractEmbeddedBundleIfPresent()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("NavlightBundle.zip");
        if (stream is null)
        {
            return null;
        }

        var extractRoot = Path.Combine(Path.GetTempPath(), "navlight-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractRoot);

        var zipPath = Path.Combine(extractRoot, "NavlightBundle.zip");
        using (var file = File.Create(zipPath))
        {
            stream.CopyTo(file);
        }

        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractRoot);

        using var mySqlStream = typeof(Program).Assembly.GetManifestResourceStream("MySqlPackage.zip");
        if (mySqlStream is not null)
        {
            var mySqlZipPath = Path.Combine(extractRoot, "MySqlPackage.zip");
            using var mySqlFile = File.Create(mySqlZipPath);
            mySqlStream.CopyTo(mySqlFile);
        }

        return extractRoot;
    }

    public void Dispose()
    {
        if (!string.IsNullOrWhiteSpace(TemporaryBundleRoot) && Directory.Exists(TemporaryBundleRoot))
        {
            try
            {
                Directory.Delete(TemporaryBundleRoot, true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}

internal enum InstallMode
{
    ClientOnly,
    HostAndClient,
    SingleUser
}

internal sealed class InstallOptions
{
    public InstallMode Mode { get; init; }
    public required string InstallRoot { get; init; }
    public required string DatabaseServer { get; init; }
    public int DatabasePort { get; init; } = 3306;
    public required string DatabaseName { get; init; }
    public required string DatabaseUser { get; init; }
    public required string DatabasePassword { get; init; }
    public bool DhcpReservationConfirmed { get; init; }
}

internal sealed class InstallerForm : Form
{
    private readonly ComboBox _modeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _installRootText = new();
    private readonly Button _browseInstallRootButton = new() { Text = "Browse..." };
    private readonly TextBox _databaseServerText = new() { Text = "navlighthost" };
    private readonly NumericUpDown _databasePortUpDown = new() { Minimum = 1, Maximum = 65535, Value = 3306 };
    private readonly TextBox _databaseNameText = new() { Text = "navlight-registration" };
    private readonly TextBox _databaseUserText = new() { Text = "root" };
    private readonly TextBox _databasePasswordText = new() { Text = "root", UseSystemPasswordChar = true };
    private readonly GroupBox _hostGroup = new() { Text = "Host install confirmation" };
    private readonly CheckBox _dhcpReservationCheck = new() { Text = "I have already created the router DHCP reservation for this host", AutoSize = true };
    private readonly Label _statusLabel = new() { AutoSize = false };
    private readonly Button _installButton = new() { Text = "Install" };
    private readonly Button _cancelButton = new() { Text = "Cancel" };

    public InstallerForm(string defaultInstallRoot, string bundleRoot)
    {
        Text = "Navlight Installer";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _modeCombo.Items.AddRange([InstallMode.ClientOnly, InstallMode.HostAndClient, InstallMode.SingleUser]);
        _modeCombo.SelectedItem = InstallMode.SingleUser;
        _modeCombo.SelectedIndexChanged += (_, _) => UpdateUiForMode();

        _installRootText.Text = defaultInstallRoot;
        _browseInstallRootButton.Click += OnBrowseInstallRoot;

        _installButton.Click += (_, _) => OnInstall(bundleRoot);
        _cancelButton.Click += (_, _) => Close();

        BuildLayout();
        UpdateUiForMode();
    }

    private void BuildLayout()
    {
        var y = 16;
        Controls.Add(MakeLabel("Install mode", 16, y));
        _modeCombo.SetBounds(180, y - 3, 220, 24);
        Controls.Add(_modeCombo);

        y += 38;
        Controls.Add(MakeLabel("Install folder", 16, y));
        _installRootText.SetBounds(180, y - 3, 440, 24);
        Controls.Add(_installRootText);
        _browseInstallRootButton.SetBounds(628, y - 4, 72, 26);
        Controls.Add(_browseInstallRootButton);

        y += 38;
        Controls.Add(MakeLabel("Database host", 16, y));
        _databaseServerText.SetBounds(180, y - 3, 220, 24);
        Controls.Add(_databaseServerText);

        y += 38;
        Controls.Add(MakeLabel("Database port", 16, y));
        _databasePortUpDown.SetBounds(180, y - 3, 120, 24);
        Controls.Add(_databasePortUpDown);

        y += 38;
        Controls.Add(MakeLabel("Database name", 16, y));
        _databaseNameText.SetBounds(180, y - 3, 220, 24);
        Controls.Add(_databaseNameText);

        y += 38;
        Controls.Add(MakeLabel("Database user", 16, y));
        _databaseUserText.SetBounds(180, y - 3, 220, 24);
        Controls.Add(_databaseUserText);

        y += 38;
        Controls.Add(MakeLabel("Database password", 16, y));
        _databasePasswordText.SetBounds(180, y - 3, 220, 24);
        Controls.Add(_databasePasswordText);

        y += 44;
        _hostGroup.SetBounds(16, y, 684, 62);
        _dhcpReservationCheck.Location = new Point(12, 26);
        _hostGroup.Controls.Add(_dhcpReservationCheck);
        Controls.Add(_hostGroup);

        y += 74;

        _statusLabel.SetBounds(16, 516, 500, 20);
        Controls.Add(_statusLabel);

        _installButton.SetBounds(538, 512, 78, 30);
        _cancelButton.SetBounds(622, 512, 78, 30);
        Controls.Add(_installButton);
        Controls.Add(_cancelButton);
    }

    private static Label MakeLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y)
        };
    }

    private void UpdateUiForMode()
    {
        var mode = GetSelectedMode();
        var isClientOnly = mode == InstallMode.ClientOnly;
        var isHostAndClient = mode == InstallMode.HostAndClient;
        var isSingleUser = mode == InstallMode.SingleUser;

        _hostGroup.Visible = isHostAndClient;

        if (isSingleUser)
        {
            _databaseServerText.Text = "localhost";
            _databaseServerText.Enabled = false;
        }
        else
        {
            if (!_databaseServerText.Enabled && string.Equals(_databaseServerText.Text, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                _databaseServerText.Text = "navlighthost";
            }

            _databaseServerText.Enabled = true;
        }
    }

    private InstallMode GetSelectedMode()
    {
        return _modeCombo.SelectedItem is InstallMode mode ? mode : InstallMode.ClientOnly;
    }

    private void OnBrowseInstallRoot(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.SelectedPath = _installRootText.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _installRootText.Text = dialog.SelectedPath;
        }
    }

    private async void OnInstall(string bundleRoot)
    {
        try
        {
            var options = BuildOptionsOrThrow();
            var progress = new Progress<string>(message => _statusLabel.Text = message);

            if (InstallerEngine.HasExistingInstallation(options.InstallRoot))
            {
                var confirmResult = MessageBox.Show(
                    this,
                    $"An existing Navlight installation was found in '{options.InstallRoot}'." + Environment.NewLine + Environment.NewLine +
                    "This will stop the existing database (if running) and delete the existing application and database files in that folder. Uninstall now?" + Environment.NewLine + Environment.NewLine +
                    "Run the installer again afterward to install fresh.",
                    "Existing Installation Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (confirmResult != DialogResult.Yes)
                {
                    return;
                }

                SetBusy(true, "Uninstalling existing installation...");
                await Task.Run(() => InstallerEngine.Uninstall(options.InstallRoot, options.DatabasePort, progress));

                MessageBox.Show(this, "The existing Navlight installation was removed. Run the installer again to install fresh.", "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }

            SetBusy(true, "Installing, please wait...");
            await Task.Run(() => InstallerEngine.Run(bundleRoot, options, progress));

            MessageBox.Show(this, "Navlight installation completed successfully.", "Install Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            SetBusy(false, "Install failed.");
            MessageBox.Show(this, ex.Message, "Install Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false, string.Empty);
            }
        }
    }

    private InstallOptions BuildOptionsOrThrow()
    {
        var mode = GetSelectedMode();
        var installRoot = _installRootText.Text.Trim();
        var databaseServer = _databaseServerText.Text.Trim();
        var databaseName = _databaseNameText.Text.Trim();
        var databaseUser = _databaseUserText.Text.Trim();
        var databasePassword = _databasePasswordText.Text;

        if (string.IsNullOrWhiteSpace(installRoot))
        {
            throw new InvalidOperationException("Please choose an install folder.");
        }

        if (mode != InstallMode.SingleUser && string.IsNullOrWhiteSpace(databaseServer))
        {
            throw new InvalidOperationException("Please enter the database host name.");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Please enter the database name.");
        }

        if (string.IsNullOrWhiteSpace(databaseUser))
        {
            throw new InvalidOperationException("Please enter the database user.");
        }

        if (mode == InstallMode.HostAndClient && !_dhcpReservationCheck.Checked)
        {
            throw new InvalidOperationException("Host installs require confirmation that the router DHCP reservation has already been created.");
        }


        return new InstallOptions
        {
            Mode = mode,
            InstallRoot = installRoot,
            DatabaseServer = databaseServer,
            DatabasePort = Decimal.ToInt32(_databasePortUpDown.Value),
            DatabaseName = databaseName,
            DatabaseUser = databaseUser,
            DatabasePassword = databasePassword,
            DhcpReservationConfirmed = _dhcpReservationCheck.Checked
        };
    }

    private void SetBusy(bool busy, string status)
    {
        _installButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        UseWaitCursor = busy;
        _statusLabel.Text = status;
    }
}

internal static class InstallerEngine
{
    public static bool HasExistingInstallation(string installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(Path.GetFullPath(installRoot)))
        {
            return false;
        }

        var fullInstallRoot = Path.GetFullPath(installRoot);
        var targetAppDir = Path.Combine(fullInstallRoot, "Navlight.Registration.App");
        var targetDatabaseDir = Path.Combine(fullInstallRoot, "Database");
        return Directory.Exists(targetAppDir) || Directory.Exists(targetDatabaseDir);
    }

    public static void Uninstall(string installRoot, int port, IProgress<string>? progress)
    {
        var fullInstallRoot = Path.GetFullPath(installRoot);
        var targetAppDir = Path.Combine(fullInstallRoot, "Navlight.Registration.App");
        var targetDatabaseDir = Path.Combine(fullInstallRoot, "Database");

        progress?.Report("Stopping the existing Navlight database...");
        StopMySqlProcessesUnderRoot(targetDatabaseDir);
        ClearPortIfInUse(port, progress);

        progress?.Report("Removing the existing installation...");
        if (Directory.Exists(targetAppDir))
        {
            Directory.Delete(targetAppDir, true);
        }

        if (Directory.Exists(targetDatabaseDir))
        {
            Directory.Delete(targetDatabaseDir, true);
        }

        progress?.Report("Existing installation removed.");
    }

    public static void Run(string bundleRoot, InstallOptions options, IProgress<string> progress)
    {
        var installRoot = Path.GetFullPath(options.InstallRoot);
        var payloadRoot = Path.Combine(bundleRoot, "payload");
        var sourceAppDir = Path.Combine(payloadRoot, "Navlight.Registration.App");
        var sourceDatabaseDir = Path.Combine(payloadRoot, "Database");
        var sourceSampleEntriesPath = Path.Combine(payloadRoot, "Samples", "Entries.xlsx");

        if (!Directory.Exists(sourceAppDir))
        {
            throw new InvalidOperationException($"The release payload was not found. Expected '{sourceAppDir}'.");
        }

        if (RequiresAdminForPath(installRoot) && !IsAdministrator())
        {
            throw new InvalidOperationException($"Install folder '{installRoot}' is under Program Files. Choose a non-Program Files folder or rerun as Administrator.");
        }

        Directory.CreateDirectory(installRoot);

        var targetAppDir = Path.Combine(installRoot, "Navlight.Registration.App");
        var targetDatabaseDir = Path.Combine(installRoot, "Database");
        var targetEntryListsDir = Path.Combine(targetAppDir, "EntryLists");

        progress.Report("Copying application files...");
        if (Directory.Exists(targetAppDir))
        {
            Directory.Delete(targetAppDir, true);
        }

        CopyDirectory(sourceAppDir, targetAppDir);

        var resolvedDatabaseServer = options.Mode switch
        {
            InstallMode.SingleUser => "localhost",
            _ => string.IsNullOrWhiteSpace(options.DatabaseServer) ? "navlighthost" : options.DatabaseServer
        };

        if (options.Mode == InstallMode.HostAndClient)
        {
            if (string.IsNullOrWhiteSpace(resolvedDatabaseServer))
            {
                throw new InvalidOperationException("Host installs require a client-facing host name.");
            }

            if (!options.DhcpReservationConfirmed)
            {
                throw new InvalidOperationException("Host install stopped. Create the DHCP reservation on the router first.");
            }

            progress.Report($"Checking hostname '{resolvedDatabaseServer}'...");
            if (!PingHost(resolvedDatabaseServer))
            {
                throw new InvalidOperationException($"Host install stopped. Ping to '{resolvedDatabaseServer}' failed. Confirm the router reservation and local DNS entry exist.");
            }
        }

        progress.Report("Writing app settings...");
        WriteAppSettings(Path.Combine(targetAppDir, "appsettings.json"), resolvedDatabaseServer, options.DatabasePort, options.DatabaseName, options.DatabaseUser, options.DatabasePassword);

        if (options.Mode is InstallMode.HostAndClient or InstallMode.SingleUser)
        {
            if (File.Exists(sourceSampleEntriesPath))
            {
                Directory.CreateDirectory(targetEntryListsDir);
                File.Copy(sourceSampleEntriesPath, Path.Combine(targetEntryListsDir, "Entries.xlsx"), true);
            }

            progress.Report("Copying database files...");
            StopMySqlProcessesUnderRoot(targetDatabaseDir);
            if (Directory.Exists(targetDatabaseDir))
            {
                Directory.Delete(targetDatabaseDir, true);
            }

            CopyDirectory(sourceDatabaseDir, targetDatabaseDir);

            var targetMySqlDir = Path.Combine(targetDatabaseDir, "mysql");
            var mysqldPath = Path.Combine(targetMySqlDir, "bin", "mysqld.exe");

            if (!File.Exists(mysqldPath))
            {
                var resolvedMySqlZipPath = ResolveMySqlArchivePath(bundleRoot);
                progress.Report("Extracting MySQL archive...");
                ExpandMySqlArchive(resolvedMySqlZipPath, targetMySqlDir);
            }

            progress.Report("Initializing MySQL...");
            SetupMySql(targetDatabaseDir, options.DatabasePassword, options.DatabasePort, Path.Combine(targetDatabaseDir, "schema.sql"), progress);
        }

        progress.Report("Creating shortcuts...");
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenuProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var startMenuFolder = Path.Combine(startMenuProgramsPath, "Navlight");
        Directory.CreateDirectory(startMenuFolder);

        CreateShortcut(
            Path.Combine(desktopPath, "Navlight Registration.lnk"),
            Path.Combine(targetAppDir, "Navlight.Registration.App.exe"),
            targetAppDir,
            string.Empty);

        CreateShortcut(
            Path.Combine(startMenuFolder, "Navlight Registration.lnk"),
            Path.Combine(targetAppDir, "Navlight.Registration.App.exe"),
            targetAppDir,
            string.Empty);

        if (options.Mode is InstallMode.HostAndClient or InstallMode.SingleUser)
        {
            var startMySqlTarget = Path.Combine(targetDatabaseDir, "mysql", "bin", "mysqld.exe");
            var startMySqlArgs = $"--console --basedir=\"{Path.Combine(targetDatabaseDir, "mysql")}\" --datadir=\"{Path.Combine(targetDatabaseDir, "data")}\" --port={options.DatabasePort}";
            var stopMySqlTarget = Path.Combine(targetDatabaseDir, "mysql", "bin", "mysqladmin.exe");
            var stopMySqlArgs = $"--protocol=TCP --port={options.DatabasePort} -u root --password={options.DatabasePassword} shutdown";

            CreateShortcut(Path.Combine(desktopPath, "Navlight Start MySQL.lnk"), startMySqlTarget, targetDatabaseDir, startMySqlArgs);
            CreateShortcut(Path.Combine(desktopPath, "Navlight Stop MySQL.lnk"), stopMySqlTarget, targetDatabaseDir, stopMySqlArgs);
            CreateShortcut(Path.Combine(startMenuFolder, "Navlight Start MySQL.lnk"), startMySqlTarget, targetDatabaseDir, startMySqlArgs);
            CreateShortcut(Path.Combine(startMenuFolder, "Navlight Stop MySQL.lnk"), stopMySqlTarget, targetDatabaseDir, stopMySqlArgs);
        }

        progress.Report("Navlight installation complete.");
    }

    private static bool RequiresAdminForPath(string path)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var programFiles = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool PingHost(string host)
    {
        using var ping = new Ping();
        try
        {
            var reply = ping.Send(host, 3000);
            return reply is { Status: IPStatus.Success };
        }
        catch
        {
            return false;
        }
    }

    private static void WriteAppSettings(string path, string server, int port, string database, string user, string password)
    {
        var payload = new
        {
            Database = new
            {
                Server = server,
                Port = port,
                Database = database,
                User = user,
                Password = password
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static void StopMySqlProcessesUnderRoot(string databaseRoot)
    {
        var mysqlRoot = Path.GetFullPath(Path.Combine(databaseRoot, "mysql"));

        foreach (var process in Process.GetProcessesByName("mysqld"))
        {
            try
            {
                var executablePath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    continue;
                }

                if (Path.GetFullPath(executablePath).StartsWith(mysqlRoot, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill(true);
                    process.WaitForExit(10000);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private static string ResolveMySqlArchivePath(string bundleRoot)
    {
        var embeddedPackagePath = Path.Combine(bundleRoot, "MySqlPackage.zip");
        if (File.Exists(embeddedPackagePath))
        {
            return embeddedPackagePath;
        }

        throw new InvalidOperationException("MySQL package was not found in the installer payload. Rebuild the installer with MySqlPackage.zip available.");
    }

    private static void ExpandMySqlArchive(string archivePath, string destinationPath)
    {
        var extractRoot = Path.Combine(Path.GetTempPath(), "navlight-mysql-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractRoot);

        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, extractRoot, true);
            var mysqlFolder = Directory.GetDirectories(extractRoot).FirstOrDefault();
            if (mysqlFolder is null)
            {
                throw new InvalidOperationException("The MySQL archive did not contain a top-level directory.");
            }

            Directory.CreateDirectory(destinationPath);
            foreach (var entry in Directory.GetFileSystemEntries(destinationPath))
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, true);
                }
                else
                {
                    File.Delete(entry);
                }
            }

            CopyDirectory(mysqlFolder, destinationPath);
        }
        finally
        {
            if (Directory.Exists(extractRoot))
            {
                Directory.Delete(extractRoot, true);
            }
        }
    }

    private static void SetupMySql(string databaseRoot, string rootPassword, int port, string schemaPath, IProgress<string>? progress)
    {
        var mysqlRoot = Path.Combine(databaseRoot, "mysql");
        var binRoot = Path.Combine(mysqlRoot, "bin");
        var dataRoot = Path.Combine(databaseRoot, "data");
        var mysqldPath = Path.Combine(binRoot, "mysqld.exe");
        var mysqlPath = Path.Combine(binRoot, "mysql.exe");
        var mysqlAdminPath = Path.Combine(binRoot, "mysqladmin.exe");

        AssertFileExists(mysqldPath, "mysqld.exe");
        AssertFileExists(mysqlPath, "mysql.exe");
        AssertFileExists(mysqlAdminPath, "mysqladmin.exe");
        AssertFileExists(schemaPath, "schema.sql");

        Directory.CreateDirectory(dataRoot);

        ClearPortIfInUse(port, progress);

        if (!Directory.EnumerateFileSystemEntries(dataRoot).Any())
        {
            RunProcessAndThrowOnFailure(mysqldPath, ["--initialize-insecure", $"--basedir={mysqlRoot}", $"--datadir={dataRoot}"], null, "MySQL data directory initialization failed", TimeSpan.FromMinutes(2));
        }

        var bootstrapPort = GetAvailableLoopbackPort();
        var mysqlErrorLogPath = Path.Combine(dataRoot, $"{Environment.MachineName}.err");

        var startInfo = new ProcessStartInfo(mysqldPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add($"--basedir={mysqlRoot}");
        startInfo.ArgumentList.Add($"--datadir={dataRoot}");
        startInfo.ArgumentList.Add($"--port={bootstrapPort}");

        var startupDeadline = TimeSpan.FromMinutes(2);
        using var serverProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start temporary MySQL process.");

        try
        {
            progress?.Report($"Waiting for temporary MySQL to start on port {bootstrapPort}...");
            WaitForPort(bootstrapPort, startupDeadline);

            progress?.Report("Setting the MySQL root password...");
            var escapedPassword = rootPassword.Replace("'", "''", StringComparison.Ordinal);
            var bootstrapSql = $"ALTER USER 'root'@'localhost' IDENTIFIED BY '{escapedPassword}';{Environment.NewLine}FLUSH PRIVILEGES;";
            RunProcessAndThrowOnFailure(mysqlPath, ["--protocol=TCP", $"--port={bootstrapPort}", "-u", "root"], bootstrapSql, "Setting the MySQL root password failed", TimeSpan.FromMinutes(1));

            progress?.Report("Loading the registration schema...");
            var schemaSql = File.ReadAllText(schemaPath);
            RunProcessAndThrowOnFailure(mysqlPath, ["--protocol=TCP", $"--port={bootstrapPort}", "-u", "root", $"--password={rootPassword}"], schemaSql, "Loading the registration schema failed", TimeSpan.FromMinutes(1));
        }
        catch (Exception ex)
        {
            var serverExitCode = serverProcess.HasExited ? serverProcess.ExitCode.ToString() : "running";
            var errorLogTail = ReadTail(mysqlErrorLogPath, 60);

            var details = new StringBuilder();
            details.AppendLine(ex.Message);
            details.AppendLine($"MySQL error log: {mysqlErrorLogPath}");
            details.AppendLine($"mysqld status: {serverExitCode}");
            if (!string.IsNullOrWhiteSpace(errorLogTail))
            {
                details.AppendLine("Last MySQL error-log lines:");
                details.AppendLine(errorLogTail);
            }

            throw new InvalidOperationException(details.ToString());
        }
        finally
        {
            if (!serverProcess.HasExited)
            {
                try
                {
                    RunProcessAndThrowOnFailure(mysqlAdminPath, ["--protocol=TCP", $"--port={bootstrapPort}", "-u", "root", $"--password={rootPassword}", "shutdown"], null, "mysqladmin shutdown failed", TimeSpan.FromSeconds(30));
                    serverProcess.WaitForExit(10000);
                }
                catch
                {
                    // Ignore and force kill below.
                }

                if (!serverProcess.HasExited)
                {
                    serverProcess.Kill(true);
                }
            }
        }
    }

    private static async Task PumpOutputAsync(StreamReader reader, StreamWriter writer)
    {
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is not null)
            {
                await writer.WriteLineAsync(line).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
    }

    private static string? ReadTail(string path, int lineCount)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var lines = File.ReadLines(path).TakeLast(lineCount).ToArray();
        return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private static bool IsPortListening(int port)
    {
        return CanConnectToPort(IPAddress.Loopback, port, 300) || CanConnectToPort(IPAddress.IPv6Loopback, port, 300);
    }

    private static void ClearPortIfInUse(int port, IProgress<string>? progress)
    {
        if (!IsPortListening(port))
        {
            return;
        }

        progress?.Report($"Port {port} is still in use. Attempting to stop the process using it...");

        foreach (var processId in FindProcessIdsListeningOnPort(port))
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                progress?.Report($"Stopping process '{process.ProcessName}' (PID {processId}) using port {port}...");
                process.Kill(true);
                process.WaitForExit(10000);
            }
            catch
            {
                // Best effort; the process may have already exited or be inaccessible.
            }
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (IsPortListening(port) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(250);
        }

        if (IsPortListening(port))
        {
            throw new InvalidOperationException($"Port {port} is still in use and could not be freed automatically. Stop the process manually and rerun the installer.");
        }
    }

    private static IReadOnlyCollection<int> FindProcessIdsListeningOnPort(int port)
    {
        var processIds = new HashSet<int>();

        try
        {
            var startInfo = new ProcessStartInfo("netstat.exe", "-ano")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return processIds;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5 || !string.Equals(parts[3], "LISTENING", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var localAddress = parts[1];
                var portSeparatorIndex = localAddress.LastIndexOf(':');
                if (portSeparatorIndex < 0 ||
                    !int.TryParse(localAddress[(portSeparatorIndex + 1)..], out var localPort) ||
                    localPort != port)
                {
                    continue;
                }

                if (int.TryParse(parts[^1], out var processId))
                {
                    processIds.Add(processId);
                }
            }
        }
        catch
        {
            // Best effort; if netstat isn't available, nothing more we can do here.
        }

        return processIds;
    }

    private static int GetAvailableLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void WaitForPort(int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsPortListening(port))
            {
                return;
            }

            Thread.Sleep(500);
        }

        throw new InvalidOperationException($"MySQL did not start listening on port {port} within {timeout.TotalSeconds} seconds.");
    }

    private static bool CanConnectToPort(IPAddress address, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient(address.AddressFamily);
            var connectTask = client.ConnectAsync(address, port);
            return connectTask.Wait(timeoutMs) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static void RunProcessAndThrowOnFailure(string filePath, IReadOnlyList<string> arguments, string? standardInput, string errorPrefix, TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo(filePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process '{filePath}'.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (standardInput is not null)
        {
            process.StandardInput.Write(standardInput);
            process.StandardInput.Close();
        }

        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(2);
        if (!process.WaitForExit((int)effectiveTimeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // Best effort cleanup.
            }

            throw new InvalidOperationException($"{errorPrefix} timed out after {effectiveTimeout.TotalSeconds} seconds.");
        }

        Task.WaitAll([outputTask, errorTask], 5000);
        var output = outputTask.IsCompletedSuccessfully ? outputTask.Result : string.Empty;
        var error = errorTask.IsCompletedSuccessfully ? errorTask.Result : string.Empty;

        if (process.ExitCode != 0)
        {
            var message = new StringBuilder();
            message.AppendLine($"{errorPrefix} with exit code {process.ExitCode}.");
            if (!string.IsNullOrWhiteSpace(error))
            {
                message.AppendLine(error.Trim());
            }

            if (!string.IsNullOrWhiteSpace(output))
            {
                message.AppendLine(output.Trim());
            }

            throw new InvalidOperationException(message.ToString());
        }
    }

    private static void AssertFileExists(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{description} was not found at '{path}'.");
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var source = new DirectoryInfo(sourceDir);
        if (!source.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in source.GetFiles())
        {
            var targetPath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetPath, true);
        }

        foreach (var directory in source.GetDirectories())
        {
            var targetSubDir = Path.Combine(destinationDir, directory.Name);
            CopyDirectory(directory.FullName, targetSubDir);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string arguments)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell COM object is not available.");
        dynamic shell = Activator.CreateInstance(shellType) ?? throw new InvalidOperationException("Failed to create WScript.Shell COM object.");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Arguments = arguments;
        shortcut.Save();
    }
}
