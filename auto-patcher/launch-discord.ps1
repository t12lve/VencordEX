# Vencord Auto-Patch & Discord Launcher
# Ce script verifie si Discord a ete mis a jour (perte du patch Vencord)
# Si c'est le cas, il le re-patche silencieusement en < 100ms avant d'ouvrir Discord.

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DiscordArgs
)

$ErrorActionPreference = "SilentlyContinue"

$discordDir = "$env:LOCALAPPDATA\Discord"
$vencordAppData = "$env:APPDATA\Vencord"
$cliPath = Join-Path $vencordAppData "VencordInstallerCli.exe"

# 1. Detection et sécurisation des versions installées de Discord
$channels = @("Discord", "DiscordCanary", "DiscordPTB", "DiscordDevelopment")
foreach ($chan in $channels) {
    $chanLocal = Join-Path $env:LOCALAPPDATA $chan
    $chanData = Join-Path $env:APPDATA ($chan.ToLowerInvariant())
    if (Test-Path $chanLocal) {
        $apps = Get-ChildItem -Path $chanLocal -Directory -Filter "app-*" -ErrorAction SilentlyContinue
        foreach ($app in $apps) {
            $v = $app.Name -replace '^app-', ''
            if ($v) {
                $tgt = Join-Path $chanData $v
                if (-not (Test-Path $tgt)) {
                    New-Item -ItemType Directory -Path $tgt -Force -ErrorAction SilentlyContinue | Out-Null
                }
                $marker = Join-Path $tgt ".first-run"
                if (-not (Test-Path $marker)) {
                    Set-Content -Path $marker -Value "true" -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
}

$latestApp = Get-ChildItem -Path $discordDir -Directory -Filter "app-*" -ErrorAction SilentlyContinue |
             Sort-Object { try { [version]($_.Name -replace '^app-', '') } catch { 0 } } -Descending |
             Select-Object -First 1

if ($latestApp) {
    $verName = $latestApp.Name -replace '^app-', ''
    $appDataVer = Join-Path $env:APPDATA "discord\$verName"
    if (-not (Test-Path $appDataVer)) {
        New-Item -ItemType Directory -Path $appDataVer -Force -ErrorAction SilentlyContinue | Out-Null
    }
    $firstRun = Join-Path $appDataVer ".first-run"
    if (-not (Test-Path $firstRun)) {
        Set-Content -Path $firstRun -Value "true" -Force -ErrorAction SilentlyContinue
    }

    $resourcesDir = Join-Path $latestApp.FullName "resources"
    $patchedIndicator = Join-Path $resourcesDir "_app.asar"
    $appAsar = Join-Path $resourcesDir "app.asar"

    # Discord a ete mis a jour si app.asar existe mais que _app.asar est absent
    if ((Test-Path $appAsar) -and -not (Test-Path $patchedIndicator)) {
        # Si le binaire CLI n'est pas encore present dans AppData\Vencord, le telecharger
        if (-not (Test-Path $cliPath)) {
            $url = "https://github.com/Vencord/Installer/releases/latest/download/VencordInstallerCli.exe"
            try {
                Invoke-WebRequest -Uri $url -OutFile $cliPath -UseBasicParsing -TimeoutSec 15
            } catch {}
        }

        # Execution du patch automatique silencieux
        if (Test-Path $cliPath) {
            Start-Process -FilePath $cliPath -ArgumentList "-install", "-branch", "auto" -Wait -WindowStyle Hidden
        }
    }
}

# 2. Lancement de Discord (avec transmission transparente des arguments)
$updateExe = Join-Path $discordDir "Update.exe"
if (Test-Path $updateExe) {
    $execArgs = @("--processStart", "Discord.exe")
    if ($DiscordArgs -and $DiscordArgs.Count -gt 0) {
        $execArgs += "--process-start-args"
        $execArgs += ($DiscordArgs -join " ")
    }
    Start-Process -FilePath $updateExe -ArgumentList $execArgs
} elseif ($latestApp) {
    $discordExe = Join-Path $latestApp.FullName "Discord.exe"
    Start-Process -FilePath $discordExe -ArgumentList $DiscordArgs
}
