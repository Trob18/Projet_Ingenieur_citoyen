"use strict";

let extractedLinks = new Set();
let currentHost = "";

// --- Éléments du DOM ---
const resultArea = document.getElementById("result");
const statusEl   = document.getElementById("status");
const inputUrl   = document.getElementById("inputUrl");
const btnExtract = document.getElementById("btnExtract");
const btnCopy    = document.getElementById("btnCopy");
const btnDownload = document.getElementById("btnDownload");

// --- Utilitaires ---

async function getTargetOrigin() {
  let manualUrl = inputUrl.value.trim();
  if (manualUrl) {
    // Ajouter https:// si aucun schéma n'est précisé
    if (!/^https?:\/\//i.test(manualUrl)) {
      manualUrl = "https://" + manualUrl;
    }
    const url = new URL(manualUrl);
    currentHost = url.hostname;
    return url.origin;
  }
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  const url = new URL(tab.url);
  currentHost = url.hostname;
  return url.origin;
}

function cleanAndFilterUrl(link) {
  try {
    const urlObj = new URL(link);
    if (urlObj.hostname !== currentHost) return null;
    urlObj.search = "";
    urlObj.hash = "";
    return urlObj.href;
  } catch {
    return null;
  }
}

/**
 * Parcourt un sitemap XML (et les éventuels index de sitemaps imbriqués).
 */
async function parseSitemap(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error("Sitemap introuvable : " + url);
  const text = await res.text();

  const sitemapRefs = [...text.matchAll(/<sitemap>\s*<loc>(.*?)<\/loc>/gs)];
  if (sitemapRefs.length > 0) {
    for (const match of sitemapRefs) {
      await parseSitemap(match[1].trim());
    }
    return;
  }

  const matches = [...text.matchAll(/<loc>(.*?)<\/loc>/g)];
  for (const match of matches) {
    const validUrl = cleanAndFilterUrl(match[1].trim());
    if (validUrl) extractedLinks.add(validUrl);
  }
}

function setStatus(message) {
  statusEl.textContent = message;
}

function updateUI() {
  resultArea.value = Array.from(extractedLinks).join("\n");
  setStatus(extractedLinks.size + " lien(s) trouvé(s).");
}

function setButtonsDisabled(disabled) {
  btnExtract.disabled = disabled;
}

// --- Bouton principal ---
btnExtract.addEventListener("click", async () => {
  extractedLinks.clear();
  resultArea.value = "";
  setStatus("Recherche du sitemap…");
  setButtonsDisabled(true);

  let origin;
  try {
    origin = await getTargetOrigin();
  } catch {
    setStatus("Erreur : URL invalide.");
    setButtonsDisabled(false);
    return;
  }

  // Tentative sitemap
  let sitemapOk = false;
  try {
    await parseSitemap(origin + "/sitemap.xml");
    sitemapOk = extractedLinks.size > 0;
  } catch {
    // sitemap indisponible
  }

  if (sitemapOk) {
    updateUI();
    setStatus("✓ Liens récupérés via Sitemap ! Synchronisation avec l'application…");
    
    // Quand même contacter le serveur pour que l'application enregistre l'historique
    try {
      const ping = await fetch("http://127.0.0.1:5789/ping", {
        signal: AbortSignal.timeout(2000)
      });
      
      if (ping.ok) {
        const links = Array.from(extractedLinks);
        const res = await fetch("http://127.0.0.1:5789/crawl", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ url: origin, presyncedLinks: links }),
          signal: AbortSignal.timeout(10000)
        });
        
        if (res.ok) {
          setStatus("✓ Liens synchronisés avec l'application !");
        }
      }
    } catch (err) {
      console.log("[Extension] Synchronisation avec l'application échouée (non critique):", err);
      // Ce n'est pas grave, on a quand même les liens à afficher
    }
    
    setButtonsDisabled(false);
    return;
  }

  // Fallback : contacter l'application locale via le serveur HTTP
  setStatus("Sitemap introuvable, contact de l'application…");

  let appLinks = null;
  try {
    // Vérifier que l'application est démarrée
    console.log("[Extension] Vérification du serveur sur localhost:5789");
    const ping = await fetch("http://127.0.0.1:5789/ping", {
      signal: AbortSignal.timeout(2000)
    });

    console.log("[Extension] Réponse ping:", ping.status);
    
    if (ping.ok) {
      setStatus("Application trouvée, crawl en cours (peut prendre quelques minutes)…");
      console.log("[Extension] Envoi de la requête crawl:", { url: origin });
      
      const res = await fetch("http://127.0.0.1:5789/crawl", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ url: origin }),
        signal: AbortSignal.timeout(300000) // 5 minutes max
      });

      console.log("[Extension] Réponse crawl:", res.status);
      const data = await res.json();
      console.log("[Extension] Données reçues:", data);
      appLinks = data.links ?? null;
    }
  } catch (err) {
    console.error("[Extension] Erreur lors du contact du serveur:", err);
  }

  if (appLinks && appLinks.length > 0) {
    appLinks.forEach(l => extractedLinks.add(l));
    updateUI();
    setStatus("✓ Liens récupérés via l'application !");
  } else if (appLinks !== null) {
    setStatus("L'application n'a retourné aucun lien.");
  } else {
    setStatus("⚠ Application non démarrée. Lancez ArchiveNumerique.exe puis réessayez.");
  }
  setButtonsDisabled(false);
});

// --- Export : Presse-papier ---
btnCopy.addEventListener("click", async () => {
  const text = resultArea.value;
  if (!text) { setStatus("Rien à copier."); return; }
  await navigator.clipboard.writeText(text);
  setStatus("Copié dans le presse-papier !");
});

// --- Export : Fichier TXT ---
btnDownload.addEventListener("click", () => {
  const text = resultArea.value;
  if (!text) { setStatus("Rien à télécharger."); return; }
  const blob = new Blob([text], { type: "text/plain" });
  const url  = URL.createObjectURL(blob);
  chrome.downloads.download({
    url,
    filename: `Exportation lien - ${currentHost}.txt`,
    saveAs: true,
  });
});