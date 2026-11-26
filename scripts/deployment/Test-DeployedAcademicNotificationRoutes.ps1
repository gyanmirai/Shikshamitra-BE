[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [uri]$ApiBaseUri,

    [ValidateRange(1, 12)]
    [int]$Attempts = 6,

    [ValidateRange(0, 60)]
    [int]$RetryDelaySeconds = 10
)

$ErrorActionPreference = 'Stop'

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

    throw 'Staging did not expose the Academic notification routes from this deployment.'
}
finally {
    $client.Dispose()
}
