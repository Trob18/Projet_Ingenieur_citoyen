using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace ArchiveNumerique.Services
{
    /// <summary>
    /// Parse robots.txt pour extraire les règles Disallow et le lien Sitemap.
    /// </summary>
    internal class RobotsService
    {
        private readonly HttpClient _httpClient;

        public RobotsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private const string BotName = "ArchiveNumeriqueBot";

        /// <summary>
        /// Télécharge et parse robots.txt.
        /// Priorité : règles spécifiques à ArchiveNumeriqueBot, sinon règles wildcard "*".
        /// </summary>
        public async Task<RobotsResult> ParseAsync(Uri baseUri)
        {
            var result = new RobotsResult();
            try
            {
                var robotsUrl = new Uri(baseUri, "/robots.txt");
                var content = await _httpClient.GetStringAsync(robotsUrl);

                var wildcardDisallowed = new List<string>();
                var botDisallowed = new List<string>();
                var currentAgent = "";

                foreach (var rawLine in content.Split('\n'))
                {
                    var line = rawLine.Trim();

                    if (line.StartsWith("#", StringComparison.Ordinal) || string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentAgent = line.Substring("User-agent:".Length).Trim();
                    }
                    else if (line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = line.Substring("Disallow:".Length).Trim();
                        if (!string.IsNullOrEmpty(path))
                        {
                            if (currentAgent == "*")
                                wildcardDisallowed.Add(path);
                            else if (currentAgent.Equals(BotName, StringComparison.OrdinalIgnoreCase))
                                botDisallowed.Add(path);
                        }
                    }
                    else if (line.StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase))
                    {
                        var sitemapUrl = line.Substring("Sitemap:".Length).Trim();
                        if (Uri.TryCreate(sitemapUrl, UriKind.Absolute, out _))
                        {
                            result.SitemapUrls.Add(sitemapUrl);
                        }
                    }
                }

                // Règles spécifiques à notre bot en priorité, sinon wildcard
                result.DisallowedPaths = botDisallowed.Count > 0 ? botDisallowed : wildcardDisallowed;
            }
            catch (HttpRequestException)
            {
                // robots.txt introuvable — on considère tout autorisé
            }

            return result;
        }

        /// <summary>
        /// Vérifie si un chemin est autorisé selon les règles Disallow.
        /// </summary>
        public static bool IsAllowed(string path, List<string> disallowedPaths)
        {
            foreach (var disallowed in disallowedPaths)
            {
                if (disallowed.EndsWith("*"))
                {
                    var prefix = disallowed.TrimEnd('*');
                    if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (path.StartsWith(disallowed, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }
    }

    internal class RobotsResult
    {
        public List<string> DisallowedPaths { get; set; } = new();
        public List<string> SitemapUrls { get; set; } = new();
    }
}