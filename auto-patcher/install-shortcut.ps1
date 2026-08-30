# Script d'installation du raccourci Discord Vencord Auto-Patch
param(
    [switch]$ConfigureStartup
)

$ErrorActionPreference = "Stop"

Write-Host "=== Configuration du lanceur Vencord Auto-Patch ===" -ForegroundColor Cyan

$vencordDir = "$env:APPDATA\Vencord"
$discordDir = "$env:LOCALAPPDATA\Discord"
$scriptDir = $PSScriptRoot

if (-not (Test-Path $vencordDir)) {
    New-Item -ItemType Directory -Path $vencordDir -Force | Out-Null
}

# 1. Copie des scripts dans AppData\Vencord
Write-Host "[1/4] Installation des scripts dans $vencordDir..." -ForegroundColor Yellow
Copy-Item (Join-Path $scriptDir "launch-discord.ps1") -Destination (Join-Path $vencordDir "launch-discord.ps1") -Force
Copy-Item (Join-Path $scriptDir "DiscordLauncher.vbs") -Destination (Join-Path $vencordDir "DiscordLauncher.vbs") -Force

# 2. Verification du binaire VencordInstallerCli
Write-Host "[2/4] Verification du patcher CLI..." -ForegroundColor Yellow
$cliPath = Join-Path $vencordDir "VencordInstallerCli.exe"
if (-not (Test-Path $cliPath)) {
    $url = "https://github.com/Vencord/Installer/releases/latest/download/VencordInstallerCli.exe"
    Write-Host "Telechargement de VencordInstallerCli.exe..." -ForegroundColor Gray
    try {
        Invoke-WebRequest -Uri $url -OutFile $cliPath -UseBasicParsing -TimeoutSec 20
    } catch {
        Write-Warning "Echec du telechargement direct. Vous pourrez placer VencordInstallerCli.exe dans $vencordDir manuellement."
    }
}
Write-Host "Patcher CLI pret." -ForegroundColor Green

# 3. Sauvegarde et mise a jour du raccourci Menu Demarrer
Write-Host "[3/4] Mise a jour du raccourci du menu Demarrer..." -ForegroundColor Yellow
$startMenuLnk = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Discord.lnk"
$backupLnk = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Discord_Original.lnk.bak"

$wsh = New-Object -ComObject WScript.Shell

if (Test-Path $startMenuLnk) {
    if (-not (Test-Path $backupLnk)) {
        Copy-Item $startMenuLnk $backupLnk -Force
        Write-Host "Sauvegarde du raccourci original creee ($backupLnk)." -ForegroundColor Gray
    }
}

$vbsPath = Join-Path $vencordDir "DiscordLauncher.vbs"
$iconPath = Join-Path $discordDir "app.ico"
if (-not (Test-Path $iconPath)) {
    $iconPath = "wscript.exe,0"
}

$sc = $wsh.CreateShortcut($startMenuLnk)
$sc.TargetPath = "wscript.exe"
$sc.Arguments = "`"$vbsPath`""
$sc.WorkingDirectory = $discordDir
$sc.IconLocation = "$iconPath,0"
$sc.Description = "Discord avec Vencord Auto-Patch"
$sc.Save()

# 4. Raccourci Bureau (s'il existe)
$desktopPath = [Environment]::GetFolderPath('Desktop')
$desktopLnk = Join-Path $desktopPath "Discord.lnk"
if (Test-Path $desktopLnk) {
    Write-Host "[4/4] Mise a jour du raccourci Bureau..." -ForegroundColor Yellow
    $backupDesk = Join-Path $desktopPath "Discord_Original.lnk.bak"
    if (-not (Test-Path $backupDesk)) {
        Copy-Item $desktopLnk $backupDesk -Force
    }
    $scDesk = $wsh.CreateShortcut($desktopLnk)
    $scDesk.TargetPath = "wscript.exe"
    $scDesk.Arguments = "`"$vbsPath`""
    $scDesk.WorkingDirectory = $discordDir
    $scDesk.IconLocation = "$iconPath,0"
    $scDesk.Description = "Discord avec Vencord Auto-Patch"
    $scDesk.Save()
} else {
    Write-Host "[4/4] Pas de raccourci sur le Bureau a modifier." -ForegroundColor Gray
}

# Optionnel : Démarrage Windows
if ($ConfigureStartup) {
    $runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    $existing = (Get-ItemProperty -Path $runKey -Name "Discord" -ErrorAction SilentlyContinue).Discord
    if ($existing) {
        Set-ItemProperty -Path $runKey -Name "Discord_Original_Backup" -Value $existing -Force
    }
    $startupCmd = "wscript.exe `"$vbsPath`" --start-inactive"
    Set-ItemProperty -Path $runKey -Name "Discord" -Value $startupCmd -Force
    Write-Host "[Demarrage Windows] Entree de demarrage mise a jour avec succes." -ForegroundColor Magenta
}

Write-Host "`n[SUCCES] Vencord Auto-Patch est pret et configure !" -ForegroundColor Green
Write-Host "Discord sera verifie et patche automatiquement a chaque clic sur son raccourci." -ForegroundColor Cyan
