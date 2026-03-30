using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abot2.Crawler;
using Abot2.Poco;

namespace ArchiveNumerique.Services
{
    internal class CrawlerService
    {
        public async Task<List<string>> GetInternalLinksAsync(string domain)
        {
            var discoveredLinks = new ConcurrentBag<string>();
            var uri = new Uri(domain);

            var config = new CrawlConfiguration
            {
                MaxPagesToCrawl = 1000,
                MinCrawlDelayPerDomainMilliSeconds = 0,
                IsExternalPageCrawlingEnabled = false
            };

            using var crawler = new PoliteWebCrawler(config);

            crawler.PageCrawlCompleted += (obj, e) =>
            {
                string url = e.CrawledPage.Uri.AbsoluteUri;
                discoveredLinks.Add(url);
            };

            await crawler.CrawlAsync(uri);

            return discoveredLinks.Distinct().ToList();
        }
    }
}
