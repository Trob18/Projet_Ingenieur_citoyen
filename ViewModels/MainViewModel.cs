using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArchiveNumerique.Models;
using ArchiveNumerique.Services;

namespace ArchiveNumerique.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {
        private readonly HistoryService _historyService = new();

        // ─── Propriétés du crawl en cours ─────────────────────────────────────────

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

        private string _serverStatus = "Serveur HTTP : http://127.0.0.1:5789";
        public string ServerStatus
        {
            get => _serverStatus;
            set => SetProperty(ref _serverStatus, value);
        }

        private string _currentUrl = "";
        public string CurrentUrl
        {
            get => _currentUrl;
            set => SetProperty(ref _currentUrl, value);
        }

        // ─── Historique ───────────────────────────────────────────────────────────

        private ObservableCollection<CrawlHistoryEntry> _historyEntries = new();
        public ObservableCollection<CrawlHistoryEntry> HistoryEntries
        {
            get => _historyEntries;
            set
            {
                if (SetProperty(ref _historyEntries, value))
                    OnPropertyChanged(nameof(HistoryTabHeader));
            }
        }

        private CrawlHistoryEntry? _selectedHistoryEntry;
        public CrawlHistoryEntry? SelectedHistoryEntry
        {
            get => _selectedHistoryEntry;
            set
            {
                _selectedHistoryEntry = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedHistoryLinks));
                OnPropertyChanged(nameof(HasSelectedHistory));
            }
        }

        public IReadOnlyList<string> SelectedHistoryLinks =>
            _selectedHistoryEntry?.Links ?? new List<string>();

        public bool HasSelectedHistory => _selectedHistoryEntry != null;

        public string HistoryTabHeader =>
            _historyEntries.Count == 0
                ? "Historique"
                : $"Historique ({_historyEntries.Count})";

        // ─── Constructeur ─────────────────────────────────────────────────────────

        public MainViewModel()
        {
            var history = _historyService.Load();
            _historyEntries = new ObservableCollection<CrawlHistoryEntry>(history);
        }

        // ─── Méthodes appelées par MainWindow ─────────────────────────────────────

        public void UpdateServerStatus(string message)
        {
            ServerStatus = message;
        }

        public void OnCrawlStarted(string url)
        {
            CurrentUrl = url;
            Links.Clear();
            IsLoading = true;
        }

        public void OnLinkFound(string link)
        {
            Links.Add(link);
        }

        public void OnCrawlComplete()
        {
            IsLoading = false;

            if (!string.IsNullOrEmpty(CurrentUrl) && Links.Count > 0)
            {
                var entry = new CrawlHistoryEntry
                {
                    Url = CurrentUrl,
                    CrawledAt = DateTime.Now,
                    Links = Links.ToList()
                };
                var updated = _historyService.AddEntry(entry);
                HistoryEntries = new ObservableCollection<CrawlHistoryEntry>(updated);
            }
        }

        // ─── Export (appelé depuis le code-behind après le SaveFileDialog) ────────

        public void ExportTxt(string filePath) =>
            HistoryService.ExportTxt(SelectedHistoryEntry!, filePath);

        public void ExportCsv(string filePath) =>
            HistoryService.ExportCsv(SelectedHistoryEntry!, filePath);

        // ─── Crawl manuel depuis l'interface ──────────────────────────────────────

        public async Task LoadLinksAsync(string url)
        {
            try
            {
                IsLoading = true;
                CurrentUrl = url;
                Links.Clear();
                var crawlerService = new CrawlerService(maxPages: 500, delayMs: 200);
                var links = await crawlerService.GetInternalLinksAsync(url,
                    onLinkFound: link => Links.Add(link));

                // Reconstruire la liste propre (sans doublons potentiels)
                var allLinks = links.Union(Links).ToList();
                Links = new ObservableCollection<string>(allLinks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur crawl: {ex.Message}");
                throw;
            }
            finally
            {
                OnCrawlComplete();
            }
        }
    }
}
