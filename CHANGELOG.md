# 📜 Journal des modifications (Changelog) - VencordEX

Toutes les modifications notables apportées au projet **VencordEX** sont documentées dans ce fichier.

Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/),
et ce projet adhère au [Semantic Versioning](https://semver.org/lang/fr/).

---

## [1.1.0] - 2026-09-03

### ✨ Ajouté
- **Système de mise à jour automatique GitHub** :
  - Détection automatique et manuelle des nouvelles versions publiées sur le dépôt GitHub [t12lve/VencordEX](https://github.com/t12lve/VencordEX/releases).
  - Intégration dans le Centre de Contrôle (`VencordEXSettings.exe`) avec badge de version en temps réel (`v1.1.0`), bouton d'état « Vérifier MàJ » / « ⚡ MàJ vX.X.X » et journal d'activité.
  - Téléchargement sécurisé du nouvel exécutable depuis les assets de release GitHub.
  - Remplacement automatique à chaud (self-update) avec script relais asynchrone gérant la fermeture, le remplacement de `VencordEX.exe` et `VencordEXSettings.exe`, puis la relance automatique.
  - Gestion automatique des droits d'élévation UAC (`runas`) si VencordEX est installé dans `C:\Program Files\VencordEX`.

### 🛡️ Corrigé
- **Correctif critique de démarrage Discord (Crash Rust `EnvironmentNotInitialized`)** :
  - Résolution du blocage survenu lors de la mise à jour de Discord vers la version **1.0.9256** où Discord crashait silencieusement après l'application du patch.
  - Ajout de la routine `EnsureDiscordFirstRunMarker()` générant automatiquement le fichier `.first-run` dans `%APPDATA%\discord\<version>`, empêchant le panic interne de l'updater Rust de Discord (`discord_common\rust\napi\src\class.rs`).
  - Amélioration de la méthode `LaunchDiscord()` pour prioriser le point d'entrée officiel `Update.exe --processStart Discord.exe` assurant une initialisation complète du runtime Squirrel, avec fallback sur l'exécutable direct.
  - Sécurisation équivalente intégrée dans le script de secours [launch-discord.ps1](file:///f:/DBXSSD/Dropbox/FOUTOIR/vibecoding/vencordEX/Vencord/auto-patcher/launch-discord.ps1).

---

## [1.0.0] - 2026-08-30

### 🚀 Version Initiale (Suite VencordEX)
- **Lanceur Autonome & Auto-Patch** :
  - Détection automatique des mises à jour de Discord créant de nouveaux répertoires `app-1.0.xxxx`.
  - Injection silencieuse et instantanée de Vencord via `VencordInstallerCli.exe` en arrière-plan.
  - Démarrage instantané (< 20 ms) et arrêt immédiat du processus lorsque Discord est déjà patché.
- **Préchargeur Fluide (Motion Preloader)** :
  - Interface WPF moderne sombre/glassmorphism avec dégradé Discord (#5865F2 vers #10B981).
  - Compteur textuel dynamique `00%` à `100%` et barre de progression animée.
- **Dialogue Intelligent de Conflit** :
  - Prise en charge des situations où Discord est déjà ouvert lors d'une détection de mise à jour (évite de couper un appel vocal en cours).
  - Choix entre redémarrage immédiat ou application différée au prochain lancement.
- **Centre de Contrôle Dédié (`VencordEXSettings.exe` / `--manager`)** :
  - Bento Grid affichant en temps réel la version de Discord, le statut Vencord, OpenAsar et la protection des raccourcis.
  - Gestion en 1 clic des raccourcis Windows (Menu Démarrer, dossier `Discord Inc`, Barre des tâches, Bureau).
  - Gestionnaire OpenAsar (installation / désinstallation en 1 clic).
  - Module de sauvegarde & restauration des préférences, plugins configurés et thèmes dans des archives ZIP horodatées.
  - Option de restauration Discord originale (nettoyage complet des patchs).
- **Installeur Windows Complet (`VencordEX-Setup.exe`)** :
  - Installation propre dans `C:\Program Files\VencordEX`.
  - Enregistrement dans « Applications et fonctionnalités » de Windows avec désinstallation automatisée.
- **Documentation & Portail Web** :
  - Guide interactif `Mode-Operatoire.html` avec simulateur d'animations.
  - Déploiement GitHub Pages sur [t12lve.github.io/VencordEX](https://t12lve.github.io/VencordEX/).
