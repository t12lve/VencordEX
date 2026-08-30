# Script de desinstallation et restauration du raccourci Discord d'origine
$ErrorActionPreference = "SilentlyContinue"

Write-Host "=== Restauration du raccourci Discord d'origine ===" -ForegroundColor Cyan

$discordDir = "$env:LOCALAPPDATA\Discord"
$startMenuLnk = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Discord.lnk"
$backupLnk = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Discord_Original.lnk.bak"

if (Test-Path $backupLnk) {
    Copy-Item $backupLnk $startMenuLnk -Force
    Remove-Item $backupLnk -Force
    Write-Host "Raccourci Menu Demarrer restaure depuis la sauvegarde." -ForegroundColor Green
} else {
    $updateExe = Join-Path $discordDir "Update.exe"
    $wsh = New-Object -ComObject WScript.Shell
    $sc = $wsh.CreateShortcut($startMenuLnk)
    $sc.TargetPath = $updateExe
    $sc.Arguments = "--processStart Discord.exe"
    $sc.WorkingDirectory = $discordDir
    $sc.IconLocation = "$discordDir\app.ico,0"
    $sc.Save()
    Write-Host "Raccourci Menu Demarrer reinitialise vers Update.exe officiel." -ForegroundColor Green
}

$desktopPath = [Environment]::GetFolderPath('Desktop')
$desktopLnk = Join-Path $desktopPath "Discord.lnk"
$backupDesk = Join-Path $desktopPath "Discord_Original.lnk.bak"

if (Test-Path $backupDesk) {
    Copy-Item $backupDesk $desktopLnk -Force
    Remove-Item $backupDesk -Force
    Write-Host "Raccourci Bureau restaure." -ForegroundColor Green
}

# Restauration Registre de demarrage Windows
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$backupRun = (Get-ItemProperty -Path $runKey -Name "Discord_Original_Backup" -ErrorAction SilentlyContinue).Discord_Original_Backup
if ($backupRun) {
    Set-ItemProperty -Path $runKey -Name "Discord" -Value $backupRun -Force
    Remove-ItemProperty -Path $runKey -Name "Discord_Original_Backup" -Force
    Write-Host "Entree de demarrage Windows restauree." -ForegroundColor Green
}

Write-Host "`nRestauration terminee avec succes." -ForegroundColor Green
