[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishProfile,

    [Parameter(Mandatory)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$remoteDirectory = '/home/site/dbmigrator'

function Protect-MigratorOutput {
    param([AllowEmptyString()][string]$Text)

    $protected = $Text
    $protected = $protected -replace '(?i)Bearer\s+[A-Za-z0-9._~+/-]+=*', 'Bearer [REDACTED]'
    $protected = $protected -replace '(?i)(password|pwd|secret|token|api[-_]?key|accountkey|sharedaccesssignature|user\s*id|username)\s*[:=]\s*("[^"]*"|''[^'']*''|[^;\s,}<]+)', '$1=[REDACTED]'
    $protected = $protected -replace '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}', '[REDACTED_EMAIL]'
    return $protected
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Database migrator package was not found at '$PackagePath'."
}

[xml]$publishData = $PublishProfile
$profile = @(
    $publishData.publishData.publishProfile |
        Where-Object { $_.publishMethod -eq 'MSDeploy' }
)[0]

if ($null -eq $profile) {
    throw 'No MSDeploy profile was available for the database migration.'
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
$escapedDirectory = [Uri]::EscapeDataString($remoteDirectory)
$commandUri = "https://${publishHost}/api/command"

try {
    Write-Output 'Uploading the database migrator to the protected App Service workspace.'
    Invoke-WebRequest `
        -Uri "https://${publishHost}/api/publish?type=zip&path=${escapedDirectory}&async=false" `
        -Method Post `
        -Headers $headers `
        -InFile $PackagePath `
        -ContentType 'application/zip' `
        -TimeoutSec 300 | Out-Null

    $migrationCommand = @{
        command = 'ASPNETCORE_ENVIRONMENT=Staging DOTNET_ENVIRONMENT=Staging SeedTestLogins=true dotnet ShikshaMitra.DbMigrator.dll'
        dir = $remoteDirectory
    } | ConvertTo-Json

    Write-Output 'Running database migrations and seeders inside the App Service network.'
    $result = Invoke-RestMethod `
        -Uri $commandUri `
        -Method Post `
        -Headers $headers `
        -ContentType 'application/json' `
        -Body $migrationCommand `
        -TimeoutSec 900

    $safeOutput = Protect-MigratorOutput ([string]$result.Output)
    if (-not [string]::IsNullOrWhiteSpace($safeOutput)) {
        $safeOutput -split '\r?\n' |
            Select-Object -Last 120 |
            ForEach-Object { Write-Output $_ }
    }

    if ([int]$result.ExitCode -ne 0) {
        $safeError = Protect-MigratorOutput ([string]$result.Error)
        throw "Database migrator exited with code $($result.ExitCode). $safeError"
    }

    Write-Output 'Database migrations and seeders completed successfully.'
}
finally {
    try {
        $cleanupCommand = @{
            command = "rm -rf '$remoteDirectory'"
            dir = '/home/site'
        } | ConvertTo-Json

        Invoke-RestMethod `
            -Uri $commandUri `
            -Method Post `
            -Headers $headers `
            -ContentType 'application/json' `
            -Body $cleanupCommand `
            -TimeoutSec 120 | Out-Null
        Write-Output 'Removed the temporary database migrator workspace.'
    }
    catch {
        Write-Warning 'The temporary database migrator workspace could not be removed automatically.'
    }

    $password = $null
    $authorization = $null
    $headers = $null
}
