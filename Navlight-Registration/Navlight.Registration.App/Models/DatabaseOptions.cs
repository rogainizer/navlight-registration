using System.Text.Json;
using MySqlConnector;

namespace Navlight.Registration.App.Models;

public sealed class DatabaseOptions
{
    public string Server { get; init; } = string.Empty;
    public int Port { get; init; } = 3306;
    public string Database { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public string ToConnectionString()
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = Server,
            Port = (uint)Port,
            Database = Database,
            UserID = User,
            Password = Password,
            AllowUserVariables = true,
            AllowPublicKeyRetrieval = true,
            SslMode = MySqlSslMode.None
        };

        return builder.ConnectionString;
    }

    public static DatabaseOptions Load()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException($"Configuration file not found: {configPath}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!document.RootElement.TryGetProperty("Database", out var databaseSection))
        {
            throw new InvalidOperationException("Database configuration section is missing.");
        }

        return new DatabaseOptions
        {
            Server = databaseSection.GetProperty("Server").GetString() ?? string.Empty,
            Port = databaseSection.GetProperty("Port").GetInt32(),
            Database = databaseSection.GetProperty("Database").GetString() ?? string.Empty,
            User = databaseSection.GetProperty("User").GetString() ?? string.Empty,
            Password = databaseSection.GetProperty("Password").GetString() ?? string.Empty
        };
    }
}
