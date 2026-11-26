[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Enter', 'Exit')]
    [string]$Mode
)

$ErrorActionPreference = 'Stop'

if ($env:GITHUB_RUN_ID -notmatch '^\d+$' -or $env:GITHUB_RUN_ATTEMPT -notmatch '^\d+$') {
    throw 'A GitHub deployment run ID and attempt are required.'
}

$scm = & (Join-Path $PSScriptRoot 'Get-AzureScmConnection.ps1') -ExpectedSiteName 'shikshamitra-dev'
$offlineRequest = @{
    Uri = "$($scm.BaseUrl)/api/vfs/site/wwwroot/app_offline.htm"
    Authentication = 'Basic'
    Credential = $scm.Credential
    MaximumRedirection = 0
    SkipHttpErrorCheck = $true
    TimeoutSec = 20
}
$marker = "shikshamitra-staging-deploy:$($env:GITHUB_RUN_ID):$($env:GITHUB_RUN_ATTEMPT)"
$markerPattern = '<!-- shikshamitra-staging-deploy:\d+:\d+ -->'
$offlineFile = Invoke-WebRequest @offlineRequest

if ($offlineFile.StatusCode -notin 200, 404) {
    throw "Azure SCM returned HTTP $($offlineFile.StatusCode) while checking staging maintenance."
}

if ($Mode -eq 'Exit') {
    if ($offlineFile.StatusCode -eq 404 -or !$offlineFile.Content.Contains("<!-- $marker -->")) { return }

    $etag = @($offlineFile.Headers.ETag)[0]
    if (!$etag) { throw 'Azure SCM did not return an ETag for the staging maintenance file.' }
    $result = Invoke-WebRequest @offlineRequest -Method Delete -Headers @{ 'If-Match' = $etag }
    if ($result.StatusCode -notin 200, 204, 404) {
        throw "Azure SCM returned HTTP $($result.StatusCode) while resuming staging."
    }

    Write-Output 'Removed the staging maintenance file for this deployment.'
    return
}

$headers = @{}
if ($offlineFile.StatusCode -eq 200) {
    if ($offlineFile.Content -notmatch $markerPattern) {
        throw 'Staging already has a maintenance file from another source.'
    }
    # Main-only workflow concurrency prevents an earlier run from deploying at the same time.
    $etag = @($offlineFile.Headers.ETag)[0]
    if (!$etag) { throw 'Azure SCM did not return an ETag for the staging maintenance file.' }
    $headers['If-Match'] = $etag
}

$body = "<!doctype html><title>Staging maintenance</title><p>A staging update is in progress.</p><!-- $marker -->"
$result = Invoke-WebRequest @offlineRequest -Method Put -Headers $headers -ContentType 'text/html; charset=utf-8' -Body $body
if ($result.StatusCode -notin 200, 201, 204) {
    throw "Azure SCM returned HTTP $($result.StatusCode) while pausing staging."
}

foreach ($attempt in 1..6) {
    try {
        $probe = Invoke-WebRequest -Uri 'https://stage-api.gyanmirai.in/.well-known/openid-configuration' `
            -MaximumRedirection 0 -SkipHttpErrorCheck -TimeoutSec 10
        if ($probe.StatusCode -eq 503 -and $probe.Content.Contains("<!-- $marker -->")) {
            # IIS can serve the maintenance page while the previous in-process app is still shutting down.
            Start-Sleep -Seconds 15
            Write-Output 'IIS is serving the staging maintenance page for this deployment.'
            return
        }
    }
    catch {
        Write-Output "The staging maintenance probe did not respond on attempt $attempt."
    }
    if ($attempt -lt 6) { Start-Sleep -Seconds 3 }
}

throw 'Staging did not enter maintenance mode. No release files were copied.'
