param(
    [Parameter(Mandatory = $false)]
    [string]$BaseUrl = "http://localhost:44319",
    [Parameter(Mandatory = $true)]
    [string]$BearerToken,
    [Parameter(Mandatory = $false)]
    [string]$Month = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Month)) {
    $Month = (Get-Date).AddMonths(-1).ToString("yyyy-MM")
}

$outDir = Join-Path $PSScriptRoot "..\..\artifacts\payroll-e2e"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $headers = @{ Authorization = "Bearer $BearerToken" }
    $uri = "$BaseUrl$Path"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -Body ($Body | ConvertTo-Json -Depth 8) -ContentType "application/json"
}

Write-Host "Validating month: $Month"

# 1) Health check (schema readiness)
$health = Invoke-RestMethod -Method GET -Uri "$BaseUrl/health/payroll-snapshot"
$health | ConvertTo-Json -Depth 8 | Out-File (Join-Path $outDir "health.json") -Encoding utf8

# 2) Force refresh and read preview
$preview1 = Invoke-Api -Method GET -Path "/api/payroll/run?month=$Month&page=0&pageSize=200&forceRefresh=true"
$preview2 = Invoke-Api -Method GET -Path "/api/payroll/run?month=$Month&page=0&pageSize=200"
$preview1 | ConvertTo-Json -Depth 12 | Out-File (Join-Path $outDir "preview_force_refresh.json") -Encoding utf8
$preview2 | ConvertTo-Json -Depth 12 | Out-File (Join-Path $outDir "preview_readback.json") -Encoding utf8

$rows = @($preview2.items)
if ($rows.Count -eq 0) {
    throw "No payroll rows returned for $Month."
}

$pending = @($rows | Where-Object { $_.reviewStatus -eq "PENDING" })
$reviewed = @($rows | Where-Object { $_.reviewStatus -eq "REVIEWED" })

if ($pending.Count -gt 0) {
    $ids = @($pending | Select-Object -First ([Math]::Min(3, $pending.Count)) | ForEach-Object { [Guid]$_.employeeId })
    $markRes = Invoke-Api -Method POST -Path "/api/payroll/run/reviews" -Body @{ month = $Month; employeeIds = $ids }
    $markRes | ConvertTo-Json -Depth 8 | Out-File (Join-Path $outDir "mark_reviewed.json") -Encoding utf8
}

$afterMark = Invoke-Api -Method GET -Path "/api/payroll/run?month=$Month&page=0&pageSize=200"
$afterMark | ConvertTo-Json -Depth 12 | Out-File (Join-Path $outDir "after_mark_preview.json") -Encoding utf8

$reviewedNow = @($afterMark.items | Where-Object { $_.reviewStatus -eq "REVIEWED" })
if ($reviewedNow.Count -gt 0) {
    $lockIds = @($reviewedNow | Select-Object -First ([Math]::Min(2, $reviewedNow.Count)) | ForEach-Object { [Guid]$_.employeeId })
    $lockRes = Invoke-Api -Method POST -Path "/api/payroll/run" -Body @{ month = $Month; employeeIds = $lockIds }
    $lockRes | ConvertTo-Json -Depth 8 | Out-File (Join-Path $outDir "lock_reviewed.json") -Encoding utf8
}

$final = Invoke-Api -Method GET -Path "/api/payroll/run?month=$Month&page=0&pageSize=200"
$final | ConvertTo-Json -Depth 12 | Out-File (Join-Path $outDir "final_preview.json") -Encoding utf8

$summary = [PSCustomObject]@{
    Month = $Month
    Total = @($final.items).Count
    Pending = @($final.items | Where-Object { $_.reviewStatus -eq "PENDING" }).Count
    Reviewed = @($final.items | Where-Object { $_.reviewStatus -eq "REVIEWED" }).Count
    Locked = @($final.items | Where-Object { $_.reviewStatus -eq "LOCKED" }).Count
    ArtifactsPath = (Resolve-Path $outDir).Path
}
$summary | ConvertTo-Json | Out-File (Join-Path $outDir "summary.json") -Encoding utf8
$summary | Format-List
