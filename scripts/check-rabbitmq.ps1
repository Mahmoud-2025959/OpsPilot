<#
.SYNOPSIS
    Checks that the local RabbitMQ node is up and that OpsPilot's topology exists.

.DESCRIPTION
    Run after scripts\start-rabbitmq.ps1 and after the API has connected once. It checks the AMQP
    and management ports, calls the management API with the guest account, and lists the
    exchange/queue names the application declares.
#>
[CmdletBinding()]
param(
    [string]$ToolsRoot = (Join-Path $env:USERPROFILE 'Tools'),
    [string]$ErlangVersion = '29.1.2',
    [string]$RabbitMqVersion = '4.4.0'
)

$ErrorActionPreference = 'Stop'

function Test-Port {
    param([int]$Port)
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $task = $client.ConnectAsync('127.0.0.1', $Port)
        return $task.Wait(1000) -and $client.Connected
    }
    catch { return $false }
    finally { $client.Dispose() }
}

Write-Host "AMQP 5672:       $(if (Test-Port 5672) { 'open' } else { 'closed' })"
Write-Host "Management 15672: $(if (Test-Port 15672) { 'open' } else { 'closed' })"

if (Test-Port 15672) {
    $pair = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('guest:guest'))
    try {
        $overview = Invoke-RestMethod -Uri 'http://localhost:15672/api/overview' `
            -Headers @{ Authorization = "Basic $pair" } -UseBasicParsing
        Write-Host "RabbitMQ version: $($overview.rabbitmq_version)  node: $($overview.node)"
    }
    catch {
        Write-Host "Management API call failed: $($_.Exception.Message)"
    }
}

$erlDir = Join-Path $ToolsRoot "erlang-$ErlangVersion"
$rmqDir = Join-Path $ToolsRoot "rabbitmq-$RabbitMqVersion"
$env:ERLANG_HOME = $erlDir
$env:PATH = "$erlDir\bin;$env:PATH"
$env:RABBITMQ_BASE = Join-Path $ToolsRoot 'rabbitmq-data'

$ctl = Join-Path $rmqDir 'sbin\rabbitmqctl.bat'

if (Test-Port 5672) {
    Write-Host ''
    Write-Host 'Queues:'
    & $ctl list_queues name messages consumers
    Write-Host ''
    Write-Host 'Exchanges:'
    & $ctl list_exchanges name type | Select-String -Pattern 'opspilot|name'
}
