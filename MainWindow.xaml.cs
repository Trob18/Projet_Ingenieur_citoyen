using System;
using System.Windows;
using ArchiveNumerique.ViewModels;

namespace ArchiveNumerique
{
    /// <summary>
    /// Interface temporaire pour tester le crawler.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }

        private async void CrawlButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Veuillez entrer une URL.", "Attention");
                return;
            }

            // UI : désactiver le bouton et afficher le chargement
            CrawlButton.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Visible;
            StatusText.Text = $"Crawl en cours sur {url}...";

            try
            {
                await _viewModel.LoadLinksAsync(url);
                StatusText.Text = $"Terminé — {_viewModel.Links.Count} lien(s) trouvé(s).";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erreur : {ex.Message}";
                MessageBox.Show(ex.Message, "Erreur");
            }
            finally
            {
                CrawlButton.IsEnabled = true;
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }
    }
}