[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [uri]$ApiBaseUri,

    [Parameter(Mandatory)]
    [string]$PublishedDirectory,

    [ValidateRange(1, 12)]
    [int]$Attempts = 6,

    [ValidateRange(0, 60)]
    [int]$RetryDelaySeconds = 10
)

$ErrorActionPreference = 'Stop'

function Write-DeployedFileComparison {
    param([System.Net.Http.HttpClient]$Client)

    if ([string]::IsNullOrWhiteSpace($env:AZURE_PUBLISH_PROFILE)) {
        Write-Warning 'Azure file comparison is unavailable without the deploy publish profile.'
        return
    }

    try {
        [xml]$document = $env:AZURE_PUBLISH_PROFILE
        $profile = $document.publishData.publishProfile |
            Where-Object { $_.publishMethod -eq 'MSDeploy' } | Select-Object -First 1
        $scmEndpoint = [string]$profile.publishUrl
        if ($scmEndpoint -notmatch '^[a-z0-9-]+\.scm(?:\.[a-z0-9-]+)*\.azurewebsites\.net(?::443)?$') {
            Write-Warning 'The deploy publish profile did not identify an Azure SCM endpoint.'
            return
        }

        $basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($profile.userName):$($profile.userPWD)"))
        foreach ($name in 'ShikshaMitra.HttpApi.dll', 'ShikshaMitra.HttpApi.Host.dll', 'web.config') {
            $localPath = Join-Path $PublishedDirectory $name
            if (!(Test-Path -LiteralPath $localPath -PathType Leaf)) {
                Write-Warning "The build package does not contain $name."
                continue
            }

            $request = [System.Net.Http.HttpRequestMessage]::new('GET', "https://$scmEndpoint/api/vfs/site/wwwroot/$name")
            $response = $null
            try {
                $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Basic', $basic)
                $response = $Client.SendAsync($request).GetAwaiter().GetResult()
                if (!$response.IsSuccessStatusCode) {
                    Write-Warning "Azure SCM returned HTTP $([int]$response.StatusCode) for $name."
                    continue
                }

                $localHash = (Get-FileHash -LiteralPath $localPath -Algorithm SHA256).Hash
                $remoteHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
                    $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()))
                if ($localHash -eq $remoteHash) {
                    Write-Output "Azure wwwroot file $name matches the build package."
                }
                else {
                    Write-Warning "Azure wwwroot file $name differs from the build package."
                }
            }
            catch {
                Write-Warning "Azure file comparison failed for $name ($($_.Exception.GetType().Name))."
            }
            finally {
                if ($response) { $response.Dispose() }
                $request.Dispose()
            }
        }
    }
    catch {
        Write-Warning "Azure file comparison could not start ($($_.Exception.GetType().Name))."
    }
}

function Test-CatalogueChallenge {
    param(
        [System.Net.Http.HttpResponseMessage]$Response,
        [string]$CataloguePath
    )

    $status = [int]$Response.StatusCode
    if ($status -eq 302 -and $Response.Headers.Location) {
        $login = [uri]::new($ApiBaseUri, $Response.Headers.Location)
        $returnPath = [regex]::Escape($CataloguePath)
        return $login.Scheme -eq $ApiBaseUri.Scheme -and $login.Authority -eq $ApiBaseUri.Authority -and
            $login.AbsolutePath -eq '/Account/Login' -and
            ([uri]::UnescapeDataString($login.Query) -match "(?:[?&])ReturnUrl=$returnPath(?:&|$)")
    }
    if ($status -notin 401, 403) { return $false }
    if ($Response.Headers.WwwAuthenticate.Scheme -contains 'Bearer') { return $true }
    if ($Response.Content.Headers.ContentType.MediaType -ne 'application/problem+json') { return $false }

    $problem = $Response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json -AsHashtable
    return $problem.instance -eq $CataloguePath
}

$baseUrl = $ApiBaseUri.AbsoluteUri.TrimEnd('/')
$cataloguePath = '/api/academic/notifications/catalogue'
$requiredRoutes = [ordered]@{
    $cataloguePath = 'get'
    '/api/academic/notifications/rules' = 'get'
    '/api/academic/notifications/history' = 'get'
    '/api/academic/notifications/history/recent' = 'get'
    '/api/academic/notifications/templates/preview' = 'post'
}
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(20)

try {
    foreach ($attempt in 1..$Attempts) {
        $swaggerResponse = $null
        $catalogueResponse = $null
        try {
            $swaggerResponse = $client.GetAsync("$baseUrl/swagger/v1-academic/swagger.json").GetAwaiter().GetResult()
            $catalogueResponse = $client.GetAsync("$baseUrl$cataloguePath").GetAwaiter().GetResult()
            $swaggerStatus = [int]$swaggerResponse.StatusCode
            $catalogueStatus = [int]$catalogueResponse.StatusCode
            $missing = @($requiredRoutes.Keys)
            if ($swaggerStatus -eq 200) {
                $spec = $swaggerResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json -AsHashtable
                if ($spec.paths -is [System.Collections.IDictionary]) {
                    $missing = @($requiredRoutes.Keys | Where-Object {
                        $route = $spec.paths[$_]
                        $route -isnot [System.Collections.IDictionary] -or !$route.Contains($requiredRoutes[$_])
                    })
                }
            }
            if ($missing.Count -eq 0 -and (Test-CatalogueChallenge $catalogueResponse $cataloguePath)) {
                Write-Output "Staging Academic notification routes are published and the catalogue requires sign-in (HTTP $catalogueStatus)."
                return
            }

            Write-Output "Academic route check $attempt/$Attempts. Swagger HTTP $swaggerStatus. Catalogue HTTP $catalogueStatus. Missing routes or methods: $($missing -join ', ')."
        }
        catch {
            Write-Output "Academic route check $attempt/$Attempts failed ($($_.Exception.GetType().Name))."
        }
        finally {
            if ($swaggerResponse) { $swaggerResponse.Dispose() }
            if ($catalogueResponse) { $catalogueResponse.Dispose() }
        }
        if ($attempt -lt $Attempts) { Start-Sleep -Seconds $RetryDelaySeconds }
    }

    Write-DeployedFileComparison -Client $client
    throw 'Staging did not expose the Academic notification routes from this deployment.'
}
finally {
    $client.Dispose()
}
