namespace DotnetSdkManager.Cli;

internal sealed class CliArguments
{
    private static readonly HashSet<string> BooleanOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "help",
        "version",
        "json",
        "include-preview",
        "allow-eol",
        "offline",
        "allow-http",
        "activate",
        "force",
        "managed-only",
        "no-activate",
        "allow-uninstalled",
        "allow-prerelease",
        "no-allow-prerelease",
        "all-channels"
    };

    private readonly Dictionary<string, List<string>> _options;

    private CliArguments(string command, IReadOnlyList<string> positionals, Dictionary<string, List<string>> options)
    {
        Command = command;
        Positionals = positionals;
        _options = options;
    }

    public string Command { get; }

    public IReadOnlyList<string> Positionals { get; }

    public static CliArguments Parse(string[] args)
    {
        var options = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();
        var command = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            if (token is "-h")
            {
                Add(options, "help", "true");
                continue;
            }

            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var option = token[2..];
                var equals = option.IndexOf('=');
                if (equals >= 0)
                {
                    Add(options, option[..equals], option[(equals + 1)..]);
                    continue;
                }

                if (BooleanOptions.Contains(option))
                {
                    Add(options, option, "true");
                    continue;
                }

                if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Option '--{option}' requires a value.");
                }

                Add(options, option, args[++index]);
                continue;
            }

            if (string.IsNullOrEmpty(command))
            {
                command = token.ToLowerInvariant();
            }
            else
            {
                positionals.Add(token);
            }
        }

        return new CliArguments(command, positionals, options);
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) =>
        _options.TryGetValue(name, out var values) ? values.LastOrDefault() : null;

    public IReadOnlyList<string> GetAll(string name) =>
        _options.TryGetValue(name, out var values) ? values : [];

    public string RequirePositional(int index, string description) =>
        index < Positionals.Count
            ? Positionals[index]
            : throw new ArgumentException($"Missing {description}.");

    private static void Add(Dictionary<string, List<string>> options, string name, string value)
    {
        if (!options.TryGetValue(name, out var values))
        {
            values = [];
            options[name] = values;
        }

        values.Add(value);
    }
}
