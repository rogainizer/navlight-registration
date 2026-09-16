using System.Text;
using MySqlConnector;
using Navlight.Registration.App.Models;

namespace Navlight.Registration.App.Services;

internal static class MySqlInteractionLogger
{
    private static readonly object SyncRoot = new();

    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "navlight-mysql.log");

    public static void LogInfo(string message)
    {
        Write("INFO", message);
    }

    public static void LogError(string message)
    {
        Write("ERROR", message);
    }

    public static void LogConnectionAttempt(DatabaseOptions options, string context)
    {
        LogInfo($"Connection attempt ({context}) server={options.Server};port={options.Port};database={options.Database};user={options.User}");
    }

    public static void LogCommandStart(string operation, MySqlCommand command)
    {
        LogInfo($"{operation} start sql=\"{NormalizeSql(command.CommandText)}\" params=[{FormatParameters(command.Parameters)}]");
    }

    public static void LogCommandSuccess(string operation, string detail)
    {
        LogInfo($"{operation} success {detail}");
    }

    public static void LogCommandFailure(string operation, Exception ex, MySqlCommand command)
    {
        LogError($"{operation} failure sql=\"{NormalizeSql(command.CommandText)}\" params=[{FormatParameters(command.Parameters)}] exception={ex.GetType().Name}: {ex.Message}");
    }

    private static string NormalizeSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return string.Empty;
        }

        return sql.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static string FormatParameters(MySqlParameterCollection parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(parameters.Count);
        foreach (MySqlParameter parameter in parameters)
        {
            var valueText = IsSensitive(parameter.ParameterName)
                ? "<redacted>"
                : FormatValue(parameter.Value);
            parts.Add($"{parameter.ParameterName}={valueText}");
        }

        return string.Join(", ", parts);
    }

    private static bool IsSensitive(string parameterName)
    {
        return parameterName.Contains("password", StringComparison.OrdinalIgnoreCase)
               || parameterName.Contains("pwd", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatValue(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return "null";
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("O");
        }

        var text = value.ToString() ?? string.Empty;
        if (text.Length > 120)
        {
            return text[..120] + "...";
        }

        return text;
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (SyncRoot)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
