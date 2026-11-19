param(
    [Parameter(Mandatory = $true)]
    [string]$WebRoot
)

$ErrorActionPreference = 'Stop'
$libsPath = Join-Path $WebRoot 'libs'
$assets = @(Get-ChildItem -LiteralPath $libsPath -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in '.js', '.css' -and $_.Length -gt 0 })

if ($assets.Count -eq 0) {
    throw "ABP web libraries are missing from '$libsPath'. Run 'abp install-libs' in src/ShikshaMitra.HttpApi.Host before building or publishing."
}

foreach ($requiredAsset in @('abp/core/abp.js', 'abp/core/abp.css', 'jquery/jquery.js', 'bootstrap/css/bootstrap.css')) {
    $assetPath = Join-Path $libsPath $requiredAsset
    if (!(Test-Path -LiteralPath $assetPath -PathType Leaf) -or (Get-Item -LiteralPath $assetPath).Length -eq 0) {
        throw "Required ABP web asset is missing or empty: '$assetPath'. Run 'abp install-libs' in src/ShikshaMitra.HttpApi.Host."
    }
}

Write-Output "Validated $($assets.Count) ABP JavaScript/CSS assets in '$libsPath'."
