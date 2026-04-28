using System.Threading;
using System.Windows;
using ArchiveNumerique.Services;
using ArchiveNumerique.ViewModels;

namespace ArchiveNumerique
{
    public partial class App : Application
    {
        private readonly CancellationTokenSource _cts = new();
        public static LocalServerService? Server { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Démarrer le serveur HTTP local en arrière-plan
            Server = new LocalServerService();
            _ = Server.RunAsync(_cts.Token);

            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _cts.Cancel();
            base.OnExit(e);
        }
    }
}
