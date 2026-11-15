<#
.SYNOPSIS
    A minimal local webhook receiver for demoing and testing OpsPilot's webhook channel.

.DESCRIPTION
    Listens on a loopback port (5099 by default), accepts one HTTP POST at a time, prints the
    OpsPilot headers and the JSON body, answers 200, and appends the call to a log file.

    It deliberately uses a raw TcpListener rather than System.Net.HttpListener: HttpListener
    needs a URL ACL on Windows unless the shell is elevated, and a demo receiver should not
    need administrator rights.

.PARAMETER Port
    Port to listen on. Default 5099, matching the URL in OPS_FLOW.http.

.PARAMETER LogPath
    File every received call is appended to. Default webhook-receiver.log beside the repo root.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\webhook-receiver.ps1

    Then register http://localhost:5099/webhook as a webhook endpoint and create an incident.
#>
[CmdletBinding()]
param(
    [int]$Port = 5099,
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

# Resolve the default log path here rather than in the parameter block: Windows PowerShell 5.1
# can bind parameters before $PSScriptRoot is populated, which made the default expression fail
# with "Cannot bind argument to parameter 'Path' because it is an empty string".
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $scriptDirectory = if ($PSScriptRoot) {
        $PSScriptRoot
    }
    elseif ($PSCommandPath) {
        Split-Path -Parent $PSCommandPath
    }
    else {
        (Get-Location).Path
    }

    $LogPath = Join-Path (Split-Path $scriptDirectory -Parent) 'webhook-receiver.log'
}

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()

Write-Host "Webhook receiver listening on http://localhost:$Port/webhook"
Write-Host "Logging calls to $LogPath"
Write-Host 'Press Ctrl+C to stop.'
Write-Host ''

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()

        try {
            $stream = $client.GetStream()

            $reader = [System.IO.StreamReader]::new(
                $stream,
                [System.Text.Encoding]::UTF8,
                $false,
                1024,
                $true)

            $requestLine = $reader.ReadLine()
            $headers = @{}

            while ($null -ne ($line = $reader.ReadLine()) -and $line -ne '') {
                $separator = $line.IndexOf(':')
                if ($separator -gt 0) {
                    $headers[$line.Substring(0, $separator).Trim()] = $line.Substring($separator + 1).Trim()
                }
            }

            $length = 0
            if ($headers.ContainsKey('Content-Length')) {
                [void][int]::TryParse($headers['Content-Length'], [ref]$length)
            }

            $body = ''
            if ($length -gt 0) {
                $buffer = [char[]]::new($length)
                $read = 0
                while ($read -lt $length) {
                    $count = $reader.Read($buffer, $read, $length - $read)
                    if ($count -le 0) { break }
                    $read += $count
                }
                $body = [string]::new($buffer, 0, $read)
            }

            $receivedAt = (Get-Date).ToString('o')

            Write-Host "[$receivedAt] $requestLine"
            foreach ($name in ($headers.Keys | Sort-Object)) {
                if ($name -like 'X-OpsPilot-*' -or $name -eq 'Content-Type') {
                    Write-Host "  $name`: $($headers[$name])"
                }
            }
            if ($body) { Write-Host "  body: $body" }
            Write-Host ''

            $record = [pscustomobject]@{
                receivedAt = $receivedAt
                requestLine = $requestLine
                headers = $headers
                body = $body
            }
            $record | ConvertTo-Json -Depth 6 -Compress | Add-Content -LiteralPath $LogPath -Encoding utf8

            $reply = '{"received":true}'
            $replyBytes = [System.Text.Encoding]::UTF8.GetBytes($reply)
            $head = "HTTP/1.1 200 OK`r`n" +
                    "Content-Type: application/json`r`n" +
                    "Content-Length: $($replyBytes.Length)`r`n" +
                    "Connection: close`r`n`r`n"
            $headBytes = [System.Text.Encoding]::ASCII.GetBytes($head)

            $stream.Write($headBytes, 0, $headBytes.Length)
            $stream.Write($replyBytes, 0, $replyBytes.Length)
            $stream.Flush()
        }
        catch {
            Write-Warning "A call could not be handled: $($_.Exception.Message)"
        }
        finally {
            $client.Dispose()
        }
    }
}
finally {
    $listener.Stop()
}
