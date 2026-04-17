using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ArchiveNumerique.Services
{
    /// <summary>
    /// Parse les fichiers sitemap.xml (y compris les sitemap index).
    /// </summary>
    internal class SitemapService
    {
        private readonly HttpClient _httpClient;

        public SitemapService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Tente de récupérer les URLs depuis le(s) sitemap(s).
        /// Retourne null si aucun sitemap n'est exploitable.
        /// </summary>
        public async Task<List<string>?> GetLinksAsync(List<string> sitemapUrls, Uri baseUri)
        {
            // Si robots.txt n'a pas indiqué de sitemap, on tente l'URL par défaut
            if (sitemapUrls.Count == 0)
            {
                sitemapUrls = new List<string> { new Uri(baseUri, "/sitemap.xml").ToString() };
            }

            var allLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var url in sitemapUrls)
            {
                await ParseSitemapRecursiveAsync(url, baseUri, allLinks);
            }

            return allLinks.Count > 0 ? new List<string>(allLinks) : null;
        }

        private async Task ParseSitemapRecursiveAsync(string sitemapUrl, Uri baseUri, HashSet<string> results)
        {
            try
            {
                var content = await _httpClient.GetStringAsync(sitemapUrl);
                var doc = XDocument.Parse(content);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                // Sitemap Index — contient des <sitemap><loc>
                foreach (var loc in doc.Descendants(ns + "sitemap").Elements(ns + "loc"))
                {
                    var childUrl = loc.Value.Trim();
                    if (!string.IsNullOrEmpty(childUrl))
                    {
                        await ParseSitemapRecursiveAsync(childUrl, baseUri, results);
                    }
                }

                // Sitemap standard — contient des <url><loc>
                foreach (var loc in doc.Descendants(ns + "url").Elements(ns + "loc"))
                {
                    var link = loc.Value.Trim();
                    if (IsInternalCleanLink(link, baseUri))
                    {
                        results.Add(CleanUrl(link));
                    }
                }
            }
            catch
            {
                // Sitemap inaccessible ou mal formé — on ignore
            }
        }

        private static bool IsInternalCleanLink(string url, Uri baseUri)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                   && uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase);
        }

        private static string CleanUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                // On enlève query string et fragment → liens propres
                return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            }
            return url.TrimEnd('/');
        }
    }
}