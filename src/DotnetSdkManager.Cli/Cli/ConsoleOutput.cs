using System.Text.Json;

namespace DotnetSdkManager.Cli;

internal static class ConsoleOutput
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void Json(object value) => Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    public static void Error(string message) => Console.Error.WriteLine($"error: {message}");

    public static void Warning(string message) => Console.Error.WriteLine($"warning: {message}");
}
