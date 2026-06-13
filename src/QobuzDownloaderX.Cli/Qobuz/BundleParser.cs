using System.Text;
using System.Text.RegularExpressions;

namespace QobuzDownloaderX.Cli.Qobuz;

/// <summary>
/// Extracts the app_id and the list of app secrets from Qobuz's publicly
/// available web-player JavaScript bundle. This mirrors what the original
/// QopenAPI did via <c>GetAppID()</c> / <c>GetAppSecret()</c> and what other
/// public Qobuz tooling (qobuz-dl, streamrip) do. No private keys are shipped
/// with this program; everything is scraped client-side at runtime.
/// </summary>
public static class BundleParser
{
    private const string LoginPage = "https://play.qobuz.com/login";
    private const string BaseUrl = "https://play.qobuz.com";

    public sealed record Credentials(string AppId, IReadOnlyList<string> Secrets);

    public static async Task<Credentials> FetchAsync(HttpClient http, CancellationToken ct = default)
    {
        string loginHtml = await http.GetStringAsync(LoginPage, ct).ConfigureAwait(false);

        var bundleMatch = Regex.Match(
            loginHtml,
            @"<script src=""(?<url>/resources/\d+\.\d+\.\d+-[a-z\d]+/bundle\.js)""");
        if (!bundleMatch.Success)
            throw new QobuzApiException("Could not locate the Qobuz web-player bundle.js URL.");

        string bundleUrl = BaseUrl + bundleMatch.Groups["url"].Value;
        string bundle = await http.GetStringAsync(bundleUrl, ct).ConfigureAwait(false);

        string appId = ExtractAppId(bundle);
        var secrets = ExtractSecrets(bundle);
        if (secrets.Count == 0)
            throw new QobuzApiException("Failed to extract any app secret from bundle.js.");

        return new Credentials(appId, secrets);
    }

    private static string ExtractAppId(string bundle)
    {
        var m = Regex.Match(bundle, @"production:\{api:\{appId:""(?<appId>\d{9})"",appSecret:""\w+""");
        if (m.Success) return m.Groups["appId"].Value;

        // Fallback: any 9 digit appId assignment.
        m = Regex.Match(bundle, @"appId:""(?<appId>\d{9})""");
        if (m.Success) return m.Groups["appId"].Value;

        throw new QobuzApiException("Failed to extract app_id from bundle.js.");
    }

    private static List<string> ExtractSecrets(string bundle)
    {
        // 1. Collect seed + timezone pairs, preserving order.
        var seeds = new List<(string Timezone, string Seed)>();
        foreach (Match m in Regex.Matches(
                     bundle,
                     @"[a-z]\.initialSeed\(""(?<seed>[\w=]+)"",window\.utimezone\.(?<timezone>[a-z]+)\)"))
        {
            seeds.Add((m.Groups["timezone"].Value, m.Groups["seed"].Value));
        }

        if (seeds.Count == 0) return new List<string>();

        // Reproduce the ordering quirk from the reference implementations:
        // the second timezone is moved to the front.
        var ordered = new List<(string Timezone, string Seed)>(seeds);
        if (ordered.Count > 1)
        {
            var second = ordered[1];
            ordered.RemoveAt(1);
            ordered.Insert(0, second);
        }

        // 2. For each timezone, find its info/extras chunk and assemble the
        //    base64 string: seed + info + extras, then drop the last 44 chars
        //    and base64-decode the remainder.
        var secrets = new List<string>();
        foreach (var (timezone, seed) in ordered)
        {
            string capitalised = char.ToUpperInvariant(timezone[0]) + timezone.Substring(1);
            var m = Regex.Match(
                bundle,
                $@"name:""\w+/{capitalised}"",info:""(?<info>[\w=]+)"",extras:""(?<extras>[\w=]+)""");
            if (!m.Success) continue;

            string combined = seed + m.Groups["info"].Value + m.Groups["extras"].Value;
            if (combined.Length <= 44) continue;
            combined = combined.Substring(0, combined.Length - 44);

            try
            {
                string secret = Encoding.UTF8.GetString(Convert.FromBase64String(combined));
                if (!string.IsNullOrWhiteSpace(secret))
                    secrets.Add(secret);
            }
            catch (FormatException)
            {
                // Skip malformed candidates.
            }
        }

        return secrets;
    }
}
