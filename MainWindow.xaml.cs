using System;
using System.Windows;
using System.Windows.Controls;
using ArchiveNumerique.ViewModels;
using Microsoft.Win32;

namespace ArchiveNumerique
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // Gerer la visibilite de la ProgressBar
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsLoading))
                {
                    LoadingProgress.Visibility = _viewModel.IsLoading
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            };

            // S'abonner aux evenements du serveur HTTP
            if (App.Server != null)
            {
                App.Server.OnServerMessage += msg => 
                    Dispatcher.Invoke(() => _viewModel.UpdateServerStatus(msg));

                App.Server.OnCrawlStarted += url => 
                    Dispatcher.Invoke(() => _viewModel.OnCrawlStarted(url));

                App.Server.OnLinkFound += link => 
                    Dispatcher.Invoke(() => _viewModel.OnLinkFound(link));

                App.Server.OnCrawlComplete += () => 
                    Dispatcher.Invoke(() => _viewModel.OnCrawlComplete());
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StopCrawl();
            App.Server?.CurrentCrawlCts?.Cancel();
        }

        private async void CrawlButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Veuillez entrer une URL.", "Attention");
                return;
            }

            CrawlButton.IsEnabled = false;
            StatusText.Text = $"Crawl en cours sur {url}...";

            try
            {
                await _viewModel.LoadLinksAsync(url);
                StatusText.Text = $"Terminé — {_viewModel.Links.Count} lien(s) trouvé(s).";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = $"Arrêté — {_viewModel.Links.Count} lien(s) trouvé(s) avant arrêt.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erreur : {ex.Message}";
                MessageBox.Show(ex.Message, "Erreur");
            }
            finally
            {
                CrawlButton.IsEnabled = true;
            }
        }

        // ─── Export ───────────────────────────────────────────────────────────────

        private void ExportTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedHistoryEntry == null) return;

            var domainName = GetDomainName(_viewModel.SelectedHistoryEntry.Url);
            var dlg = new SaveFileDialog
            {
                Title = "Exporter en TXT",
                Filter = "Fichier texte (*.txt)|*.txt",
                FileName = $"Exportation lien - {domainName}.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                _viewModel.ExportTxt(dlg.FileName);
                StatusText.Text = $"Exporté : {dlg.FileName}";
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedHistoryEntry == null) return;

            var domainName = GetDomainName(_viewModel.SelectedHistoryEntry.Url);
            var dlg = new SaveFileDialog
            {
                Title = "Exporter en CSV",
                Filter = "Fichier CSV (*.csv)|*.csv",
                FileName = $"Exportation lien - {domainName}.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                _viewModel.ExportCsv(dlg.FileName);
                StatusText.Text = $"Exporté : {dlg.FileName}";
            }
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Êtes-vous sûr de vouloir effacer complètement l'historique ?",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ClearAllHistory();
                StatusText.Text = "Historique effacé.";
            }
        }

        private static string GetDomainName(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return uri.Host;
            return "export";
        }

        private void LinkContextMenu_Opening(object sender, EventArgs e)
        {
            // Juste pour garder le menu contextuel actif
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
            {
                // Récupérer le TextBlock (propriétaire du ContextMenu)
                var textBlock = contextMenu.PlacementTarget as TextBlock;
                if (textBlock != null)
                {
                    var text = textBlock.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        Clipboard.SetText(text);
                        StatusText.Text = "Lien copié !";
                    }
                }
            }
        }
    }
}
