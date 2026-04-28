using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ArchiveNumerique.Models;

namespace ArchiveNumerique.Services
{
    /// <summary>
    /// Gère la persistance de l'historique des crawls.
    /// Stockage : %APPDATA%\ArchiveNumerique\history.json (norme Windows pour données applicatives)
    /// Limite : 50 dernières entrées — LIFO, la plus récente en tête.
    /// </summary>
    public sealed class HistoryService
    {
        private const int MaxEntries = 50;

        private static readonly string DataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ArchiveNumerique");

        private static readonly string FilePath =
            Path.Combine(DataFolder, "history.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // ─── Chargement ───────────────────────────────────────────────────────────

        public List<CrawlHistoryEntry> Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new List<CrawlHistoryEntry>();

                var json = File.ReadAllText(FilePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<CrawlHistoryEntry>>(json, JsonOpts)
                       ?? new List<CrawlHistoryEntry>();
            }
            catch
            {
                // Fichier corrompu : on repart de zéro sans crash
                return new List<CrawlHistoryEntry>();
            }
        }

        // ─── Ajout ────────────────────────────────────────────────────────────────

        public List<CrawlHistoryEntry> AddEntry(CrawlHistoryEntry entry)
        {
            var list = Load();
            list.Insert(0, entry);                                  // plus récent en premier
            if (list.Count > MaxEntries)
                list = list.Take(MaxEntries).ToList();
            Save(list);
            return list;
        }

        // ─── Sauvegarde ───────────────────────────────────────────────────────────

        private static void Save(List<CrawlHistoryEntry> entries)
        {
            Directory.CreateDirectory(DataFolder);
            var json = JsonSerializer.Serialize(entries, JsonOpts);
            // Écriture atomique : fichier temporaire puis remplacement
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json, Encoding.UTF8);
            File.Move(tmp, FilePath, overwrite: true);
        }

        // ─── Export TXT ───────────────────────────────────────────────────────────

        public static void ExportTxt(CrawlHistoryEntry entry, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Archive Numérique — Crawl du {entry.CrawledAt:dd/MM/yyyy à HH:mm:ss}");
            sb.AppendLine($"URL : {entry.Url}");
            sb.AppendLine($"Liens trouvés : {entry.Links.Count}");
            sb.AppendLine(new string('─', 60));
            sb.AppendLine();
            foreach (var link in entry.Links)
                sb.AppendLine(link);

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        // ─── Export CSV ───────────────────────────────────────────────────────────

        public static void ExportCsv(CrawlHistoryEntry entry, string filePath)
        {
            var sb = new StringBuilder();
            foreach (var link in entry.Links)
                sb.AppendLine(link);

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        // ─── Effacement de l'historique ────────────────────────────────────────────

        /// <summary>
        /// Efface complètement l'historique en supprimant le fichier.
        /// </summary>
        public void ClearHistory()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch
            {
                // Ignorer les erreurs silencieusement
            }
        }

    }
}
