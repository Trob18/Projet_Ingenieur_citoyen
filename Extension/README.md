# Archive Numérique — Extracteur de liens internes

Projet citoyen composé de deux parties :

- **L'application de bureau** (`ArchiveNumerique.exe`) — crawle un site web et conserve un historique des liens trouvés.
- **L'extension navigateur** (`Extension/`) — extrait les liens depuis l'onglet actif ou une URL saisie, en s'appuyant sur le sitemap ou, en fallback, sur l'application.

---

## Prérequis

- Windows 10 / 11
- Chrome ou Edge (pour l'extension)

> **.NET 8 Runtime est inclus** dans l'exécutable fourni — aucune installation supplémentaire requise.

---

## 1. Installer et lancer l'application

1. Télécharger le fichier `ArchiveNumerique.zip`.
2. Extraire le .zip dans un dossier (ex. `C:\ArchiveNumerique\`).
3. Double-cliquer sur `ArchiveNumerique.exe` dans le dossier extrait.

> L'application démarre un serveur HTTP local sur **http://127.0.0.1:5789** utilisé par l'extension.
>
> **Note :** Le dossier `Extension/` contenu dans le .zip sera utilisé à l'étape 2.

---

## 2. Installer l'extension navigateur

L'extension n'est pas publiée sur le Chrome Web Store. Elle s'installe en mode développeur depuis le dossier `Extension/` présent dans le .zip.

### Chrome

1. Ouvrir `chrome://extensions/`.
2. Activer le **Mode développeur** (interrupteur en haut à droite).
3. Cliquer sur **Charger l'extension non empaquetée**.
4. Sélectionner le dossier `Extension/` du répertoire extrait (ex. `C:\ArchiveNumerique\Extension\`).
5. L'icône **Extracteur de Liens** apparaît dans la barre d'outils.

### Edge

1. Ouvrir `edge://extensions/`.
2. Activer le **Mode développeur** (barre latérale gauche).
3. Cliquer sur **Charger l'extension décompressée**.
4. Sélectionner le dossier `Extension/` du répertoire extrait (ex. `C:\ArchiveNumerique\Extension\`).

---

## 3. Utiliser l'extension

1. Naviguer vers le site web dont vous souhaitez extraire les liens **OU** saisir une URL dans le champ **URL cible**.
2. Cliquer sur **Récupérer les liens**.

### Ce qui se passe en coulisse

| Étape | Comportement |
|-------|-------------|
| **1 — Sitemap** | L'extension télécharge `https://site.com/sitemap.xml` et en extrait toutes les URL internes. |
| **2 — Fallback application** | Si aucun sitemap n'est trouvé, l'extension contacte l'application locale (`http://127.0.0.1:5789/crawl`) qui effectue un crawl BFS complet du site en respectant `robots.txt`. |
| **3 — Erreur** | Si l'application n'est pas démarrée et qu'il n'y a pas de sitemap, un message d'avertissement s'affiche. |

> **Important :** pour utiliser le mode fallback (crawl), l'application `ArchiveNumerique.exe` **doit être démarrée avant** de cliquer sur le bouton.

### Exporter les résultats

Une fois les liens affichés :

- **Copier** — copie tous les liens dans le presse-papiers.
- **Télécharger .txt** — enregistre les liens dans un fichier texte.

---

## 4. Utiliser l'application

### Crawl manuel

1. Saisir une URL dans le champ en haut (ex. `https://exemple.com`).
2. Cliquer sur **Crawl**.
3. Les liens apparaissent en temps réel. La barre de progression indique que le crawl est en cours.
4. Cliquer sur **Stop** pour interrompre un crawl.

### Historique

L'onglet **Historique** liste les 50 derniers crawls effectués (via l'application ou l'extension).

- Cliquer sur une entrée pour afficher les liens associés.
- **Exporter en TXT / CSV** — enregistre les liens de l'entrée sélectionnée.
- **Effacer l'historique** — supprime toutes les entrées après confirmation.

> L'historique est stocké dans `%APPDATA%\ArchiveNumerique\history.json`.
