[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ExpectedSiteName,

    [Parameter(Mandatory)]
    [string]$PublishedDirectory
)

$ErrorActionPreference = 'Stop'

try {
    [xml]$document = $env:AZURE_PUBLISH_PROFILE
}
catch {
    throw 'The Azure publish profile could not be parsed.'
}

$profile = $document.publishData.publishProfile |
    Where-Object { $_.publishMethod -eq 'MSDeploy' } | Select-Object -First 1
$scmEndpoint = [string]$profile.publishUrl
$site = [regex]::Escape($ExpectedSiteName)
if ($scmEndpoint -notmatch "^$site(?:-[a-z0-9]+)?\.scm(?:\.[a-z0-9-]+)*\.azurewebsites\.net(?::443)?$") {
    throw 'The Azure publish profile does not match the expected App Service SCM host.'
}

if (!$profile.userName -or !$profile.userPWD) {
    throw 'The Azure publish profile is missing its deployment credentials.'
}

$password = ConvertTo-SecureString -String $profile.userPWD -AsPlainText -Force
$credential = [pscredential]::new($profile.userName, $password)
$files = @(
    'ShikshaMitra.HttpApi.Host.dll',
    'ShikshaMitra.HttpApi.dll',
    'ShikshaMitra.Application.dll',
    'ShikshaMitra.Application.Contracts.dll',
    'web.config'
)

foreach ($name in $files) {
    $localPath = Join-Path $PublishedDirectory $name
    if (!(Test-Path -LiteralPath $localPath -PathType Leaf)) {
        throw "The backend build package does not contain $name."
    }

    try {
        $response = Invoke-WebRequest -Uri "https://$scmEndpoint/api/vfs/site/wwwroot/$name" `
            -Authentication Basic -Credential $credential -MaximumRedirection 0 -SkipHttpErrorCheck -TimeoutSec 30
    }
    catch {
        throw "Could not read $name from the expected Azure SCM host ($($_.Exception.GetType().Name))."
    }

    if ($response.StatusCode -ne 200) {
        throw "Azure SCM returned HTTP $($response.StatusCode) while verifying $name."
    }

    $localHash = (Get-FileHash -LiteralPath $localPath -Algorithm SHA256).Hash
    $response.RawContentStream.Position = 0
    $remoteHash = (Get-FileHash -InputStream $response.RawContentStream -Algorithm SHA256).Hash
    if ($localHash -ne $remoteHash) {
        throw "Azure wwwroot file $name differs from the build package."
    }

    Write-Output "Azure wwwroot file $name matches the build package."
}
