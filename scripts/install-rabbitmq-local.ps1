<#
.SYNOPSIS
    Downloads portable Erlang/OTP and RabbitMQ into the user profile.

.DESCRIPTION
    For a Windows development machine without Docker or admin rights. Nothing is written to
    Program Files and no Windows service is registered.

    RabbitMQ 4.4 supports Erlang/OTP 27 through 29. The versions below are pinned so the
    application is tested against a known pair rather than whatever happens to be latest.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install-rabbitmq-local.ps1
#>
[CmdletBinding()]
param(
    [string]$ToolsRoot = (Join-Path $env:USERPROFILE 'Tools'),
    [string]$ErlangVersion = '29.1.2',
    [string]$RabbitMqVersion = '4.4.0'
)

$ErrorActionPreference = 'Stop'

$downloads = Join-Path $ToolsRoot '_downloads'
$erlDir = Join-Path $ToolsRoot "erlang-$ErlangVersion"
$rmqDir = Join-Path $ToolsRoot "rabbitmq-$RabbitMqVersion"

New-Item -ItemType Directory -Force -Path $downloads | Out-Null

$erlZip = Join-Path $downloads "otp_win64_$ErlangVersion.zip"
$rmqZip = Join-Path $downloads "rabbitmq-server-windows-$RabbitMqVersion.zip"

if (-not (Test-Path $erlZip)) {
    Write-Host "Downloading Erlang/OTP $ErlangVersion..."
    Invoke-WebRequest -UseBasicParsing -OutFile $erlZip `
        -Uri "https://github.com/erlang/otp/releases/download/OTP-$ErlangVersion/otp_win64_$ErlangVersion.zip"
} else {
    Write-Host "Erlang zip already downloaded: $erlZip"
}

if (-not (Test-Path $rmqZip)) {
    Write-Host "Downloading RabbitMQ $RabbitMqVersion..."
    Invoke-WebRequest -UseBasicParsing -OutFile $rmqZip `
        -Uri "https://github.com/rabbitmq/rabbitmq-server/releases/download/v$RabbitMqVersion/rabbitmq-server-windows-$RabbitMqVersion.zip"
} else {
    Write-Host "RabbitMQ zip already downloaded: $rmqZip"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Expand-Flattened {
    param([string]$Zip, [string]$Destination, [string]$Marker)

    if (Test-Path (Join-Path $Destination $Marker)) {
        Write-Host "Already extracted: $Destination"
        return
    }

    if (Test-Path $Destination) { Remove-Item -Recurse -Force $Destination }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    Write-Host "Extracting $Zip ..."
    [System.IO.Compression.ZipFile]::ExtractToDirectory($Zip, $Destination)

    # Some archives have a version-named top-level folder. Flatten it so callers can use a
    # predictable path.
    if (-not (Test-Path (Join-Path $Destination $Marker))) {
        $child = Get-ChildItem $Destination -Directory | Select-Object -First 1
        if ($child -and (Test-Path (Join-Path $child.FullName $Marker))) {
            Get-ChildItem $child.FullName -Force | Move-Item -Destination $Destination -Force
            Remove-Item -Recurse -Force $child.FullName
        } else {
            throw "Could not find '$Marker' under $Destination."
        }
    }
}

Expand-Flattened -Zip $erlZip -Destination $erlDir -Marker 'bin\erl.exe'
Expand-Flattened -Zip $rmqZip -Destination $rmqDir -Marker 'sbin\rabbitmq-server.bat'

Write-Host ''
Write-Host "Erlang:   $erlDir"
Write-Host "RabbitMQ: $rmqDir"
Write-Host 'Installed. Run scripts\start-rabbitmq.ps1 to start the broker.'
