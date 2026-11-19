#requires -Version 7.0
# Local DDVS demo only. Run from the backend repository root; the script prompts for
# the existing user's password and never writes credentials or tokens to disk.
# pwsh -NoProfile -File scripts/local/Seed-DemoAccountsSettings.ps1
param(
    [Guid]$AcademicYearId = '3a2373c7-3243-28ac-e5d1-9c926b661769',
    [string]$Tenant = 'ddvs',
    [string]$UserName = 'admin-ddvs',
    [string]$EffectiveFrom = '2026-04-01',
    [switch]$SkipBank
)

$ErrorActionPreference = 'Stop'

function Read-FrontendEnvironment([string]$frontendRoot) {
    $settings = @{}
    foreach ($filename in @('.env', '.env.local')) {
        $path = Join-Path $frontendRoot $filename
        if (-not (Test-Path -LiteralPath $path)) { continue }
        foreach ($line in Get-Content -LiteralPath $path) {
            if ($line -notmatch '^\s*(NEXT_PUBLIC_[A-Z_]+)\s*=\s*(.*?)\s*$') { continue }
            $settings[$matches[1]] = $matches[2].Trim().Trim("'", '"')
        }
    }
    return $settings
}

$frontendRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../Shikshamitra-FE')).Path
$settings = Read-FrontendEnvironment $frontendRoot
$apiBase = $settings['NEXT_PUBLIC_API_URL'].TrimEnd('/')
$apiUri = [Uri]$apiBase
if ($apiUri.Host -notin @('localhost', '127.0.0.1')) {
    throw 'Demo settings may only be seeded through a localhost API.'
}
foreach ($name in @('NEXT_PUBLIC_CLIENT_ID', 'NEXT_PUBLIC_CLIENT_SECRET', 'NEXT_PUBLIC_SCOPE')) {
    if ([string]::IsNullOrWhiteSpace($settings[$name])) { throw "Missing $name in frontend environment." }
}
if ($AcademicYearId -eq [Guid]::Empty -or [string]::IsNullOrWhiteSpace($Tenant)) {
    throw 'Tenant and academic year are required.'
}

$securePassword = Read-Host "Password for $UserName in tenant $Tenant" -AsSecureString
$plainPassword = [System.Net.NetworkCredential]::new('', $securePassword).Password
try {
    $tokenResult = Invoke-RestMethod -Uri "$apiBase/connect/token" -Method Post `
        -ContentType 'application/x-www-form-urlencoded' -SkipCertificateCheck -TimeoutSec 45 `
        -Headers @{ __tenant = $Tenant } -Body @{
            grant_type = 'password'
            username = $UserName
            password = $plainPassword
            client_id = $settings['NEXT_PUBLIC_CLIENT_ID']
            client_secret = $settings['NEXT_PUBLIC_CLIENT_SECRET']
            scope = $settings['NEXT_PUBLIC_SCOPE']
        }
} finally {
    $plainPassword = $null
    $securePassword = $null
}
if ([string]::IsNullOrWhiteSpace($tokenResult.access_token)) { throw 'Local sign-in returned no access token.' }
$headers = @{ Authorization = "Bearer $($tokenResult.access_token)"; __tenant = $Tenant }

function Invoke-AccountsApi([string]$method, [string]$path, $body = $null) {
    $options = @{
        Uri = "$apiBase$path"
        Method = $method
        Headers = $headers
        SkipCertificateCheck = $true
        TimeoutSec = 45
    }
    if ($null -ne $body) {
        $options.ContentType = 'application/json'
        $options.Body = ConvertTo-Json -InputObject $body -Depth 8 -Compress
    }
    try { return Invoke-RestMethod @options }
    catch {
        $status = [int]$_.Exception.Response.StatusCode
        throw "$method $path failed (HTTP $status). No additional settings will be created."
    }
}

$yearQuery = "academicYearId=$AcademicYearId"
$heads = [ordered]@{}
$headDefinitions = @(
    @{ Name = 'School Bank'; Type = 'Asset'; Note = 'Demo school bank balance' },
    @{ Name = 'Cash on Hand'; Type = 'Asset'; Note = 'Demo school cash balance' },
    @{ Name = 'UPI Clearing'; Type = 'Asset'; Note = 'Demo UPI awaiting settlement' },
    @{ Name = 'Refund Payable'; Type = 'Liability'; Note = 'Demo refunds awaiting payout' },
    @{ Name = 'Tuition Fee Income'; Type = 'Income'; Note = 'Demo tuition fee income' },
    @{ Name = 'Transport Fee Income'; Type = 'Income'; Note = 'Demo transport fee income' },
    @{ Name = 'Other Income'; Type = 'Income'; Note = 'Demo non-fee income' },
    @{ Name = 'Electricity Expense'; Type = 'Expense'; Note = 'Demo electricity costs' },
    @{ Name = 'Stationery Expense'; Type = 'Expense'; Note = 'Demo stationery costs' }
)

foreach ($definition in $headDefinitions) {
    $search = [Uri]::EscapeDataString($definition.Name)
    $result = Invoke-AccountsApi 'GET' "/api/account-heads?$yearQuery&page=1&pageSize=100&search=$search"
    $matches = @($result.items | Where-Object { $_.headName -eq $definition.Name })
    if ($matches.Count -gt 1) { throw "Multiple account heads named '$($definition.Name)' exist; resolve them first." }
    if ($matches.Count -eq 1) {
        $head = $matches[0]
        if ([string]$head.headType -ne $definition.Type -and [int]$head.headType -ne @{
            Asset = 1; Liability = 2; Income = 4; Expense = 5
        }[$definition.Type]) { throw "'$($definition.Name)' exists with a different account type." }
        if (-not $head.isActive) { throw "'$($definition.Name)' exists but is inactive." }
        Write-Output "Reused head: $($definition.Name)"
    } else {
        $head = Invoke-AccountsApi 'POST' "/api/account-heads?$yearQuery" @{
            headName = $definition.Name
            headType = $definition.Type
            parentAccountHeadId = $null
            notes = $definition.Note
        }
        Write-Output "Created head: $($definition.Name)"
    }
    $heads[$definition.Name] = $head.id
}

$categoryDefinitions = @(
    @{ Path = 'income-categories'; Name = 'Tuition Fees'; Head = 'Tuition Fee Income' },
    @{ Path = 'income-categories'; Name = 'Transport Fees'; Head = 'Transport Fee Income' },
    @{ Path = 'income-categories'; Name = 'Donations'; Head = 'Other Income' },
    @{ Path = 'expense-categories'; Name = 'Electricity'; Head = 'Electricity Expense' },
    @{ Path = 'expense-categories'; Name = 'Stationery'; Head = 'Stationery Expense' }
)

foreach ($definition in $categoryDefinitions) {
    $search = [Uri]::EscapeDataString($definition.Name)
    $path = "/api/$($definition.Path)"
    $result = Invoke-AccountsApi 'GET' "${path}?$yearQuery&page=1&pageSize=100&search=$search"
    $matches = @($result.items | Where-Object { $_.name -eq $definition.Name })
    if ($matches.Count -gt 1) { throw "Multiple categories named '$($definition.Name)' exist; resolve them first." }
    if ($matches.Count -eq 1) {
        if ($matches[0].defaultAccountHeadId -ne $heads[$definition.Head]) {
            throw "Category '$($definition.Name)' exists with a different account head."
        }
        if (-not $matches[0].isActive) { throw "Category '$($definition.Name)' exists but is inactive." }
        Write-Output "Reused category: $($definition.Name)"
    } else {
        $null = Invoke-AccountsApi 'POST' "${path}?$yearQuery" @{
            name = $definition.Name
            defaultAccountHeadId = $heads[$definition.Head]
            description = "Demo mapping to $($definition.Head)"
            tags = @()
        }
        Write-Output "Created category: $($definition.Name)"
    }
}

if (-not $SkipBank) {
    $bankName = 'Demo Bank'
    $search = [Uri]::EscapeDataString($bankName)
    $result = Invoke-AccountsApi 'GET' "/api/bank-accounts?$yearQuery&page=1&pageSize=100&search=$search"
    $matches = @($result.items | Where-Object { $_.bankName -eq $bankName })
    if ($matches.Count -gt 1) { throw "Multiple '$bankName' accounts exist; resolve them first." }
    if ($matches.Count -eq 1) {
        if ($matches[0].accountHeadId -ne $heads['School Bank'] -or
            $matches[0].maskedAccountNumber -notmatch '1234$') {
            throw 'An existing Demo Bank account has different details; it was not changed.'
        }
        Write-Output "Reused bank account: $bankName (ending 1234)"
    } else {
        $null = Invoke-AccountsApi 'POST' "/api/bank-accounts?$yearQuery" @{
            accountHeadId = $heads['School Bank']
            bankName = $bankName
            branchName = 'Demo City Branch'
            branchAddress = 'Demo branch address'
            accountHolderName = 'DDVS School Demo'
            accountType = 1
            accountNumber = '000000001234'
            purpose = 'DEMO ONLY - Never use for real collections or payouts'
            ifscCode = 'TEST0001234'
            upiId = $null
            effectiveFrom = "$($EffectiveFrom)T00:00:00+05:30"
            effectiveTo = $null
        }
        Write-Output "Created bank account: $bankName (ending 1234)"
    }
}

Write-Output "Demo Accounts Settings ready for tenant $Tenant and academic year $AcademicYearId."
