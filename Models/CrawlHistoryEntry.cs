using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ArchiveNumerique.Models
{
    /// <summary>
    /// Représente une entrée dans l'historique des crawls.
    /// Stocké dans %APPDATA%\ArchiveNumerique\history.json
    /// </summary>
    public class CrawlHistoryEntry
    {
        public string Url { get; set; } = "";
        public DateTime CrawledAt { get; set; }
        public List<string> Links { get; set; } = new();

        // Helpers d'affichage — exclus de la sérialisation JSON
        [JsonIgnore]
        public string DisplayDate => CrawledAt.ToString("dd/MM/yyyy HH:mm");

        [JsonIgnore]
        public string LinkCount => $"{Links.Count} lien(s)";
    }
}
