<#
.SYNOPSIS
    Starts the portable RabbitMQ node and its management plugin.

.DESCRIPTION
    Reads the install created by scripts\install-rabbitmq-local.ps1, starts the node detached by
    default, waits for AMQP port 5672, and prints the management URL.

.PARAMETER Foreground
    Keep the Erlang console attached to this shell instead of detaching.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\start-rabbitmq.ps1
#>
[CmdletBinding()]
param(
    [string]$ToolsRoot = (Join-Path $env:USERPROFILE 'Tools'),
    [string]$ErlangVersion = '29.1.2',
    [string]$RabbitMqVersion = '4.4.0',
    [switch]$Foreground
)

$ErrorActionPreference = 'Stop'

$erlDir = Join-Path $ToolsRoot "erlang-$ErlangVersion"
$rmqDir = Join-Path $ToolsRoot "rabbitmq-$RabbitMqVersion"
$dataDir = Join-Path $ToolsRoot 'rabbitmq-data'

if (-not (Test-Path (Join-Path $erlDir 'bin\erl.exe'))) {
    throw "Erlang was not found at $erlDir. Run scripts\install-rabbitmq-local.ps1 first."
}

if (-not (Test-Path (Join-Path $rmqDir 'sbin\rabbitmq-server.bat'))) {
    throw "RabbitMQ was not found at $rmqDir. Run scripts\install-rabbitmq-local.ps1 first."
}

# The scripts discover Erlang from the registry when this is not set; setting it explicitly is
# what lets the portable copy work without a machine-wide install.
$env:ERLANG_HOME = $erlDir
$env:PATH = "$erlDir\bin;$env:PATH"

# Keeps the node's database, config and logs under the tools folder instead of %APPDATA%.
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$env:RABBITMQ_BASE = $dataDir

$server = Join-Path $rmqDir 'sbin\rabbitmq-server.bat'
$plugins = Join-Path $rmqDir 'sbin\rabbitmq-plugins.bat'

# Idempotent: enabling an already-enabled plugin is a no-op.
& $plugins enable rabbitmq_management | Out-Null

if ($Foreground) {
    Write-Host 'Starting RabbitMQ in the foreground...'
    & $server
    return
}

Write-Host 'Starting RabbitMQ detached...'
$process = Start-Process -FilePath $server -WindowStyle Hidden -PassThru -Wait:$false

# Wait for the AMQP listener rather than sleeping a fixed time: a first start on a cold machine
# takes noticeably longer than a warm one.
function Test-AmqpPort {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $task = $client.ConnectAsync('127.0.0.1', 5672)
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
    if (Test-AmqpPort) {
        Write-Host ''
        Write-Host 'RabbitMQ is listening on 5672.'
        Write-Host 'Management UI: http://localhost:15672  (guest / guest)'
        Write-Host 'Node data:     ' $dataDir
        return
    }
    Start-Sleep -Milliseconds 500
}

throw "RabbitMQ did not open port 5672 within two minutes. Check the logs under $dataDir\log."
