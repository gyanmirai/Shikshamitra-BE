[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ExpectedSiteName,

    [Parameter(Mandatory)]
    [string]$PublishedDirectory
)

$ErrorActionPreference = 'Stop'

$scm = & (Join-Path $PSScriptRoot 'Get-AzureScmConnection.ps1') -ExpectedSiteName $ExpectedSiteName
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
        $response = Invoke-WebRequest -Uri "$($scm.BaseUrl)/api/vfs/site/wwwroot/$name" `
            -Authentication Basic -Credential $scm.Credential -MaximumRedirection 0 -SkipHttpErrorCheck -TimeoutSec 30
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
