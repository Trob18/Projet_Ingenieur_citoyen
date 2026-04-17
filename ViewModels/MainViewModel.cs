using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArchiveNumerique.Services;

namespace ArchiveNumerique.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {
        private ObservableCollection<string> _links = new();
        public ObservableCollection<string> Links
        {
            get => _links;
            set => SetProperty(ref _links, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public async Task LoadLinksAsync(string url)
        {
            try
            {
                IsLoading = true;
                var crawlerService = new CrawlerService(maxPages: 500, delayMs: 200);
                var links = await crawlerService.GetInternalLinksAsync(url);

                Links.Clear();
                foreach (var link in links)
                {
                    Links.Add(link);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur crawl: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
