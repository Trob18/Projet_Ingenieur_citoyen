using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace ArchiveNumerique.Services
{
    /// <summary>
    /// Orchestrateur : tente le sitemap d'abord, sinon crawl le site en respectant robots.txt.
    /// Retourne uniquement des liens internes propres (pas de query string, pas de fragment).
    /// </summary>
    internal class CrawlerService
    {
        private readonly HttpClient _httpClient;
        private readonly int _maxPages;
        private readonly int _delayMs;

        /// <param name="maxPages">Nombre max de pages à visiter en mode crawl.</param>
        /// <param name="delayMs">Délai en ms entre chaque requête pour être poli.</param>
        public CrawlerService(int maxPages = 500, int delayMs = 200)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ArchiveNumeriqueBot/1.0 (+https://github.com/Trob18/Projet_Ingenieur_citoyen)");
            _maxPages = maxPages;
            _delayMs = delayMs;
        }

        /// <summary>
        /// Point d'entrée principal : retourne la liste de tous les liens internes propres.
        /// </summary>
        public async Task<List<string>> GetInternalLinksAsync(string url)
        {
            var baseUri = new Uri(url.TrimEnd('/') + "/");

            // 1. Lire robots.txt
            var robotsService = new RobotsService(_httpClient);
            var robots = await robotsService.ParseAsync(baseUri);

            // 2. Tenter le sitemap
            var sitemapService = new SitemapService(_httpClient);
            var sitemapLinks = await sitemapService.GetLinksAsync(robots.SitemapUrls, baseUri);

            if (sitemapLinks != null && sitemapLinks.Count > 0)
            {
                // Filtrer selon robots.txt
                return sitemapLinks
                    .Where(link => IsAllowedByRobots(link, baseUri, robots.DisallowedPaths))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(l => l)
                    .ToList();
            }

            // 3. Fallback : crawl BFS en respectant robots.txt
            return await CrawlAsync(baseUri, robots.DisallowedPaths);
        }

        private async Task<List<string>> CrawlAsync(Uri baseUri, List<string> disallowedPaths)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();

            var startUrl = CleanUrl(baseUri.ToString());
            queue.Enqueue(startUrl);
            visited.Add(startUrl);

            while (queue.Count > 0 && visited.Count < _maxPages)
            {
                var currentUrl = queue.Dequeue();

                // Vérifier robots.txt
                var path = new Uri(currentUrl).AbsolutePath;
                if (!RobotsService.IsAllowed(path, disallowedPaths))
                    continue;

                var links = await ExtractLinksFromPageAsync(currentUrl, baseUri);

                foreach (var link in links)
                {
                    if (!visited.Contains(link))
                    {
                        visited.Add(link);
                        queue.Enqueue(link);
                    }
                }

                // Délai de politesse
                if (_delayMs > 0)
                    await Task.Delay(_delayMs);
            }

            return visited
                .Where(link => IsAllowedByRobots(link, baseUri, disallowedPaths))
                .OrderBy(l => l)
                .ToList();
        }

        private async Task<List<string>> ExtractLinksFromPageAsync(string url, Uri baseUri)
        {
            var results = new List<string>();

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return results;

                // Ne parser que du HTML
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                    return results;

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
                if (anchors == null)
                    return results;

                foreach (var anchor in anchors)
                {
                    var href = anchor.GetAttributeValue("href", "");
                    if (string.IsNullOrWhiteSpace(href))
                        continue;

                    // Ignorer les liens javascript:, mailto:, tel:, #
                    if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                        href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                        href == "#")
                        continue;

                    // Résoudre les URLs relatives
                    if (!Uri.TryCreate(new Uri(url), href, out var resolved))
                        continue;

                    // Garder uniquement les liens internes
                    if (!resolved.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Uniquement HTTP/HTTPS
                    if (resolved.Scheme != "http" && resolved.Scheme != "https")
                        continue;

                    var clean = CleanUrl(resolved.ToString());
                    results.Add(clean);
                }
            }
            catch
            {
                // Page inaccessible — on continue le crawl
            }

            return results;
        }

        private static bool IsAllowedByRobots(string url, Uri baseUri, List<string> disallowedPaths)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return RobotsService.IsAllowed(uri.AbsolutePath, disallowedPaths);
            }
            return true;
        }

        /// <summary>
        /// Nettoie l'URL : supprime query string, fragment, trailing slash.
        /// Cela garantit des liens propres sans paramètres GET/POST.
        /// </summary>
        private static string CleanUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            }
            return url.TrimEnd('/');
        }
    }
}
