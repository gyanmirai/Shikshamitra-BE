[CmdletBinding()]
param([Parameter(Mandatory)][string]$ExpectedSiteName)

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
[pscustomobject]@{
    BaseUrl = "https://$scmEndpoint"
    Credential = [pscredential]::new($profile.userName, $password)
}
