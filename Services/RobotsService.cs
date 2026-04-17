using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Télécharge et parse robots.txt pour l'user-agent "*".
        /// </summary>
        public async Task<RobotsResult> ParseAsync(Uri baseUri)
        {
            var result = new RobotsResult();
            try
            {
                var robotsUrl = new Uri(baseUri, "/robots.txt");
                var content = await _httpClient.GetStringAsync(robotsUrl);

                bool appliesToUs = false;

                foreach (var rawLine in content.Split('\n'))
                {
                    var line = rawLine.Trim();

                    if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                    {
                        var agent = line.Substring("User-agent:".Length).Trim();
                        appliesToUs = agent == "*";
                    }
                    else if (line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase) && appliesToUs)
                    {
                        var path = line.Substring("Disallow:".Length).Trim();
                        if (!string.IsNullOrEmpty(path))
                        {
                            result.DisallowedPaths.Add(path);
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