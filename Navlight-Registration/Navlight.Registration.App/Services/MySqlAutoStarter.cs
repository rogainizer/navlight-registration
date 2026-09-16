using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MySqlConnector;
using Navlight.Registration.App.Models;

namespace Navlight.Registration.App.Services;

internal static class MySqlAutoStarter
{
    private static string? _startedMysqlRoot;
    private static DatabaseOptions? _startedOptions;

    public static void EnsureStartedIfNeeded(DatabaseOptions options)
    {
        if (!ShouldAttemptLocalStart(options.Server))
        {
            MySqlInteractionLogger.LogInfo($"Auto-start skipped for non-local server '{options.Server}'.");
            return;
        }

        if (CanOpenDatabaseConnection(options))
        {
            MySqlInteractionLogger.LogInfo($"Auto-start skipped: MySQL already reachable on {options.Server}:{options.Port}.");
            return;
        }

        if (IsPortListening(options.Port))
        {
            MySqlInteractionLogger.LogError($"Auto-start blocked: port {options.Port} is open but DB login failed for configured credentials.");
            throw new InvalidOperationException($"Port {options.Port} is already open, but Navlight could not connect to MySQL using the configured credentials. Stop the existing process on that port or update appsettings.json.");
        }

        var appRoot = AppContext.BaseDirectory;
        var databaseRoot = ResolveDatabaseRoot(appRoot);
        if (string.IsNullOrWhiteSpace(databaseRoot))
        {
            MySqlInteractionLogger.LogError($"Auto-start skipped: unable to resolve Database folder from app root '{appRoot}'.");
            return;
        }

        var mysqlRoot = Path.Combine(databaseRoot, "mysql");
        var mysqldPath = Path.Combine(mysqlRoot, "bin", "mysqld.exe");
        var dataRoot = Path.Combine(databaseRoot, "data");

        if (!File.Exists(mysqldPath) || !Directory.Exists(dataRoot))
        {
            MySqlInteractionLogger.LogError($"Auto-start skipped: mysqld/data path missing. mysqld='{mysqldPath}', data='{dataRoot}'.");
            return;
        }

        MySqlInteractionLogger.LogInfo($"Auto-start launching mysqld at '{mysqldPath}' on port {options.Port}.");

        var startInfo = new ProcessStartInfo(mysqldPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = databaseRoot
        };

        startInfo.ArgumentList.Add("--basedir=" + mysqlRoot);
        startInfo.ArgumentList.Add("--datadir=" + dataRoot);
        startInfo.ArgumentList.Add("--port=" + options.Port);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start mysqld.exe.");

        if (!WaitForDatabaseConnection(options, TimeSpan.FromSeconds(25)))
        {
            MySqlInteractionLogger.LogError($"Auto-start failed: MySQL not ready on port {options.Port} after timeout.");
            throw new InvalidOperationException($"MySQL did not become ready on port {options.Port}.");
        }

        _startedMysqlRoot = mysqlRoot;
        _startedOptions = options;

        MySqlInteractionLogger.LogInfo($"Auto-start success: MySQL is ready on {options.Server}:{options.Port}.");
    }

    public static void StopIfStarted()
    {
        if (_startedMysqlRoot is null || _startedOptions is null)
        {
            return;
        }

        var mysqlRoot = _startedMysqlRoot;
        var options = _startedOptions;
        _startedMysqlRoot = null;
        _startedOptions = null;

        var mysqladminPath = Path.Combine(mysqlRoot, "bin", "mysqladmin.exe");
        if (!File.Exists(mysqladminPath))
        {
            MySqlInteractionLogger.LogError($"Auto-stop skipped: mysqladmin.exe not found at '{mysqladminPath}'.");
            return;
        }

        try
        {
            MySqlInteractionLogger.LogInfo($"Auto-stop shutting down mysqld on port {options.Port}.");

            var startInfo = new ProcessStartInfo(mysqladminPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("--protocol=TCP");
            startInfo.ArgumentList.Add($"--port={options.Port}");
            startInfo.ArgumentList.Add("-u");
            startInfo.ArgumentList.Add(options.User);
            startInfo.ArgumentList.Add($"--password={options.Password}");
            startInfo.ArgumentList.Add("shutdown");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                MySqlInteractionLogger.LogError("Auto-stop failed: could not start mysqladmin.exe.");
                return;
            }

            if (!process.WaitForExit(10000))
            {
                MySqlInteractionLogger.LogError("Auto-stop timed out waiting for mysqladmin shutdown.");
                return;
            }

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd();
                MySqlInteractionLogger.LogError($"Auto-stop: mysqladmin shutdown exited with code {process.ExitCode}. {stderr}");
                return;
            }

            MySqlInteractionLogger.LogInfo("Auto-stop success: MySQL stopped.");
        }
        catch (Exception ex)
        {
            MySqlInteractionLogger.LogError($"Auto-stop failed: {ex.Message}");
        }
    }

    internal static bool ShouldAttemptLocalStart(string server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return false;
        }

        var normalized = server.Trim();
        if (string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var machineName = Environment.MachineName;
        if (string.Equals(normalized, machineName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var hostName = Dns.GetHostName();
        if (string.Equals(normalized, hostName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var shortHostName = hostName.Split('.').FirstOrDefault();
        return !string.IsNullOrWhiteSpace(shortHostName) &&
               string.Equals(normalized, shortHostName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WaitForPort(int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsPortListening(port))
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private static bool WaitForDatabaseConnection(DatabaseOptions options, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (CanOpenDatabaseConnection(options))
            {
                return true;
            }

            Thread.Sleep(300);
        }

        return false;
    }

    private static bool CanOpenDatabaseConnection(DatabaseOptions options)
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(options.ToConnectionString())
            {
                ConnectionTimeout = 2
            };

            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPortListening(int port)
    {
        return CanConnect(IPAddress.Loopback, port) || CanConnect(IPAddress.IPv6Loopback, port);
    }

    private static bool CanConnect(IPAddress ipAddress, int port)
    {
        try
        {
            using var client = new TcpClient(ipAddress.AddressFamily);
            var connectTask = client.ConnectAsync(ipAddress, port);
            return connectTask.Wait(250) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveDatabaseRoot(string appRoot)
    {
        var fullAppRoot = Path.GetFullPath(appRoot);
        var parent = Directory.GetParent(fullAppRoot)?.FullName;

        var candidates = new List<string>
        {
            Path.Combine(fullAppRoot, "..", "Database"),
            Path.Combine(fullAppRoot, "Database")
        };

        if (!string.IsNullOrWhiteSpace(parent))
        {
            candidates.Add(Path.Combine(parent, "Database"));
        }

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            var mysqlBin = Path.Combine(resolved, "mysql", "bin", "mysqld.exe");
            var dataPath = Path.Combine(resolved, "data");
            if (File.Exists(mysqlBin) && Directory.Exists(dataPath))
            {
                return resolved;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }
}
