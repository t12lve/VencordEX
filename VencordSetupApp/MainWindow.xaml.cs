using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace VencordSetup
{
    public partial class MainWindow : Window
    {
        private string _installedExePath = string.Empty;

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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new OpenFolderDialog
            {
                Title = "Choisir le dossier d'installation",
                InitialDirectory = @"C:\Program Files"
            };

            if (fbd.ShowDialog() == true)
            {
                TxtInstallPath.Text = Path.Combine(fbd.FolderName, "VencordEX");
            }
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            string targetDir = TxtInstallPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                targetDir = @"C:\Program Files\VencordEX";
            }

            bool patchShortcuts = ChkPatchShortcuts.IsChecked == true;
            bool createSettingsShortcut = ChkCreateSettingsShortcut.IsChecked == true;
            bool registerUninstall = ChkRegisterUninstall.IsChecked == true;

            StepConfigGrid.Visibility = Visibility.Collapsed;
            BtnGroupConfig.Visibility = Visibility.Collapsed;
            StepProgressGrid.Visibility = Visibility.Visible;

            await AnimateProgressBarAsync(0, 30, 300);

            try
            {
                await Task.Run(() =>
                {
                    // 1. Création du dossier cible
                    Directory.CreateDirectory(targetDir);

                    // 2. Extraction du binaire VencordEX.exe
                    byte[]? payloadBytes = null;
                    var assembly = Assembly.GetExecutingAssembly();
                    foreach (var name in assembly.GetManifestResourceNames())
                    {
                        if (name.EndsWith("VencordEX.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            using var stream = assembly.GetManifestResourceStream(name);
                            if (stream != null)
                            {
                                using var ms = new MemoryStream();
                                stream.CopyTo(ms);
                                payloadBytes = ms.ToArray();
                                break;
                            }
                        }
                    }

                    // Repli : si non trouvé dans les ressources, chercher à côté de l'installeur
                    if (payloadBytes == null)
                    {
                        string localCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VencordEX.exe");
                        if (File.Exists(localCandidate))
                        {
                            payloadBytes = File.ReadAllBytes(localCandidate);
                        }
                    }

                    if (payloadBytes == null)
                    {
                        throw new Exception("Impossible de trouver le binaire VencordEX.exe à installer.");
                    }

                    string targetVencordEX = Path.Combine(targetDir, "VencordEX.exe");
                    string targetSettings = Path.Combine(targetDir, "VencordEXSettings.exe");

                    File.WriteAllBytes(targetVencordEX, payloadBytes);
                    File.WriteAllBytes(targetSettings, payloadBytes);

                    _installedExePath = targetVencordEX;

                    // 3. Configuration des raccourcis
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string discordDir = Path.Combine(localAppData, "Discord");
                    string iconPath = Path.Combine(discordDir, "app.ico") + ",0";
                    string programsDir = Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs");

                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic? shell = Activator.CreateInstance(shellType);
                        if (shell != null)
                        {
                            // Raccourci Paramètres VencordEX
                            if (createSettingsShortcut)
                            {
                                try
                                {
                                    string vencordLnk = Path.Combine(programsDir, "VencordEX.lnk");
                                    dynamic scV = shell.CreateShortcut(vencordLnk);
                                    scV.TargetPath = targetSettings;
                                    scV.WorkingDirectory = discordDir;
                                    scV.IconLocation = iconPath;
                                    scV.Description = "VencordEX • Centre de Contrôle & Paramètres";
                                    scV.Save();
                                }
                                catch { }
                            }

                            // Raccourcis Discord habituels
                            if (patchShortcuts)
                            {
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
                                            sc.TargetPath = targetVencordEX;
                                            sc.Arguments = "";
                                            sc.WorkingDirectory = discordDir;
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

                    // 4. Enregistrement dans le Registre Windows (Ajout/Suppression de programmes)
                    if (registerUninstall)
                    {
                        try
                        {
                            using var key = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\VencordEX");
                            if (key != null)
                            {
                                key.SetValue("DisplayName", "VencordEX");
                                key.SetValue("DisplayVersion", "1.1.1");
                                key.SetValue("Publisher", "t12lve le vibecodeur de l'extreme");
                                key.SetValue("DisplayIcon", targetVencordEX + ",0");
                                key.SetValue("InstallLocation", targetDir);
                                key.SetValue("UninstallString", $"\"{targetSettings}\" --uninstall");
                                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                                key.SetValue("EstimatedSize", 2048, RegistryValueKind.DWord);
                            }
                        }
                        catch { }
                    }
                });

                await AnimateProgressBarAsync(30, 100, 400);

                LblFinalPath.Text = targetDir;
                StepProgressGrid.Visibility = Visibility.Collapsed;
                StepSuccessGrid.Visibility = Visibility.Visible;
                BtnGroupSuccess.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de l'installation : " + ex.Message, "Erreur d'installation", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private async Task AnimateProgressBarAsync(int fromPercent, int toPercent, int durationMs)
        {
            int steps = Math.Max(1, toPercent - fromPercent);
            int stepDelay = Math.Max(5, durationMs / steps);
            double trackWidth = 580 - 52 - 24;

            for (int i = fromPercent; i <= toPercent; i++)
            {
                InstallProgressBar.Width = (trackWidth * i) / 100.0;
                await Task.Delay(stepDelay);
            }
        }

        private void BtnLaunchDiscord_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_installedExePath) && File.Exists(_installedExePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _installedExePath,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
            Application.Current.Shutdown();
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
