using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ArchiveNumerique.Services
{
    internal class SitemapService
    {
        private readonly HttpClient _httpClient;

        public SitemapService()
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        }

        public async Task<List<string>> GetSitemapUrlsAsync(string domainUrl)
        {
            var finalUrls = new HashSet<string>();
            var sitemapFilesFound = new List<string>();
            string rootUrl = domainUrl.TrimEnd('/');

            string robotsUrl = rootUrl + "/robots.txt";

            try
            {
                var response = await _httpClient.GetAsync(robotsUrl);
                if (response.IsSuccessStatusCode)
                {
                    string robotsContent = await response.Content.ReadAsStringAsync();
                    var matches = Regex.Matches(robotsContent, @"^Sitemap:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    foreach (Match m in matches)
                    {
                        string sUrl = m.Groups[1].Value.Trim();
                        if (sUrl.StartsWith("/")) sUrl = rootUrl + sUrl;
                        sitemapFilesFound.Add(sUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($">>>> [ERREUR] Robots.txt : {ex.Message}");
            }

            if (sitemapFilesFound.Count == 0)
            {
                sitemapFilesFound.Add(rootUrl + "/sitemap.xml");
            }

            foreach (var sUrl in sitemapFilesFound)
            {
                await ParseSitemapInternal(sUrl, finalUrls);
            }

            return finalUrls.ToList();
        }

        private async Task ParseSitemapInternal(string url, HashSet<string> results)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (contentType.Contains("html")) return;

                string xmlContent = await response.Content.ReadAsStringAsync();
                xmlContent = xmlContent.Trim();

                if (string.IsNullOrEmpty(xmlContent) || !xmlContent.StartsWith("<")) return;

                XDocument doc = XDocument.Parse(xmlContent);
                XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                var sitemapNodes = doc.Descendants(ns + "sitemap").ToList();
                if (sitemapNodes.Any())
                {
                    foreach (var node in sitemapNodes)
                    {
                        string subUrl = node.Element(ns + "loc")?.Value;
                        if (!string.IsNullOrEmpty(subUrl) && subUrl != url)
                            await ParseSitemapInternal(subUrl, results);
                    }
                }

                var urlNodes = doc.Descendants(ns + "url").ToList();
                foreach (var node in urlNodes)
                {
                    string finalUrl = node.Element(ns + "loc")?.Value;
                    if (!string.IsNullOrEmpty(finalUrl)) results.Add(finalUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($">>>> [ERREUR] Parsing {url} : {ex.Message}");
            }
        }
    }
}