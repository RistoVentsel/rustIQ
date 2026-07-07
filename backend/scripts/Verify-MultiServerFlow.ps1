param(
    [string]$ApiBaseUrl = "http://localhost:5009",
    [int]$ServerSampleCount = 3
)

$ErrorActionPreference = "Stop"

function Invoke-RustIqJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    return Invoke-RestMethod -Uri $Url -Headers @{ "Accept" = "application/json" }
}

Write-Host "Checking RustIQ multi-server flow against $ApiBaseUrl"

$serversPage = Invoke-RustIqJson -Url "$ApiBaseUrl/servers?page=1&pageSize=$ServerSampleCount"

if (-not $serversPage.items -or $serversPage.items.Count -eq 0) {
    throw "GET /servers returned no items. Start the worker and confirm configured servers are being seeded."
}

Write-Host "Visible servers: $($serversPage.items.Count) / Total cached: $($serversPage.totalItems)"

$sampleServers = $serversPage.items | Select-Object -First $ServerSampleCount
foreach ($server in $sampleServers) {
    Write-Host ""
    Write-Host "Checking server $($server.id) [$($server.host):$($server.queryPort)]"

    $details = Invoke-RustIqJson -Url "$ApiBaseUrl/servers/$($server.id)"
    $players = Invoke-RustIqJson -Url "$ApiBaseUrl/servers/$($server.id)/players"
    $map = Invoke-RustIqJson -Url "$ApiBaseUrl/servers/$($server.id)/map"
    $snapshots = Invoke-RustIqJson -Url "$ApiBaseUrl/servers/$($server.id)/snapshots?limit=5"

    Write-Host "  Status: $($details.status)"
    Write-Host "  Players: $($details.players)/$($details.maxPlayers)"
    Write-Host "  Snapshot count returned: $($snapshots.Count)"
    Write-Host "  Player rows returned: $($players.players.Count)"
    Write-Host "  Map present: $([bool]$map)"
}

Write-Host ""
Write-Host "REST verification passed."
Write-Host "Next manual checks:"
Write-Host "  1. Connect a client to $ApiBaseUrl/hubs/servers"
Write-Host "  2. Watch one server id"
Write-Host "  3. Confirm only that server receives ServerUpdated messages"
