param(
    [Parameter(Mandatory = $false)]
    [string]$BaseUrl = "http://localhost:44319",
    [Parameter(Mandatory = $true)]
    [string]$BearerToken
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $headers = @{
        Authorization = "Bearer $BearerToken"
    }

    $uri = "$BaseUrl$Path"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }

    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -Body ($Body | ConvertTo-Json -Depth 6) -ContentType "application/json"
}

$lastMonth = (Get-Date).AddMonths(-1).ToString("yyyy-MM")
Write-Host "Target month: $lastMonth"

# Force-refresh once so backend persists/refreshes monthly snapshots.
$null = Invoke-Api -Method "GET" -Path "/api/payroll/run?month=$lastMonth&page=0&pageSize=200&forceRefresh=true"

# Read persisted rows.
$preview = Invoke-Api -Method "GET" -Path "/api/payroll/run?month=$lastMonth&page=0&pageSize=200"
$rows = @($preview.items | Sort-Object employeeIdentifier, employeeName)

if ($rows.Count -lt 6) {
    throw "Need at least 6 payroll rows for mixed-status seeding. Found $($rows.Count)."
}

$groupSize = [Math]::Floor($rows.Count / 3)
if ($groupSize -lt 1) { $groupSize = 1 }

$groupA = $rows | Select-Object -First $groupSize
$remaining = $rows | Select-Object -Skip $groupSize
$groupB = $remaining | Select-Object -First $groupSize
$groupC = $remaining | Select-Object -Skip $groupSize

$groupBIds = @($groupB | ForEach-Object { [Guid]$_.employeeId })
$groupCIds = @($groupC | ForEach-Object { [Guid]$_.employeeId })

if ($groupBIds.Count -gt 0) {
    $null = Invoke-Api -Method "POST" -Path "/api/payroll/run/reviews" -Body @{
        month = $lastMonth
        employeeIds = $groupBIds
    }
}

if ($groupCIds.Count -gt 0) {
    $null = Invoke-Api -Method "POST" -Path "/api/payroll/run/reviews" -Body @{
        month = $lastMonth
        employeeIds = $groupCIds
    }

    $null = Invoke-Api -Method "POST" -Path "/api/payroll/run" -Body @{
        month = $lastMonth
        employeeIds = $groupCIds
    }
}

$finalPreview = Invoke-Api -Method "GET" -Path "/api/payroll/run?month=$lastMonth&page=0&pageSize=200"
$finalRows = @($finalPreview.items)

$pending = @($finalRows | Where-Object { $_.reviewStatus -eq "PENDING" }).Count
$reviewed = @($finalRows | Where-Object { $_.reviewStatus -eq "REVIEWED" }).Count
$locked = @($finalRows | Where-Object { $_.reviewStatus -eq "LOCKED" }).Count

Write-Host "Seed complete for $lastMonth"
Write-Host "PENDING:  $pending"
Write-Host "REVIEWED: $reviewed"
Write-Host "LOCKED:   $locked"
Write-Host "Total:    $($finalRows.Count)"
