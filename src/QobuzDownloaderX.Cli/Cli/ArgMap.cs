namespace QobuzDownloaderX.Cli.Cli;

/// <summary>
/// Minimal argument parser: supports "--key value", "--key=value", boolean
/// "--flag", and positional arguments. Avoids pulling in an external dependency.
/// </summary>
public sealed class ArgMap
{
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positionals = new();

    public ArgMap(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                string key = a.Substring(2);
                int eq = key.IndexOf('=');
                if (eq >= 0)
                {
                    _options[key.Substring(0, eq)] = key.Substring(eq + 1);
                }
                else if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    _options[key] = args[++i];
                }
                else
                {
                    _options[key] = "true"; // boolean flag
                }
            }
            else
            {
                _positionals.Add(a);
            }
        }
    }

    public string? Get(string key) => _options.TryGetValue(key, out var v) ? v : null;

    public bool Has(string key) => _options.ContainsKey(key);

    public IReadOnlyList<string> Positionals => _positionals;
}
