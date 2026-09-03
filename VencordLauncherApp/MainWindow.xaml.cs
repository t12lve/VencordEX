using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace VencordLauncher
{
    public partial class MainWindow : Window
    {
        public static readonly Version CurrentVersion = new Version(1, 1, 0);
        public const string GitHubRepo = "t12lve/VencordEX";

        private bool _isTestMode = false;
        private bool _forcePatch = false;
        private bool _isManagerMode = false;
        private string[] _passedArgs = Array.Empty<string>();

        private string _discordDir = string.Empty;
        private string _vencordDir = string.Empty;
        private string _cliPath = string.Empty;
        private string? _latestAppDir = null;

        private GitHubReleaseInfo? _latestAvailableUpdate = null;
        private bool _isCheckingUpdate = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var pulseStory = (Storyboard)FindResource("PulseStoryboard");
                pulseStory.Begin();
            }
            catch { }

            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

            if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                PerformUninstall();
                return;
            }

            string currentExeName = Path.GetFileNameWithoutExtension(
                Process.GetCurrentProcess().MainModule?.FileName ?? AppDomain.CurrentDomain.FriendlyName);

            _isManagerMode = currentExeName.Contains("settings", StringComparison.OrdinalIgnoreCase) ||
                             args.Any(a => a.Equals("--gui", StringComparison.OrdinalIgnoreCase) ||
                                           a.Equals("--manager", StringComparison.OrdinalIgnoreCase) ||
                                           a.Equals("--settings", StringComparison.OrdinalIgnoreCase)) ||
                             Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            _isTestMode = args.Any(a => a.Equals("--test", StringComparison.OrdinalIgnoreCase) || 
                                        a.Equals("--preview", StringComparison.OrdinalIgnoreCase)) ||
                          Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            _forcePatch = args.Any(a => a.Equals("--force", StringComparison.OrdinalIgnoreCase) || 
                                        a.Equals("--repair", StringComparison.OrdinalIgnoreCase));

            _passedArgs = args.Where(a => !a.StartsWith("--test", StringComparison.OrdinalIgnoreCase) &&
                                          !a.StartsWith("--preview", StringComparison.OrdinalIgnoreCase) &&
                                          !a.StartsWith("--force", StringComparison.OrdinalIgnoreCase) &&
                                          !a.StartsWith("--repair", StringComparison.OrdinalIgnoreCase) &&
                                          !a.StartsWith("--gui", StringComparison.OrdinalIgnoreCase) &&
                                          !a.StartsWith("--manager", StringComparison.OrdinalIgnoreCase) &&
                                          !a.StartsWith("--settings", StringComparison.OrdinalIgnoreCase) &&
                                          !a.StartsWith("--test-confirm", StringComparison.OrdinalIgnoreCase)).ToArray();

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _discordDir = Path.Combine(localAppData, "Discord");
            _vencordDir = Path.Combine(appData, "Vencord");
            _cliPath = Path.Combine(_vencordDir, "VencordInstallerCli.exe");

            DetectDiscordVersion();

            // Si lancé avec --manager ou en maintenant Ctrl -> Afficher le Centre de Contrôle immédiatement
            if (_isManagerMode)
            {
                ShowManagerView();
                return;
            }

            bool needsPatch = false;
            bool isDiscordRunning = Process.GetProcessesByName("Discord").Length > 0;

            if (_latestAppDir != null)
            {
                string resourcesDir = Path.Combine(_latestAppDir, "resources");
                string appAsar = Path.Combine(resourcesDir, "app.asar");
                string patchedIndicator = Path.Combine(resourcesDir, "_app.asar");

                if (File.Exists(appAsar) && !File.Exists(patchedIndicator))
                {
                    needsPatch = true;
                }
            }

            if (_forcePatch)
            {
                needsPatch = true;
            }

            // Mode test preview
            if (_isTestMode)
            {
                if (args.Any(a => a.Equals("--test-confirm", StringComparison.OrdinalIgnoreCase)))
                {
                    ShowConfirmDialog();
                    return;
                }

                PreloaderGrid.Visibility = Visibility.Visible;
                ConfirmGrid.Visibility = Visibility.Collapsed;
                ManagerGrid.Visibility = Visibility.Collapsed;
                await RunMotionPreloaderAsync(launchDiscordAfter: false);
                return;
            }

            // CAS 1 : Discord est déjà patché -> lancement instantané
            if (!needsPatch && _latestAppDir != null)
            {
                LaunchDiscord();
                await ExitAppAsync();
                return;
            }

            // CAS 2 : Discord a besoin d'être patché ET Discord est ACTUELLEMENT OUVERT
            if (needsPatch && isDiscordRunning)
            {
                ShowConfirmDialog();
                return;
            }

            // CAS 3 : Discord a besoin d'être patché ET Discord est FERMÉ
            PreloaderGrid.Visibility = Visibility.Visible;
            ConfirmGrid.Visibility = Visibility.Collapsed;
            ManagerGrid.Visibility = Visibility.Collapsed;
            await RunMotionPreloaderAsync(launchDiscordAfter: true);
        }

        private void DetectDiscordVersion()
        {
            try
            {
                if (Directory.Exists(_discordDir))
                {
                    var appDirs = Directory.GetDirectories(_discordDir, "app-*");
                    _latestAppDir = appDirs
                        .OrderByDescending(d =>
                        {
                            var name = Path.GetFileName(d).Replace("app-", "");
                            return Version.TryParse(name, out var v) ? v : new Version(0, 0, 0);
                        })
                        .FirstOrDefault();

                    if (_latestAppDir != null)
                    {
                        EnsureDiscordFirstRunMarker(_latestAppDir);
                    }
                }
            }
            catch { }
        }

        private void EnsureDiscordFirstRunMarker(string? appDir)
        {
            if (string.IsNullOrEmpty(appDir)) return;
            try
            {
                string verName = Path.GetFileName(appDir).Replace("app-", "");
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string targetDir = Path.Combine(appData, "discord", verName);
                Directory.CreateDirectory(targetDir);
                string markerPath = Path.Combine(targetDir, ".first-run");
                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, "true");
                }
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════
        // GESTION DU CENTRE DE CONTRÔLE (GUI UNIFIÉ)
        // ══════════════════════════════════════════════════════════════

        private void ShowManagerView()
        {
            PreloaderGrid.Visibility = Visibility.Collapsed;
            ConfirmGrid.Visibility = Visibility.Collapsed;
            ManagerGrid.Visibility = Visibility.Visible;

            this.Width = 680;
            this.Height = 440;

            RefreshStatusUI();
            _ = CheckForUpdatesUiAsync(userInitiated: false);
        }

        private void RefreshStatusUI()
        {
            DetectDiscordVersion();

            // 1. Version Discord
            if (_latestAppDir != null)
            {
                LblDiscordVersion.Text = Path.GetFileName(_latestAppDir).Replace("app-", "");
                LblDiscordVersion.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                LblDiscordVersion.Text = "NON TROUVÉ";
                LblDiscordVersion.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }

            // 2. Patch Vencord
            bool isPatched = false;
            bool isOpenAsar = false;
            if (_latestAppDir != null)
            {
                string resourcesDir = Path.Combine(_latestAppDir, "resources");
                string patchedIndicator = Path.Combine(resourcesDir, "_app.asar");
                if (File.Exists(patchedIndicator))
                {
                    isPatched = true;
                    var fi = new FileInfo(patchedIndicator);
                    if (fi.Length < 1000000)
                    {
                        isOpenAsar = true;
                    }
                }
            }

            if (isPatched)
            {
                LblVencordStatus.Text = "✓ ACTIF";
                LblVencordStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#57F287"));
            }
            else
            {
                LblVencordStatus.Text = "✕ NON PATCHÉ";
                LblVencordStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ED4245"));
            }

            // 3. OpenAsar
            if (isOpenAsar)
            {
                LblOpenAsarStatus.Text = "✓ INSTALLÉ";
                LblOpenAsarStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EB459E"));
            }
            else
            {
                LblOpenAsarStatus.Text = "NON INSTALLÉ";
                LblOpenAsarStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#949ba4"));
            }

            // 4. Raccourci Menu Démarrer
            string startMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                @"Microsoft\Windows\Start Menu\Programs\Discord.lnk");
            bool isShortcutConfigured = false;
            try
            {
                if (File.Exists(startMenuLnk))
                {
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic? shell = Activator.CreateInstance(shellType);
                        if (shell != null)
                        {
                            dynamic sc = shell.CreateShortcut(startMenuLnk);
                            string target = (string)sc.TargetPath;
                            if (target.Contains("VencordEX", StringComparison.OrdinalIgnoreCase))
                            {
                                isShortcutConfigured = true;
                            }
                        }
                    }
                }
            }
            catch { }

            if (isShortcutConfigured)
            {
                LblShortcutStatus.Text = "✓ VENCORDEX";
                LblShortcutStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#57F287"));
            }
            else
            {
                LblShortcutStatus.Text = "ORIGINAL";
                LblShortcutStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#949ba4"));
            }

            // 5. Version VencordEX
            LblVencordEXVersion.Text = $"v{CurrentVersion.ToString(3)}";
            LblVencordEXVersion.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8da0f9"));
        }

        private async void BtnUpdateAction_Click(object sender, RoutedEventArgs e)
        {
            if (_isCheckingUpdate) return;

            if (_latestAvailableUpdate != null && _latestAvailableUpdate.IsNewer)
            {
                var confirm = MessageBox.Show(
                    $"Voulez-vous télécharger et installer la mise à jour {_latestAvailableUpdate.TagName} de VencordEX ?\n\nL'application va se fermer, remplacer le binaire et se relancer automatiquement.",
                    "Mise à jour de VencordEX",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    await ApplyUpdateAsync(_latestAvailableUpdate);
                }
            }
            else
            {
                await CheckForUpdatesUiAsync(userInitiated: true);
            }
        }

        private async Task CheckForUpdatesUiAsync(bool userInitiated)
        {
            if (_isCheckingUpdate) return;
            _isCheckingUpdate = true;

            try
            {
                var txtStatus = (TextBlock?)BtnUpdateAction.Template?.FindName("TxtUpdateStatus", BtnUpdateAction);
                var txtIcon = (TextBlock?)BtnUpdateAction.Template?.FindName("TxtUpdateIcon", BtnUpdateAction);
                var btnBorder = (Border?)BtnUpdateAction.Template?.FindName("BtnBorder", BtnUpdateAction);

                if (txtStatus != null) txtStatus.Text = "Recherche...";

                if (userInitiated)
                {
                    LblManagerLog.Text = "Vérification des mises à jour sur GitHub (t12lve/VencordEX)...";
                }

                var update = await Task.Run(CheckForGitHubUpdateAsync);

                if (update != null && update.IsNewer)
                {
                    _latestAvailableUpdate = update;

                    if (txtStatus != null)
                    {
                        txtStatus.Text = $"MàJ {update.TagName}";
                        txtStatus.Foreground = System.Windows.Media.Brushes.White;
                    }
                    if (txtIcon != null) txtIcon.Text = "⚡";

                    if (btnBorder != null)
                    {
                        btnBorder.Background = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                        btnBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#059669"));
                    }

                    LblVencordEXVersion.Text = $"{update.TagName} dispo !";
                    LblVencordEXVersion.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B"));

                    LblManagerLog.Text = $"🚀 Mise à jour {update.TagName} disponible ! Cliquez sur 'MàJ {update.TagName}' pour l'installer.";
                }
                else
                {
                    _latestAvailableUpdate = null;

                    if (txtStatus != null)
                    {
                        txtStatus.Text = "À jour ✓";
                        txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                    }
                    if (txtIcon != null) txtIcon.Text = "✓";

                    LblVencordEXVersion.Text = $"v{CurrentVersion.ToString(3)}";
                    LblVencordEXVersion.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));

                    if (userInitiated)
                    {
                        LblManagerLog.Text = $"✓ VencordEX est à jour (v{CurrentVersion.ToString(3)}). Aucune mise à jour disponible.";
                    }
                }
            }
            catch (Exception ex)
            {
                if (userInitiated)
                {
                    LblManagerLog.Text = "Erreur vérification mise à jour : " + ex.Message;
                }
            }
            finally
            {
                _isCheckingUpdate = false;
            }
        }

        private async Task<GitHubReleaseInfo?> CheckForGitHubUpdateAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("VencordEX-AutoUpdater");
                client.Timeout = TimeSpan.FromSeconds(6);

                string url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
                string json = await client.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagElem) ? (tagElem.GetString() ?? "") : "";
                string htmlUrl = root.TryGetProperty("html_url", out var htmlElem) ? (htmlElem.GetString() ?? "") : "";

                string cleanTag = tagName.TrimStart('v', 'V');
                Version? remoteVer = null;
                if (Version.TryParse(cleanTag, out var v))
                {
                    remoteVer = v;
                }
                else if (System.Text.RegularExpressions.Regex.Match(cleanTag, @"^(\d+\.\d+(\.\d+)?)") is { Success: true } m)
                {
                    string norm = m.Value;
                    if (norm.Count(c => c == '.') == 1) norm += ".0";
                    Version.TryParse(norm, out remoteVer);
                }

                string exeUrl = "";
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                        if (name.Equals("VencordEX.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            exeUrl = asset.TryGetProperty("browser_download_url", out var b) ? (b.GetString() ?? "") : "";
                            break;
                        }
                    }
                }

                bool isNewer = remoteVer != null && remoteVer > CurrentVersion;

                return new GitHubReleaseInfo
                {
                    TagName = tagName,
                    HtmlUrl = htmlUrl,
                    ExeDownloadUrl = exeUrl,
                    ParsedVersion = remoteVer,
                    IsNewer = isNewer
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task ApplyUpdateAsync(GitHubReleaseInfo update)
        {
            try
            {
                if (string.IsNullOrEmpty(update.ExeDownloadUrl))
                {
                    Process.Start(new ProcessStartInfo { FileName = update.HtmlUrl, UseShellExecute = true });
                    LblManagerLog.Text = "Redirection vers GitHub pour le téléchargement manuel...";
                    return;
                }

                LblManagerLog.Text = $"Téléchargement de {update.TagName} en cours...";
                string tempExe = Path.Combine(Path.GetTempPath(), $"VencordEX_Update_{Guid.NewGuid():N}.exe");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("VencordEX-AutoUpdater");
                    var data = await client.GetByteArrayAsync(update.ExeDownloadUrl);
                    if (data.Length < 100000)
                    {
                        throw new Exception("Binaire téléchargé corrompu ou incomplet.");
                    }
                    await File.WriteAllBytesAsync(tempExe, data);
                }

                LblManagerLog.Text = "Installation de la mise à jour et relance en cours...";

                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ??
                                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VencordEX.exe");
                string appDir = Path.GetDirectoryName(currentExe) ?? AppDomain.CurrentDomain.BaseDirectory;
                string targetVencordEX = Path.Combine(appDir, "VencordEX.exe");
                string targetSettings = Path.Combine(appDir, "VencordEXSettings.exe");

                string batchFile = Path.Combine(Path.GetTempPath(), $"vencordex_updater_{Guid.NewGuid():N}.cmd");
                int pid = Process.GetCurrentProcess().Id;

                string batchContent = $@"@echo off
timeout /t 1 /nobreak > NUL
:waitproc
tasklist /fi ""PID eq {pid}"" 2>NUL | find ""{pid}"" > NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak > NUL
    goto waitproc
)
copy /y ""{tempExe}"" ""{targetVencordEX}"" > NUL
if exist ""{targetSettings}"" (
    copy /y ""{tempExe}"" ""{targetSettings}"" > NUL
)
del ""{tempExe}"" > NUL 2>&1
start """" ""{targetVencordEX}"" --manager
(goto) 2>nul & del ""%~f0""
";

                await File.WriteAllTextAsync(batchFile, batchContent);

                bool needsElevation = false;
                try
                {
                    string testFile = Path.Combine(appDir, ".write_test");
                    File.WriteAllText(testFile, "1");
                    File.Delete(testFile);
                }
                catch
                {
                    needsElevation = true;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchFile}\"",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    Verb = needsElevation ? "runas" : ""
                };

                Process.Start(psi);
                Application.Current.Shutdown();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                LblManagerLog.Text = "Erreur installation mise à jour : " + ex.Message;
            }
        }

        private void BtnOpenManager_Click(object sender, RoutedEventArgs e)
        {
            ShowManagerView();
        }

        private void BtnCloseManager_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Action : Patcher les raccourcis Windows (Méthode unifiée)
        private void BtnPatchShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigureAllShortcuts();
                LblManagerLog.Text = "✓ Tous les raccourcis Discord et le raccourci VencordEX sont configurés !";
                RefreshStatusUI();
            }
            catch (Exception ex)
            {
                LblManagerLog.Text = "Erreur configuration raccourcis : " + ex.Message;
            }
        }

        public void ConfigureAllShortcuts()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? 
                             Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VencordEX.exe");

            string iconPath = Path.Combine(_discordDir, "app.ico") + ",0";
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programsDir = Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs");

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    // 1. Raccourci VencordEX (pour ouvrir le Centre de Contrôle directement depuis Windows Search)
                    try
                    {
                        string vencordLnk = Path.Combine(programsDir, "VencordEX.lnk");
                        dynamic scV = shell.CreateShortcut(vencordLnk);
                        scV.TargetPath = exePath;
                        scV.Arguments = "--manager";
                        scV.WorkingDirectory = _discordDir;
                        scV.IconLocation = iconPath;
                        scV.Description = "VencordEX • Centre de Contrôle & Paramètres";
                        scV.Save();
                    }
                    catch { }

                    // 2. Raccourcis Discord (Auto-Patcher)
                    string[] targetPaths = new[]
                    {
                        Path.Combine(programsDir, "Discord.lnk"),
                        Path.Combine(programsDir, @"Discord Inc\Discord.lnk"),
                        Path.Combine(appData, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Discord.lnk"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Discord.lnk")
                    };

                    foreach (var p in targetPaths)
                    {
                        string? dir = Path.GetDirectoryName(p);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            try
                            {
                                var fi = new FileInfo(p);
                                if (fi.Exists && fi.IsReadOnly) fi.IsReadOnly = false;

                                dynamic sc = shell.CreateShortcut(p);
                                sc.TargetPath = exePath;
                                sc.Arguments = "";
                                sc.WorkingDirectory = _discordDir;
                                sc.IconLocation = iconPath;
                                sc.Description = "Discord avec VencordEX Auto-Patch";
                                sc.Save();

                                fi.Refresh();
                                fi.IsReadOnly = true;
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        // Action : Restaurer les raccourcis originaux
        private void BtnUnpatchShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string updateExe = Path.Combine(_discordDir, "Update.exe");
                string iconPath = Path.Combine(_discordDir, "app.ico") + ",0";
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string programsDir = Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs");

                string[] targetPaths = new[]
                {
                    Path.Combine(programsDir, "Discord.lnk"),
                    Path.Combine(programsDir, @"Discord Inc\Discord.lnk"),
                    Path.Combine(appData, @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Discord.lnk"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Discord.lnk")
                };

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        foreach (var p in targetPaths)
                        {
                            string? dir = Path.GetDirectoryName(p);
                            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            {
                                try
                                {
                                    var fi = new FileInfo(p);
                                    if (fi.Exists && fi.IsReadOnly) fi.IsReadOnly = false;

                                    dynamic sc = shell.CreateShortcut(p);
                                    sc.TargetPath = updateExe;
                                    sc.Arguments = "--processStart Discord.exe";
                                    sc.WorkingDirectory = _discordDir;
                                    sc.IconLocation = iconPath;
                                    sc.Description = "Discord";
                                    sc.Save();
                                }
                                catch { }
                            }
                        }

                        // Nettoyer le raccourci VencordEX si souhaité
                        try
                        {
                            string vencordLnk = Path.Combine(programsDir, "VencordEX.lnk");
                            if (File.Exists(vencordLnk)) File.Delete(vencordLnk);
                        }
                        catch { }
                    }
                }

                LblManagerLog.Text = "✓ Raccourcis officiels Discord restaurés avec succès !";
                RefreshStatusUI();
            }
            catch (Exception ex)
            {
                LblManagerLog.Text = "Erreur restauration raccourcis : " + ex.Message;
            }
        }

        private void PerformUninstall()
        {
            var res = MessageBox.Show(
                "Voulez-vous vraiment désinstaller VencordEX et restaurer les raccourcis d'origine de Discord ?",
                "Désinstallation de VencordEX",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
                return;
            }

            try
            {
                // 1. Restaurer les raccourcis d'origine
                BtnUnpatchShortcut_Click(this, new RoutedEventArgs());

                // 2. Retirer l'enregistrement dans le Registre Windows
                try
                {
                    Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\VencordEX", false);
                }
                catch { }
                try
                {
                    Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\VencordEX", false);
                }
                catch { }

                // 3. Supprimer le dossier d'installation s'il s'agit de Program Files
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                if (appDir.Contains("Program Files", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c timeout /t 2 /nobreak > NUL & rmdir /s /q \"{appDir}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }

                MessageBox.Show("VencordEX a été désinstallé avec succès. Les raccourcis officiels de Discord ont été restaurés.", 
                    "Désinstallation terminée", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la désinstallation : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Application.Current.Shutdown();
            Environment.Exit(0);
        }

        // Action : Installer OpenAsar
        private async void BtnInstallOpenAsar_Click(object sender, RoutedEventArgs e)
        {
            LblManagerLog.Text = "Installation d'OpenAsar en cours...";
            await Task.Run(async () =>
            {
                await EnsureCliBinaryAsync();
                RunCliCommand("-install-openasar -branch auto");
            });

            LblManagerLog.Text = "✓ OpenAsar installé avec succès !";
            RefreshStatusUI();
        }

        // Action : Désinstaller OpenAsar
        private async void BtnUninstallOpenAsar_Click(object sender, RoutedEventArgs e)
        {
            LblManagerLog.Text = "Désinstallation d'OpenAsar en cours...";
            await Task.Run(async () =>
            {
                await EnsureCliBinaryAsync();
                RunCliCommand("-uninstall-openasar -branch auto");
            });

            LblManagerLog.Text = "✓ OpenAsar désinstallé avec succès !";
            RefreshStatusUI();
        }

        // Action : Restaurer Discord d'origine (retirer tous les patchs)
        private async void BtnRestoreDiscordClean_Click(object sender, RoutedEventArgs e)
        {
            LblManagerLog.Text = "Suppression de tous les patchs Vencord de Discord...";
            await Task.Run(async () =>
            {
                await EnsureCliBinaryAsync();
                RunCliCommand("-uninstall -branch auto");
            });

            LblManagerLog.Text = "✓ Discord a été restauré à 100% officiel (patches Vencord retirés).";
            RefreshStatusUI();
        }

        // Action : Sauvegarder les préférences Vencord
        private void BtnBackupPreferences_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string backupsDir = Path.Combine(_vencordDir, "backups");
                Directory.CreateDirectory(backupsDir);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string zipPath = Path.Combine(backupsDir, $"VencordEX-Backup-{timestamp}.zip");

                string tempDir = Path.Combine(Path.GetTempPath(), "VencordEX_Backup_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                try
                {
                    string settingsDir = Path.Combine(_vencordDir, "settings");
                    if (Directory.Exists(settingsDir))
                    {
                        CopyDirectory(settingsDir, Path.Combine(tempDir, "settings"));
                    }

                    string themesDir = Path.Combine(_vencordDir, "themes");
                    if (Directory.Exists(themesDir))
                    {
                        CopyDirectory(themesDir, Path.Combine(tempDir, "themes"));
                    }

                    ZipFile.CreateFromDirectory(tempDir, zipPath);
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }

                LblManagerLog.Text = $"✓ Sauvegarde réussie : {Path.GetFileName(zipPath)}";
            }
            catch (Exception ex)
            {
                LblManagerLog.Text = "Erreur sauvegarde : " + ex.Message;
            }
        }

        // Action : Restaurer une sauvegarde de préférences
        private void BtnRestorePreferences_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string backupsDir = Path.Combine(_vencordDir, "backups");
                Directory.CreateDirectory(backupsDir);

                var ofd = new OpenFileDialog
                {
                    Title = "Choisir une sauvegarde VencordEX (.zip)",
                    Filter = "Sauvegardes VencordEX (*.zip)|*.zip",
                    InitialDirectory = backupsDir
                };

                if (ofd.ShowDialog() == true)
                {
                    string zipPath = ofd.FileName;
                    string tempDir = Path.Combine(Path.GetTempPath(), "VencordEX_Restore_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        ZipFile.ExtractToDirectory(zipPath, tempDir);

                        string tempSettings = Path.Combine(tempDir, "settings");
                        if (Directory.Exists(tempSettings))
                        {
                            string destSettings = Path.Combine(_vencordDir, "settings");
                            Directory.CreateDirectory(destSettings);
                            CopyDirectory(tempSettings, destSettings);
                        }

                        string tempThemes = Path.Combine(tempDir, "themes");
                        if (Directory.Exists(tempThemes))
                        {
                            string destThemes = Path.Combine(_vencordDir, "themes");
                            Directory.CreateDirectory(destThemes);
                            CopyDirectory(tempThemes, destThemes);
                        }

                        LblManagerLog.Text = "✓ Préférences et thèmes restaurés avec succès !";
                    }
                    finally
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LblManagerLog.Text = "Erreur restauration : " + ex.Message;
            }
        }

        private void BtnOpenBackupsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string backupsDir = Path.Combine(_vencordDir, "backups");
                Directory.CreateDirectory(backupsDir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = backupsDir,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnLaunchDiscordNow_Click(object sender, RoutedEventArgs e)
        {
            LaunchDiscord();
            Application.Current.Shutdown();
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
            }
        }

        private void RunCliCommand(string arguments)
        {
            if (File.Exists(_cliPath))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _cliPath,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(15000);
                }
                catch { }
            }
        }

        // ══════════════════════════════════════════════════════════════
        // GESTION DU PRÉCHARGEUR ET DU DIALOGUE DE CONFIRMATION
        // ══════════════════════════════════════════════════════════════

        private void ShowConfirmDialog()
        {
            PreloaderGrid.Visibility = Visibility.Collapsed;
            ConfirmGrid.Visibility = Visibility.Visible;
            ManagerGrid.Visibility = Visibility.Collapsed;
        }

        private async void BtnKillAndRestart_Click(object sender, RoutedEventArgs e)
        {
            ConfirmGrid.Visibility = Visibility.Collapsed;
            PreloaderGrid.Visibility = Visibility.Visible;
            ManagerGrid.Visibility = Visibility.Collapsed;

            SetStatus("FERMETURE", "Fermeture de la session Discord en cours...", "KILL");
            await AnimateProgressAsync(0, 15, 200);

            await KillDiscordAsync();

            await RunMotionPreloaderAsync(launchDiscordAfter: true);
        }

        private async void BtnPatchNextLaunch_Click(object sender, RoutedEventArgs e)
        {
            ConfirmGrid.Visibility = Visibility.Collapsed;
            PreloaderGrid.Visibility = Visibility.Visible;
            ManagerGrid.Visibility = Visibility.Collapsed;

            SetStatus("PRÉPARATION", "Application du patch sur la nouvelle version...", "EN ATTENTE");

            await Task.Run(async () =>
            {
                await EnsureCliBinaryAsync();
                RunCliCommand("-install -branch auto");
            });

            await AnimateProgressAsync(0, 100, 450);

            PercentText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#57F287"));

            SetStatus("SUCCÈS", "Patch prêt ! Il s'activera au prochain lancement.", "ENREGISTRÉ");
            FooterText.Text = "VOTRE SESSION ACTUELLE CONTINUE SANS INTERRUPTION";

            await Task.Delay(800);

            LaunchDiscord();
            await ExitAppAsync();
        }

        private async Task KillDiscordAsync()
        {
            var procs = Process.GetProcessesByName("Discord");
            foreach (var p in procs)
            {
                try { p.CloseMainWindow(); } catch { }
            }

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 1200)
            {
                if (Process.GetProcessesByName("Discord").Length == 0)
                    break;
                await Task.Delay(80);
            }

            foreach (var p in Process.GetProcessesByName("Discord"))
            {
                try { p.Kill(); } catch { }
            }

            await Task.Delay(150);
        }

        private async Task RunMotionPreloaderAsync(bool launchDiscordAfter)
        {
            SetStatus("SYNCHRONISATION", "Vérification des fichiers de Discord...", "VÉRIFICATION");
            await AnimateProgressAsync(0, 25, 300);

            await EnsureCliBinaryAsync();
            await AnimateProgressAsync(25, 50, 250);

            SetStatus("INJECTION", "Application du patch Vencord en arrière-plan...", "PATCHING");

            var patchTask = Task.Run(() =>
            {
                if (!_isTestMode)
                {
                    RunCliCommand("-install -branch auto");
                }
                else
                {
                    Task.Delay(1000).Wait();
                }
            });

            await AnimateProgressAsync(50, 85, 800);
            await patchTask;

            SetStatus("SUCCÈS", "Vencord injecté ! Lancement de Discord...", "TERMINÉ");
            await AnimateProgressAsync(85, 100, 300);

            PercentText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#57F287"));

            await Task.Delay(300);

            if (launchDiscordAfter && !_isTestMode)
            {
                LaunchDiscord();
            }

            await ExitAppAsync();
        }

        private async Task EnsureCliBinaryAsync()
        {
            if (!Directory.Exists(_vencordDir))
            {
                Directory.CreateDirectory(_vencordDir);
            }

            if (!File.Exists(_cliPath))
            {
                try
                {
                    using var http = new HttpClient();
                    var data = await http.GetByteArrayAsync("https://github.com/Vencord/Installer/releases/latest/download/VencordInstallerCli.exe");
                    await File.WriteAllBytesAsync(_cliPath, data);
                }
                catch { }
            }
        }

        private async Task AnimateProgressAsync(int fromPercent, int toPercent, int durationMs)
        {
            int steps = Math.Max(1, toPercent - fromPercent);
            int stepDelay = Math.Max(4, durationMs / steps);
            double trackWidth = 540 - 56 - 30;

            for (int i = fromPercent; i <= toPercent; i++)
            {
                PercentText.Text = i < 10 ? $"0{i}%" : $"{i}%";
                ProgressBarFill.Width = (trackWidth * i) / 100.0;
                await Task.Delay(stepDelay);
            }
        }

        private void SetStatus(string title, string subtitle, string badge)
        {
            StatusTitle.Text = title;
            StatusSub.Text = subtitle;
            BadgeText.Text = badge;
        }

        private void LaunchDiscord()
        {
            EnsureDiscordFirstRunMarker(_latestAppDir);

            // 1. Privilégier Update.exe --processStart Discord.exe (initialisation propre Squirrel & runtime)
            try
            {
                string updateExe = Path.Combine(_discordDir, "Update.exe");
                if (File.Exists(updateExe))
                {
                    string args = "--processStart Discord.exe";
                    if (_passedArgs.Length > 0)
                    {
                        args += " --process-start-args \"" + string.Join(" ", _passedArgs) + "\"";
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = updateExe,
                        Arguments = args,
                        WorkingDirectory = _discordDir,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return;
                }
            }
            catch { }

            // 2. Fallback binaire direct si Update.exe n'est pas trouvé
            try
            {
                if (!string.IsNullOrEmpty(_latestAppDir))
                {
                    string discordExe = Path.Combine(_latestAppDir, "Discord.exe");
                    if (File.Exists(discordExe))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = discordExe,
                            Arguments = string.Join(" ", _passedArgs),
                            WorkingDirectory = _latestAppDir,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                        return;
                    }
                }
            }
            catch { }
        }

        private async Task ExitAppAsync()
        {
            try
            {
                var fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200));
                this.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
            catch { }

            await Task.Delay(250);

            try { this.Hide(); } catch { }
            try { Application.Current.Shutdown(); } catch { }
            Environment.Exit(0);
        }
    }

    public class GitHubReleaseInfo
    {
        public string TagName { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public string ExeDownloadUrl { get; set; } = string.Empty;
        public Version? ParsedVersion { get; set; }
        public bool IsNewer { get; set; }
    }
}
