<#
.SYNOPSIS
Creates one concession policy or seeds a representative set covering every supported policy scenario.

.DESCRIPTION
AllScenarios creates deterministic, rerunnable demo data for every value source, calculation method,
status, installment scope, and rounding mode. Existing scenario codes are skipped. The caller needs
Concession Policy View and Add permissions; Delete is also required for the archived lifecycle row.

.EXAMPLE
.\scripts\finance\add-concession-policy.ps1 -BearerToken $token -AcademicYearId $yearId -AllScenarios

.EXAMPLE
.\scripts\finance\add-concession-policy.ps1 -BearerToken $token -AcademicYearId $yearId `
    -PolicyName "Sibling concession" -PolicyCode "SIBLING-10" -Status Active `
    -ValueSource PolicyFixed -CalculationMethod Percentage -PercentageValue 10
#>
[CmdletBinding(DefaultParameterSetName = "Single")]
param(
    [Parameter(Mandatory = $false)]
    [string]$BaseUrl = "http://localhost:44319",

    [Parameter(Mandatory = $true)]
    [string]$BearerToken,

    [Parameter(Mandatory = $true)]
    [Guid]$AcademicYearId,

    [Parameter(Mandatory = $true, ParameterSetName = "Single")]
    [ValidateLength(3, 128)]
    [string]$PolicyName,

    [Parameter(Mandatory = $true, ParameterSetName = "Scenarios")]
    [switch]$AllScenarios,

    [Parameter(Mandatory = $false, ParameterSetName = "Scenarios")]
    [ValidatePattern("^[A-Za-z0-9-]{1,12}$")]
    [string]$ScenarioCodePrefix = "SEED",

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [ValidateLength(0, 32)]
    [string]$PolicyCode,

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [ValidateLength(0, 500)]
    [string]$Description,

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [ValidateSet("Draft", "Active")]
    [string]$Status = "Draft",

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [ValidateSet("PolicyFixed", "RequestEnteredWithinLimits")]
    [string]$ValueSource = "RequestEnteredWithinLimits",

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [ValidateSet("Percentage", "FixedAmount", "FullWaiver")]
    [string]$CalculationMethod = "Percentage",

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [Nullable[decimal]]$PercentageValue,

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [Nullable[decimal]]$FixedAmount,

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [Nullable[decimal]]$MaximumPercentage,

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [Nullable[decimal]]$MaximumFixedAmount,

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [ValidateSet("AllOutstanding", "CurrentInstallment", "FutureInstallments")]
    [string]$ApplyOn = "AllOutstanding",

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [ValidateSet("None", "RoundUp", "RoundDown", "NearestRupee")]
    [string]$RoundingMethod = "None",

    [Parameter(Mandatory = $false, ParameterSetName = "Single")]
    [switch]$IsDefault,

    [Parameter(Mandatory = $false)]
    [Guid]$TenantId
)

$ErrorActionPreference = "Stop"
$endpoint = "/api/finance/concession-policies"

function Assert-InRange {
    param(
        [Parameter(Mandatory = $true)] [string]$Name,
        [Parameter(Mandatory = $true)] [decimal]$Value,
        [Parameter(Mandatory = $true)] [decimal]$Minimum,
        [Parameter(Mandatory = $true)] [decimal]$Maximum
    )

    if ($Value -lt $Minimum -or $Value -gt $Maximum) {
        throw "$Name must be between $Minimum and $Maximum."
    }
}

function Assert-PolicyIsValid {
    param([Parameter(Mandatory = $true)] [hashtable]$Policy)

    if ([string]::IsNullOrWhiteSpace($Policy.policyName)) {
        throw "PolicyName is required."
    }

    if ($Policy.isDefault -and $Policy.status -ne "Active") {
        throw "A default concession policy must have Status Active."
    }

    if ($Policy.valueSource -eq "PolicyFixed") {
        switch ($Policy.calculationMethod) {
            "Percentage" {
                if ($null -eq $Policy.percentageValue) {
                    throw "PercentageValue is required for a policy-fixed percentage concession."
                }
                Assert-InRange -Name "PercentageValue" -Value $Policy.percentageValue -Minimum 0.01 -Maximum 100
            }
            "FixedAmount" {
                if ($null -eq $Policy.fixedAmount -or $Policy.fixedAmount -le 0) {
                    throw "FixedAmount must be greater than zero for a policy-fixed amount concession."
                }
            }
        }
        return
    }

    if ($Policy.calculationMethod -eq "FullWaiver") {
        throw "FullWaiver can only be used with ValueSource PolicyFixed."
    }

    if ($Policy.calculationMethod -eq "Percentage") {
        if ($null -eq $Policy.maximumPercentage) {
            throw "MaximumPercentage is required for a request-entered percentage concession."
        }
        Assert-InRange -Name "MaximumPercentage" -Value $Policy.maximumPercentage -Minimum 0.01 -Maximum 100
    }

    if ($Policy.calculationMethod -eq "FixedAmount" -and
        ($null -eq $Policy.maximumFixedAmount -or $Policy.maximumFixedAmount -le 0)) {
        throw "MaximumFixedAmount must be greater than zero for a request-entered fixed concession."
    }
}

function Invoke-ConcessionPolicyApi {
    param(
        [Parameter(Mandatory = $true)] [ValidateSet("GET", "POST", "DELETE")] [string]$Method,
        [Parameter(Mandatory = $true)] [string]$Path,
        [Parameter(Mandatory = $false)] [object]$Body
    )

    $uri = "$($BaseUrl.TrimEnd('/'))$Path"
    try {
        if ($PSBoundParameters.ContainsKey("Body")) {
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $script:headers `
                -Body ($Body | ConvertTo-Json -Depth 6) -ContentType "application/json"
        }
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $script:headers
    }
    catch {
        $details = $_.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($details)) {
            $details = $_.Exception.Message
        }
        throw "$Method '$uri' failed. $details"
    }
}

function New-ConcessionPolicy {
    param([Parameter(Mandatory = $true)] [hashtable]$Policy)

    Assert-PolicyIsValid -Policy $Policy
    return Invoke-ConcessionPolicyApi -Method POST -Path $endpoint -Body $Policy
}

function Get-ExistingScenarioPolicies {
    $byCode = @{}
    foreach ($listStatus in @("Draft", "Active", "Archived")) {
        $skipCount = 0
        do {
            $query = "academicYearId=$AcademicYearId&status=$listStatus&skipCount=$skipCount&maxResultCount=100"
            $page = Invoke-ConcessionPolicyApi -Method GET -Path "$endpoint`?$query"
            $items = @($page.items)
            foreach ($item in $items) {
                $byCode[$item.policyCode.ToUpperInvariant()] = $item
            }
            $skipCount += $items.Count
        } while ($items.Count -gt 0 -and $skipCount -lt $page.totalCount)
    }
    return $byCode
}

function New-ScenarioPolicy {
    param(
        [Parameter(Mandatory = $true)] [string]$CodeSuffix,
        [Parameter(Mandatory = $true)] [string]$Name,
        [Parameter(Mandatory = $true)] [string]$ScenarioStatus,
        [Parameter(Mandatory = $true)] [string]$ScenarioValueSource,
        [Parameter(Mandatory = $true)] [string]$ScenarioCalculationMethod,
        [Parameter(Mandatory = $true)] [string]$ScenarioApplyOn,
        [Parameter(Mandatory = $true)] [string]$ScenarioRoundingMethod,
        [decimal]$ScenarioPercentageValue = 0,
        [decimal]$ScenarioFixedAmount = 0,
        [decimal]$ScenarioMaximumPercentage = 0,
        [decimal]$ScenarioMaximumFixedAmount = 0,
        [switch]$ScenarioDefault,
        [switch]$ArchiveAfterCreate
    )

    $scenarioCode = "$($ScenarioCodePrefix.ToUpperInvariant())-$CodeSuffix"
    $body = @{
        academicYearId = $AcademicYearId
        policyName = "$ScenarioCodePrefix Scenario - $Name"
        policyCode = $scenarioCode
        description = "Seeded concession-policy scenario: $Name. Safe to rerun; matching policy codes are skipped."
        status = $ScenarioStatus
        isDefault = $ScenarioDefault.IsPresent
        valueSource = $ScenarioValueSource
        calculationMethod = $ScenarioCalculationMethod
        percentageValue = if ($ScenarioPercentageValue -gt 0) { $ScenarioPercentageValue } else { $null }
        fixedAmount = if ($ScenarioFixedAmount -gt 0) { $ScenarioFixedAmount } else { $null }
        maximumPercentage = if ($ScenarioMaximumPercentage -gt 0) { $ScenarioMaximumPercentage } else { $null }
        maximumFixedAmount = if ($ScenarioMaximumFixedAmount -gt 0) { $ScenarioMaximumFixedAmount } else { $null }
        applyOn = $ScenarioApplyOn
        roundingMethod = $ScenarioRoundingMethod
    }

    return [pscustomobject]@{
        Code = $scenarioCode
        Body = $body
        ArchiveAfterCreate = $ArchiveAfterCreate.IsPresent
    }
}

if ([string]::IsNullOrWhiteSpace($BearerToken)) {
    throw "BearerToken is required."
}
if ($AcademicYearId -eq [Guid]::Empty) {
    throw "AcademicYearId cannot be empty."
}

$token = $BearerToken.Trim()
if ($token.StartsWith("Bearer ", [StringComparison]::OrdinalIgnoreCase)) {
    $token = $token.Substring(7).Trim()
}
$script:headers = @{ Authorization = "Bearer $token" }
if ($PSBoundParameters.ContainsKey("TenantId") -and $TenantId -ne [Guid]::Empty) {
    $script:headers["__tenant"] = $TenantId.ToString()
}

if ($PSCmdlet.ParameterSetName -eq "Single") {
    $body = @{
        academicYearId = $AcademicYearId
        policyName = $PolicyName.Trim()
        policyCode = if ([string]::IsNullOrWhiteSpace($PolicyCode)) { $null } else { $PolicyCode.Trim() }
        description = if ([string]::IsNullOrWhiteSpace($Description)) { $null } else { $Description.Trim() }
        status = $Status
        isDefault = $IsDefault.IsPresent
        valueSource = $ValueSource
        calculationMethod = $CalculationMethod
        percentageValue = if ($null -ne $PercentageValue) { $PercentageValue } else { $null }
        fixedAmount = if ($null -ne $FixedAmount) { $FixedAmount } else { $null }
        maximumPercentage = if ($null -ne $MaximumPercentage) { $MaximumPercentage } else { $null }
        maximumFixedAmount = if ($null -ne $MaximumFixedAmount) { $MaximumFixedAmount } else { $null }
        applyOn = $ApplyOn
        roundingMethod = $RoundingMethod
    }

    $policy = New-ConcessionPolicy -Policy $body
    Write-Host "Concession policy created: $($policy.policyCode) - $($policy.policyName) [$($policy.status)]"
    $policy
    return
}

# This is a representative scenario set, not a Cartesian product. Together the rows cover
# every supported value source, calculation method, status, apply-on option, and rounding mode.
$scenarios = @(
    New-ScenarioPolicy -CodeSuffix "PF-PCT-DEF" -Name "Default fixed 10 percent" -ScenarioStatus Active `
        -ScenarioValueSource PolicyFixed -ScenarioCalculationMethod Percentage -ScenarioPercentageValue 10 `
        -ScenarioApplyOn AllOutstanding -ScenarioRoundingMethod None -ScenarioDefault
    New-ScenarioPolicy -CodeSuffix "PF-PCT-UP" -Name "Fixed percentage rounded up" -ScenarioStatus Active `
        -ScenarioValueSource PolicyFixed -ScenarioCalculationMethod Percentage -ScenarioPercentageValue 12.5 `
        -ScenarioApplyOn CurrentInstallment -ScenarioRoundingMethod RoundUp
    New-ScenarioPolicy -CodeSuffix "PF-PCT-DOWN" -Name "Draft fixed percentage rounded down" -ScenarioStatus Draft `
        -ScenarioValueSource PolicyFixed -ScenarioCalculationMethod Percentage -ScenarioPercentageValue 7.5 `
        -ScenarioApplyOn FutureInstallments -ScenarioRoundingMethod RoundDown
    New-ScenarioPolicy -CodeSuffix "PF-AMT-NEAR" -Name "Fixed amount nearest rupee" -ScenarioStatus Active `
        -ScenarioValueSource PolicyFixed -ScenarioCalculationMethod FixedAmount -ScenarioFixedAmount 1500.75 `
        -ScenarioApplyOn AllOutstanding -ScenarioRoundingMethod NearestRupee
    New-ScenarioPolicy -CodeSuffix "PF-AMT-DRAFT" -Name "Draft fixed amount" -ScenarioStatus Draft `
        -ScenarioValueSource PolicyFixed -ScenarioCalculationMethod FixedAmount -ScenarioFixedAmount 2500 `
        -ScenarioApplyOn CurrentInstallment -ScenarioRoundingMethod None
    New-ScenarioPolicy -CodeSuffix "PF-WAIVE-ALL" -Name "Full waiver all outstanding" -ScenarioStatus Active `
        -ScenarioValueSource PolicyFixed -ScenarioCalculationMethod FullWaiver `
        -ScenarioApplyOn AllOutstanding -ScenarioRoundingMethod None
    New-ScenarioPolicy -CodeSuffix "PF-WAIVE-FUT" -Name "Draft full waiver future installments" -ScenarioStatus Draft `
        -ScenarioValueSource PolicyFixed -ScenarioCalculationMethod FullWaiver `
        -ScenarioApplyOn FutureInstallments -ScenarioRoundingMethod RoundUp
    New-ScenarioPolicy -CodeSuffix "REQ-PCT-CAP" -Name "Request percentage capped at 25" -ScenarioStatus Active `
        -ScenarioValueSource RequestEnteredWithinLimits -ScenarioCalculationMethod Percentage `
        -ScenarioMaximumPercentage 25 -ScenarioApplyOn AllOutstanding -ScenarioRoundingMethod NearestRupee
    New-ScenarioPolicy -CodeSuffix "REQ-PCT-FUT" -Name "Draft request percentage future installments" -ScenarioStatus Draft `
        -ScenarioValueSource RequestEnteredWithinLimits -ScenarioCalculationMethod Percentage `
        -ScenarioMaximumPercentage 40 -ScenarioApplyOn FutureInstallments -ScenarioRoundingMethod RoundDown
    New-ScenarioPolicy -CodeSuffix "REQ-AMT-CAP" -Name "Request amount capped at 5000" -ScenarioStatus Active `
        -ScenarioValueSource RequestEnteredWithinLimits -ScenarioCalculationMethod FixedAmount `
        -ScenarioMaximumFixedAmount 5000 -ScenarioApplyOn CurrentInstallment -ScenarioRoundingMethod RoundUp
    New-ScenarioPolicy -CodeSuffix "REQ-AMT-DRAFT" -Name "Draft request amount cap" -ScenarioStatus Draft `
        -ScenarioValueSource RequestEnteredWithinLimits -ScenarioCalculationMethod FixedAmount `
        -ScenarioMaximumFixedAmount 10000 -ScenarioApplyOn AllOutstanding -ScenarioRoundingMethod None
    New-ScenarioPolicy -CodeSuffix "ARCHIVED" -Name "Archived policy lifecycle" -ScenarioStatus Draft `
        -ScenarioValueSource PolicyFixed -ScenarioCalculationMethod Percentage -ScenarioPercentageValue 5 `
        -ScenarioApplyOn AllOutstanding -ScenarioRoundingMethod None -ArchiveAfterCreate
)

$existingByCode = Get-ExistingScenarioPolicies
$created = [System.Collections.Generic.List[object]]::new()
$skipped = [System.Collections.Generic.List[object]]::new()

foreach ($scenario in $scenarios) {
    if ($existingByCode.ContainsKey($scenario.Code)) {
        $skipped.Add($existingByCode[$scenario.Code])
        Write-Host "SKIP   $($scenario.Code) already exists."
        continue
    }

    $policy = New-ConcessionPolicy -Policy $scenario.Body
    if ($scenario.ArchiveAfterCreate) {
        $null = Invoke-ConcessionPolicyApi -Method DELETE -Path "$endpoint/$($policy.id)"
        $policy.status = "Archived"
    }
    $created.Add($policy)
    Write-Host "CREATE $($policy.policyCode) - $($policy.policyName) [$($policy.status)]"
}

Write-Host ""
Write-Host "Concession-policy scenario seed complete."
Write-Host "Created: $($created.Count)"
Write-Host "Skipped: $($skipped.Count)"
Write-Host "Covered: PolicyFixed/RequestEnteredWithinLimits; Percentage/FixedAmount/FullWaiver; Draft/Active/Archived; all ApplyOn and RoundingMethod values."

[pscustomobject]@{
    AcademicYearId = $AcademicYearId
    Created = @($created)
    Skipped = @($skipped)
}
