<#
.SYNOPSIS
    Stops the portable RabbitMQ node started by scripts\start-rabbitmq.ps1.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\stop-rabbitmq.ps1
#>
[CmdletBinding()]
param(
    [string]$ToolsRoot = (Join-Path $env:USERPROFILE 'Tools'),
    [string]$ErlangVersion = '29.1.2',
    [string]$RabbitMqVersion = '4.4.0'
)

$ErrorActionPreference = 'Stop'

$erlDir = Join-Path $ToolsRoot "erlang-$ErlangVersion"
$rmqDir = Join-Path $ToolsRoot "rabbitmq-$RabbitMqVersion"

if (-not (Test-Path (Join-Path $rmqDir 'sbin\rabbitmqctl.bat'))) {
    throw "RabbitMQ was not found at $rmqDir."
}

$env:ERLANG_HOME = $erlDir
$env:PATH = "$erlDir\bin;$env:PATH"
$env:RABBITMQ_BASE = Join-Path $ToolsRoot 'rabbitmq-data'

& (Join-Path $rmqDir 'sbin\rabbitmqctl.bat') stop
