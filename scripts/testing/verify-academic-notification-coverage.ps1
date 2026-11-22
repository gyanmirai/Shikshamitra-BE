[CmdletBinding()]
param([switch]$NoRestore)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).ProviderPath
$docRoot = Join-Path $repoRoot 'docs/modules/academic'
$matrix = Join-Path $docRoot 'references/notification-coverage.md'
$api = Get-Content -LiteralPath (Join-Path $docRoot 'api.md')
$anchors = foreach ($line in $api) {
    if ($line -match '^#{1,6}\s+(.+)$') {
        ($Matches[1].ToLowerInvariant() -replace '[^a-z0-9 -]', '') -replace ' ', '-'
    }
}

$methods = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$eventCount = 0
$requiredChecks = @(
    'Registered_academic_events_have_a_source_and_history_policy_and_unavailable_events_have_a_reason',
    'Every_event_has_a_typed_schema_recipient_policy_and_canonical_link_owner',
    'Defaults_cover_every_catalogue_variant_as_unpublished_english_drafts_and_reinitialization_preserves_school_edits',
    'A_disabled_division_rule_overrides_an_enabled_school_rule_and_rejects_stale_changes'
)
foreach ($method in $requiredChecks) { [void]$methods.Add($method) }
foreach ($line in (Get-Content -LiteralPath $matrix)) {
    if (!$line.StartsWith('| `Academic.', [StringComparison]::Ordinal)) { continue }
    $cells = $line.Split('|')
    if ($cells.Count -ne 5) { throw "Invalid Academic notification coverage row $line" }
    $links = [regex]::Matches($cells[2], '\(\.\./api\.md#([a-z0-9-]+)\)')
    if ($links.Count -eq 0) { throw "Missing workflow contract for $($cells[1].Trim())" }
    foreach ($link in $links) {
        if ($link.Groups[1].Value -cnotin $anchors) { throw "Unknown workflow anchor $($link.Groups[1].Value)" }
    }
    $tests = [regex]::Matches($cells[3], '`([A-Za-z][A-Za-z0-9_]+)`')
    if ($tests.Count -eq 0) { throw "Missing executable evidence for $($cells[1].Trim())" }
    foreach ($testCase in $tests) { [void]$methods.Add($testCase.Groups[1].Value) }
    $eventCount += [regex]::Matches($cells[1], '`Academic\.[A-Za-z0-9.]+`').Count
}
if ($eventCount -eq 0) { throw 'The coverage table has no notification events' }

$evidenceDir = Join-Path $repoRoot ('.artifacts/academic-notifications/coverage-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $evidenceDir)
$filters = ($methods | Sort-Object | ForEach-Object { "FullyQualifiedName~.$_" }) -join '|'
$restore = @()
if ($NoRestore) { $restore += '--no-restore' }
$runs = @{ Application = 'test/ShikshaMitra.Application.Tests'; HttpApi = 'test/ShikshaMitra.HttpApi.Tests' }
$passed = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$failures = [System.Collections.Generic.List[string]]::new()

Push-Location $repoRoot
try {
    foreach ($suite in @('Application', 'HttpApi')) {
        $log = Join-Path $evidenceDir "$suite.log"
        & dotnet test $runs[$suite] @restore --filter $filters --results-directory $evidenceDir --logger "trx;LogFileName=$suite.trx" *> $log
        if ($LASTEXITCODE -ne 0) { $failures.Add("$suite test command failed. See $log") }
        Get-Content -LiteralPath $log -Tail 4
        $trx = Join-Path $evidenceDir "$suite.trx"
        if (!(Test-Path -LiteralPath $trx)) {
            $failures.Add("$suite did not produce a test report")
            continue
        }
        [xml]$report = Get-Content -LiteralPath $trx -Raw
        $definitions = @{}
        foreach ($test in $report.TestRun.TestDefinitions.UnitTest) {
            $definitions[$test.id] = [string]$test.TestMethod.name -replace '\(.*$',''
        }
        foreach ($result in $report.TestRun.Results.UnitTestResult) {
            $name = $definitions[$result.testId]
            if (!$name -or !$methods.Contains($name)) { continue }
            if ($result.outcome -eq 'Passed') { [void]$passed.Add($name) }
            else { $failures.Add("$suite case $($result.testName) ended as $($result.outcome)") }
        }
    }
} finally {
    Pop-Location
}

if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
foreach ($method in $methods) {
    if (!$passed.Contains($method)) { $failures.Add("No passing evidence for $method") }
}
if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
Write-Output "Verified $eventCount Academic notification events with $($passed.Count) passing test methods. Evidence is in $evidenceDir"
