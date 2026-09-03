# 🚀 VencordEX • Discord Auto-Patcher & Suite Standalone

<p align="center">
  <img src="https://raw.githubusercontent.com/Vendicated/Vencord/main/src/assets/icon.png" width="100" height="100" alt="Vencord Logo"/>
</p>

<p align="center">
  <b>Le lanceur autonome intelligent qui maintient Vencord injecté après chaque mise à jour de Discord.</b><br>
  <i>Avec préchargeur fluide ultra-rapide, Centre de Contrôle GUI et installeur Windows Program Files.</i>
</p>

<p align="center">
  <a href="https://t12lve.github.io/VencordEX/">🌐 Guide interactif (GitHub Pages)</a> •
  <a href="CHANGELOG.md">📜 Changelog</a> •
  <a href="#-politique-de-lapplication--éthique">🛡️ Politique & Éthique</a> •
  <a href="#-crédits-et-hommage-à-vencord">💖 Crédits à Vencord</a> •
  <a href="#-fonctionnalités">✨ Fonctionnalités</a> •
  <a href="#-téléchargements--installation">📦 Installation</a>
</p>

---

## 📜 Politique de l'Application & Éthique

### 🎯 La Raison d'être de VencordEX
Sous Windows, chaque mise à jour automatique de Discord (gérée par *Squirrel Updater*) recrée un nouveau répertoire `app-1.0.xxxx` contenant le fichier `app.asar` d'origine non patché. Résultat : Vencord "disparaît" silencieusement après une mise à jour, obligeant l'utilisateur à réexécuter manuellement l'installateur.

**VencordEX a été conçu pour éliminer définitivement cette corvée.**

### 🛡️ Nos Engagements & Principes :
1. **Compagnon, pas un remplacement** :  
   VencordEX n'est **PAS** un fork divergent du code de Vencord. C'est un wrapper / gardien de démarrage léger (~1 Mo) sous Windows qui veille à ce que l'injection officielle de Vencord reste active au fil des mises à jour de Discord.
2. **Utilisation des binaires officiels** :  
   VencordEX télécharge et utilise exclusivement l'utilitaire officiel [`VencordInstallerCli`](https://github.com/Vencord/Installer) et les distributions officielles de Vencord hébergées sur GitHub.
3. **Zéro Télémétrie, Zéro Espionnage, 100% Local** :  
   Aucune donnée personnelle n'est collectée, traquée ou transmise. Le code source est intégralement public et transparent.
4. **Performance Absolue (Zéro ralentissement)** :  
   Lorsque Discord est déjà patché (99% du temps), VencordEX lance Discord en moins de **20 millisecondes** et quitte immédiatement la mémoire vive. Aucune fenêtre ne s'affiche et aucun processus ne tourne en tâche de fond.

---

## 💖 Crédits et Hommage à Vencord

> ### 👑 Tout le mérite revient à l'équipe de Vencord !
> 
> **VencordEX ne serait rien sans le travail remarquable de [Vendicated](https://github.com/Vendicated) et de l'ensemble des contributeurs du projet [Vencord](https://github.com/Vendicated/Vencord).**
>
> Vencord est sans conteste le client mod Discord le plus élégant, rapide, léger et sécurisé jamais créé. Les plus de 100 plugins intégrés, l'isolation sandboxée, la compatibilité cross-plateforme et le support instantané des mises à jour Discord sont l'œuvre de leur talent.
>
> 🔗 **Ressources officielles de Vencord :**
> - **Site officiel** : [vencord.dev](https://vencord.dev)
> - **Dépôt GitHub** : [github.com/Vendicated/Vencord](https://github.com/Vendicated/Vencord)
> - **Communauté Discord** : [discord.gg/D9uwnFnqmd](https://discord.gg/D9uwnFnqmd)
>
> *Si vous appréciez Vencord, n'hésitez pas à soutenir le projet officiel et à leur donner une étoile sur GitHub !*

---

## ✨ Fonctionnalités de VencordEX

- ⚡ **Auto-Patch Silencieux & Résilience Discord** : Détecte au lancement si Discord a été mis à jour et réinjecte Vencord avant que Discord n'apparaisse. Sécurise le démarrage contre les crashs internes du runtime Discord (`EnvironmentNotInitialized`).
- 🔄 **Mise à jour Automatique GitHub** : Le Centre de Contrôle interroge directement les releases de [t12lve/VencordEX](https://github.com/t12lve/VencordEX) et permet la mise à jour à chaud de VencordEX en 1 clic sans re-téléchargement manuel.
- 🎨 **Préchargeur Fluide (Motion Preloader)** : Un écran de chargement moderne et soigné avec compteur `00%` ➔ `100%`, dégradé dynamique et signature :  
  `t12lve le vibecodeur de l'extreme`.
- 🛡️ **Dialogue Intelligent (Discord déjà ouvert)** :  
  Si Discord est déjà en cours d'utilisation lors d'une détection de mise à jour, VencordEX ne coupe jamais votre appel vocal sans prévenir. Une boîte de dialogue vous propose :
  - **⚡ Oui, redémarrer** : Ferme proprement la session, patche et relance Discord.
  - **⏱ Au prochain lancement** : Patche en tâche de fond et conserve votre appel / session active sans interruption.
- 🎛️ **Centre de Contrôle Dédié (`VencordEXSettings.exe`)** :
  - **🔄 Mise à Jour en 1 clic** : Indicateur de version en direct (`v1.1.0`), vérification instantanée et auto-swap sécurisé.
  - **🔗 Gestion des Raccourcis** : Patcher ou restaurer les raccourcis Windows (Menu Démarrer, dossier `Discord Inc`, Barre des tâches, Bureau).
  - **⚡ Intégration d'OpenAsar** : Installer ou retirer OpenAsar en 1 clic pour optimiser les performances de Discord.
  - **🛡️ Restauration Discord** : Dépatcher Discord à 100% pour revenir à la version officielle originale.
  - **💾 Sauvegarde & Restauration de vos Préférences** : Exporte vos plugins activés (`settings.json`), votre CSS rapide (`quickCss.css`) et tous vos thèmes (`.theme.css`) dans une archive ZIP horodatée réimportable en 1 clic.
- 📦 **Véritable Installeur Windows (`VencordEX-Setup.exe`)** :  
  Installe VencordEX dans `C:\Program Files\VencordEX` avec élévation Administrateur et enregistrement dans **« Applications et fonctionnalités »** de Windows pour une désinstallation propre.

---

## 📦 Téléchargements & Utilisation

Téléchargez la dernière version dans le dossier [`prod/`](prod/) :

| Fichier | Utilisation |
| :--- | :--- |
| **`VencordEX-Setup.exe`** | **Recommandé** : L'installeur Windows officiel. Double-cliquez pour installer dans `C:\Program Files\VencordEX`. |
| **`VencordEX.exe`** | Version portable autonome : lance et auto-patche Discord sans installation. |
| **`VencordEXSettings.exe`** | Version portable : ouvre directement le Centre de Contrôle et les paramètres. |
| **`Mode-Operatoire.html`** | Le guide interactif hors-ligne avec simulateur web de préchargeur. |

---

## 🛠️ Compilation depuis les sources

Prérequis : [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

```powershell
# 1. Cloner le dépôt
git clone https://github.com/t12lve/VencordEX.git
cd VencordEX

# 2. Compiler VencordEX
dotnet publish "VencordLauncherApp" -c Release -o "prod"

# 3. Compiler l'installeur Windows
dotnet publish "VencordSetupApp" -c Release -o "prod"
```

---

<p align="center">
  Développé avec passion par <b>t12lve le vibecodeur de l'extreme</b>.<br>
  Basé sur le travail exceptionnel de <b>Vendicated & la communauté Vencord</b>.
</p>
