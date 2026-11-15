<#
.SYNOPSIS
    Downloads and starts a portable smtp4dev instance for local email delivery.

.DESCRIPTION
    The development appsettings point SMTP at localhost:2525. When Docker Desktop is not
    available (so `docker compose --profile mail` cannot be used), this script downloads the
    smtp4dev Windows x64 standalone build into %USERPROFILE%\Tools, expands it, and starts it with
    the SMTP listener on 2525 and the web UI on http://localhost:5001.

    Nothing is installed machine-wide and no service is registered. smtp4dev keeps its own SQLite
    database under %APPDATA%\smtp4dev.

.PARAMETER Foreground
    Keep the smtp4dev process attached to this shell instead of detaching.

.PARAMETER SmtpPort
    SMTP listener port. Default 2525, matching appsettings.Development.json.

.PARAMETER UiPort
    Web UI port. Default 5001.

.PARAMETER Version
    smtp4dev release to install. Default 3.16.1.

.PARAMETER Force
    Re-download the archive even when the install directory already exists.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-smtp4dev.ps1

    Then open http://localhost:5001 and run the E2E flow; emails appear in the inbox.
#>
[CmdletBinding()]
param(
    [string]$ToolsRoot = (Join-Path $env:USERPROFILE 'Tools'),
    [string]$Version = '3.16.1',
    [int]$SmtpPort = 2525,
    [int]$UiPort = 5001,
    [switch]$Force,
    [switch]$Foreground
)

$ErrorActionPreference = 'Stop'

$installDir = Join-Path $ToolsRoot "smtp4dev-$Version"
$exe = Join-Path $installDir 'Rnwood.Smtp4dev.exe'
$downloads = Join-Path $ToolsRoot '_downloads'
$archive = Join-Path $downloads "smtp4dev-$Version-win-x64.zip"

if ($Force -or -not (Test-Path $exe)) {
    New-Item -ItemType Directory -Force -Path $downloads | Out-Null

    if ($Force -or -not (Test-Path $archive)) {
        $url = "https://github.com/rnwood/smtp4dev/releases/download/$Version/Rnwood.Smtp4dev-win-x64-$Version.zip"

        Write-Host "Downloading smtp4dev $Version from $url ..."
        # Windows PowerShell 5.1 defaults to older TLS; GitHub requires 1.2+.
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
    }

    Write-Host "Expanding $archive ..."
    if (Test-Path $installDir) { Remove-Item -Recurse -Force $installDir }
    Expand-Archive -LiteralPath $archive -DestinationPath $installDir -Force
}

if (-not (Test-Path $exe)) {
    throw "smtp4dev was not found at $exe after extraction."
}

# ServerOptions is the section smtp4dev binds. Environment overrides keep the host's own
# appsettings.json untouched and make the ports match the application's Development settings.
$env:ServerOptions__Port = $SmtpPort
$env:ServerOptions__Urls = "http://localhost:$UiPort"
$env:ServerOptions__AllowRemoteConnections = 'false'
$env:ServerOptions__DisableIPv6 = 'true'
$env:ServerOptions__TlsMode = 'None'

Write-Host "SMTP:    localhost:$SmtpPort"
Write-Host "Web UI:  http://localhost:$UiPort"
Write-Host "Install: $installDir"

if ($Foreground) {
    Write-Host 'Starting smtp4dev in the foreground...'
    & $exe
    return
}

Write-Host 'Starting smtp4dev detached...'
Start-Process -FilePath $exe -WorkingDirectory $installDir -WindowStyle Hidden | Out-Null

function Test-TcpPort([int]$Port) {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $task = $client.ConnectAsync('127.0.0.1', $Port)
        return $task.Wait(500) -and $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

$deadline = (Get-Date).AddMinutes(2)
while ((Get-Date) -lt $deadline) {
    if (Test-TcpPort $SmtpPort) {
        Write-Host ''
        Write-Host "smtp4dev is listening on $SmtpPort."
        Write-Host "Inbox: http://localhost:$UiPort"
        return
    }
    Start-Sleep -Milliseconds 500
}

throw "smtp4dev did not open port $SmtpPort within two minutes. Run with -Foreground to see its output."
