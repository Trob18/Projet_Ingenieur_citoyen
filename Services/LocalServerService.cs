using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArchiveNumerique.Services
{
    /// <summary>
    /// Serveur HTTP local (localhost:5789) utilisé par l'extension navigateur.
    /// Fonctionne avec Chrome, Edge, Firefox ou tout autre navigateur.
    /// </summary>
    public sealed class LocalServerService
    {
        public const int Port = 5789;

        private readonly HttpListener _listener = new();

        // Événements pour notifier l'interface WPF
        public event Action<string>? OnServerMessage;
        public event Action<string>? OnLinkFound;
        public event Action<string>? OnCrawlStarted;
        public event Action? OnCrawlComplete;

        // Pour pouvoir arrêter le crawl en cours
        public CancellationTokenSource? CurrentCrawlCts { get; set; }

        public LocalServerService()
        {
            // Écouter sur localhost ET 127.0.0.1 pour maximiser la compatibilité
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        }

        public async Task RunAsync(CancellationToken ct)
        {
            try
            {
                _listener.Start();
                var msg = $"[LocalServer] Démarré sur http://127.0.0.1:{Port}";
                System.Diagnostics.Debug.WriteLine(msg);
                OnServerMessage?.Invoke(msg);
            }
            catch (HttpListenerException ex)
            {
                var msg = $"[LocalServer] ERREUR : port {Port} déjà utilisé ou inaccessible. {ex.Message}";
                System.Diagnostics.Debug.WriteLine(msg);
                OnServerMessage?.Invoke(msg);
                return;
            }

            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync().WaitAsync(ct);
                }
                catch
                {
                    break;
                }

                _ = HandleAsync(ctx);
            }

            _listener.Stop();
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            try
            {
                var method = ctx.Request.HttpMethod;
                var path = ctx.Request.Url?.AbsolutePath ?? "";

                OnServerMessage?.Invoke($"[Extension] {method} {path}");

                // En-têtes CORS
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                ctx.Response.ContentType = "application/json; charset=utf-8";

                if (method == "OPTIONS")
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.Close();
                    return;
                }

                // GET /ping
                if (path == "/ping" && method == "GET")
                {
                    OnServerMessage?.Invoke("[Extension] PING reçu - application OK");
                    await WriteJsonAsync(ctx, "{\"status\":\"ok\"}");
                    return;
                }

                // POST /crawl
                if (path == "/crawl" && method == "POST")
                {
                    OnServerMessage?.Invoke("[Extension] Requête CRAWL reçue - crawl en cours...");

                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();

                    var req = JsonSerializer.Deserialize<CrawlRequest>(
                        body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (req == null || string.IsNullOrWhiteSpace(req.Url))
                    {
                        OnServerMessage?.Invoke("[Extension] ERREUR : URL vide");
                        ctx.Response.StatusCode = 400;
                        ctx.Response.Close();
                        return;
                    }

                    // Signal au UI que le crawl commence
                    OnCrawlStarted?.Invoke(req.Url);

                    // Créer un CTS pour ce crawl (peut être annulé)
                    var cts = new CancellationTokenSource();
                    CurrentCrawlCts = cts;

                    try
                    {
                        // Vérifier si les liens ont déjà été trouvés par l'extension (sitemap)
                        if (req.PresyncedLinks != null && req.PresyncedLinks.Count > 0)
                        {
                            OnServerMessage?.Invoke($"[Extension] Liens pré-synchronisés - {req.PresyncedLinks.Count} liens détectés");
                            // Notifier chaque lien trouvé
                            foreach (var link in req.PresyncedLinks)
                            {
                                OnLinkFound?.Invoke(link);
                            }
                            OnServerMessage?.Invoke($"[Extension] Pré-synchronisation terminée - {req.PresyncedLinks.Count} liens enregistrés");
                            OnCrawlComplete?.Invoke();

                            var response = JsonSerializer.Serialize(new { links = req.PresyncedLinks });
                            await WriteJsonAsync(ctx, response);
                        }
                        else
                        {
                            // Crawl complet
                            OnServerMessage?.Invoke($"[Extension] Crawling: {req.Url}");

                            var crawler = new CrawlerService(maxPages: 500, delayMs: 200);
                            
                            // Callback pour chaque lien trouvé
                            Action<string> onLinkFound = link => OnLinkFound?.Invoke(link);
                            
                            var links = await crawler.GetInternalLinksAsync(req.Url, onLinkFound, cts.Token);

                            OnServerMessage?.Invoke($"[Extension] Crawl terminé - {links.Count} liens trouvés");
                            OnCrawlComplete?.Invoke();

                            var response = JsonSerializer.Serialize(new { links });
                            await WriteJsonAsync(ctx, response);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        OnServerMessage?.Invoke("[Extension] Crawl arrêté par l'utilisateur");
                        OnCrawlComplete?.Invoke();
                        // Retourner les liens collectés jusqu'ici (déjà notifiés via OnLinkFound)
                        ctx.Response.StatusCode = 200;
                        var partialResponse = JsonSerializer.Serialize(new { links = Array.Empty<string>(), stopped = true });
                        await WriteJsonAsync(ctx, partialResponse);
                    }
                    finally
                    {
                        cts.Dispose();
                        CurrentCrawlCts = null;
                    }
                    return;
                }

                OnServerMessage?.Invoke($"[Extension] Endpoint inconnu: {path} (404)");
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                OnServerMessage?.Invoke($"[Extension] EXCEPTION: {ex.Message}");
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            }
        }

        private static async Task WriteJsonAsync(HttpListenerContext ctx, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }

        private sealed class CrawlRequest
        {
            public string Url { get; set; } = "";
            public List<string>? PresyncedLinks { get; set; } // Liens pré-synchronisés par l'extension (sitemap)
        }
    }
}
