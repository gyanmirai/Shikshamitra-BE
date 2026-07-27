[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishProfile,

    [string]$HealthUri = 'https://stage-api.gyanmirai.in/.well-known/openid-configuration'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Protect-DiagnosticText {
    param([AllowEmptyString()][string]$Text)

    $protected = $Text
    $protected = $protected -replace '(?i)Bearer\s+[A-Za-z0-9._~+/-]+=*', 'Bearer [REDACTED]'
    $protected = $protected -replace '(?i)(password|pwd|secret|token|api[-_]?key|accountkey|sharedaccesssignature|user\s*id|username)\s*[:=]\s*("[^"]*"|''[^'']*''|[^;\s,}<]+)', '$1=[REDACTED]'
    $protected = $protected -replace '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}', '[REDACTED_EMAIL]'
    return $protected
}

try {
    $healthResponse = Invoke-WebRequest `
        -Uri $HealthUri `
        -Method Get `
        -TimeoutSec 20 `
        -SkipHttpErrorCheck

    if ($healthResponse.StatusCode -eq 200) {
        Write-Output 'Production API is healthy; startup diagnostics are not required.'
        exit 0
    }
}
catch {
    Write-Output 'Production API did not return a healthy response; collecting sanitized startup diagnostics.'
}

[xml]$publishData = $PublishProfile
$profile = @(
    $publishData.publishData.publishProfile |
        Where-Object { $_.publishMethod -eq 'MSDeploy' }
)[0]

if ($null -eq $profile) {
    Write-Warning 'No MSDeploy profile was available for startup diagnostics.'
    exit 0
}

$userName = [string]$profile.userName
$password = [string]$profile.userPWD
Write-Output "::add-mask::$userName"
Write-Output "::add-mask::$password"

$publishHost = ([string]$profile.publishUrl) -replace ':443$', ''
$authorization = [Convert]::ToBase64String(
    [Text.Encoding]::ASCII.GetBytes("${userName}:${password}")
)
$headers = @{ Authorization = "Basic $authorization" }
$diagnosticRoot = Join-Path $env:RUNNER_TEMP "backend-startup-$([Guid]::NewGuid().ToString('N'))"
$archivePath = Join-Path $diagnosticRoot 'startup-diagnostics.zip'
$expandedPath = Join-Path $diagnosticRoot 'expanded'

New-Item -ItemType Directory -Path $diagnosticRoot | Out-Null

try {
    Invoke-WebRequest `
        -Uri "https://${publishHost}/api/dump" `
        -Headers $headers `
        -OutFile $archivePath `
        -TimeoutSec 120
    Expand-Archive -LiteralPath $archivePath -DestinationPath $expandedPath

    $candidateFiles = @(
        Get-ChildItem -LiteralPath $expandedPath -Recurse -File |
            Where-Object {
                $_.Extension -in @('.log', '.txt', '.xml') -and
                $_.Length -le 2MB -and
                $_.FullName -notmatch '[/\\](http|kudu|analytics)[/\\]'
            } |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 20
    )

    $diagnosticLines = [Collections.Generic.List[string]]::new()
    foreach ($file in $candidateFiles) {
        $rawText = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
        if ([string]::IsNullOrWhiteSpace($rawText)) {
            continue
        }

        if ($file.Extension -eq '.xml') {
            $rawText = [Net.WebUtility]::HtmlDecode(($rawText -replace '<[^>]+>', "`n"))
        }

        $lines = @($rawText -split '\r?\n')
        $interestingIndexes = @(
            for ($index = 0; $index -lt $lines.Count; $index++) {
                if ($lines[$index] -match '(?i)(unhandled exception|failed to start|critical|exception:|fail:|error:|FileNotFoundException|TypeLoadException|NpgsqlException|PostgresException|InvalidOperationException)') {
                    $index
                }
            }
        )

        foreach ($index in $interestingIndexes) {
            $start = [Math]::Max(0, $index - 2)
            $end = [Math]::Min($lines.Count - 1, $index + 12)
            $relativePath = [IO.Path]::GetRelativePath($expandedPath, $file.FullName)
            $diagnosticLines.Add("Source: $relativePath")

            foreach ($lineIndex in $start..$end) {
                $line = Protect-DiagnosticText $lines[$lineIndex].Trim()
                if (-not [string]::IsNullOrWhiteSpace($line)) {
                    $diagnosticLines.Add($line)
                }
            }

            if ($diagnosticLines.Count -ge 220) {
                break
            }
        }

        if ($diagnosticLines.Count -ge 220) {
            break
        }
    }

    Write-Output '::group::Sanitized Azure startup diagnostics'
    if ($diagnosticLines.Count -eq 0) {
        Write-Output 'No exception lines were found in the available text startup logs.'
        Write-Output 'Available diagnostic text files:'
        $candidateFiles |
            ForEach-Object { [IO.Path]::GetRelativePath($expandedPath, $_.FullName) } |
            ForEach-Object { Write-Output $_ }
    }
    else {
        $diagnosticLines |
            Select-Object -First 220 -Unique |
            ForEach-Object { Write-Output $_ }
    }
    Write-Output '::endgroup::'
}
catch {
    Write-Warning "Azure startup diagnostics could not be collected: $($_.Exception.Message)"
}
finally {
    if (Test-Path -LiteralPath $diagnosticRoot) {
        Remove-Item -LiteralPath $diagnosticRoot -Recurse -Force
    }

    $password = $null
    $authorization = $null
    $headers = $null
}
