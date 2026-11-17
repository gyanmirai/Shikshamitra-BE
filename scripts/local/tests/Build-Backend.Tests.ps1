#requires -Version 7.2
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$builder = Join-Path $repository 'scripts/local/Build-Backend.ps1'
if (!(Test-Path $builder)) { throw 'The build command must include cleanup itself, including when VS Code reruns the task.' }
$fixture = Join-Path $repository ('.artifacts/build-command-checks/' + [Guid]::NewGuid())
New-Item -ItemType Directory -Path "$fixture/scripts/local", "$fixture/tools" -Force | Out-Null
Copy-Item -LiteralPath $builder -Destination "$fixture/scripts/local/Build-Backend.ps1"
@'
param([string]$Target, [switch]$IncludeRelatedWorktreePortOwners)
Add-Content -LiteralPath $env:BACKEND_BUILD_TEST_LOG -Value "cleanup:$Target"
if ($env:BACKEND_BUILD_TEST_CLEANUP_FAIL -eq '1') { throw 'Synthetic cleanup failure' }
'@ | Set-Content "$fixture/scripts/local/Stop-BackendProcess.ps1"
@'
@echo off
echo build:%*>>"%BACKEND_BUILD_TEST_LOG%"
exit /b %BACKEND_BUILD_TEST_EXIT%
'@ | Set-Content "$fixture/tools/dotnet.cmd"
$testLog = "$fixture/events.txt"
$originalPath = $env:PATH
$checks = 0
function Assert-Build([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }; $script:checks++
}
try {
    $env:PATH = "$fixture/tools;$originalPath"
    $env:BACKEND_BUILD_TEST_LOG = $testLog
    $env:BACKEND_BUILD_TEST_EXIT = '0'
    $env:BACKEND_BUILD_TEST_CLEANUP_FAIL = '0'
    foreach ($target in @('Host', 'Host', 'DbMigrator')) {
        Set-Content $testLog -Value '' -NoNewline
        & powershell.exe -NoProfile -NonInteractive -File "$fixture/scripts/local/Build-Backend.ps1" -Target $target
        Assert-Build ($LASTEXITCODE -eq 0) "$target build failed"
        $events = @(Get-Content $testLog)
        Assert-Build ($events.Count -eq 2 -and $events[0] -eq "cleanup:$target" -and $events[1].StartsWith('build:build ')) 'Cleanup must precede every build, including reruns'
    }
    $env:BACKEND_BUILD_TEST_EXIT = '7'
    & powershell.exe -NoProfile -NonInteractive -File "$fixture/scripts/local/Build-Backend.ps1" -Target Host
    Assert-Build ($LASTEXITCODE -eq 7) 'Compiler failure must reach VS Code'
    Set-Content $testLog -Value '' -NoNewline
    $env:BACKEND_BUILD_TEST_CLEANUP_FAIL = '1'
    & powershell.exe -NoProfile -NonInteractive -File "$fixture/scripts/local/Build-Backend.ps1" -Target Host
    Assert-Build ($LASTEXITCODE -ne 0) 'Cleanup failure must fail the task'
    Assert-Build (@(Get-Content $testLog).Count -eq 1) 'Build must not run after cleanup fails'
    $env:BACKEND_BUILD_TEST_CLEANUP_FAIL = '0'
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($fixture.ToLowerInvariant()))).Replace('-', '') }
    finally { $sha.Dispose() }
    $mutex = [Threading.Mutex]::new($false, "Local\ShikshaMitraBuild-$hash")
    try {
        [void]$mutex.WaitOne()
        Set-Content $testLog -Value '' -NoNewline
        & powershell.exe -NoProfile -NonInteractive -File "$fixture/scripts/local/Build-Backend.ps1" -Target DbMigrator
        Assert-Build ($LASTEXITCODE -ne 0) 'Concurrent Host/DbMigrator builds must be refused'
        Assert-Build ((Get-Item $testLog).Length -eq 0) 'A refused build must not stop a runtime'
    }
    finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
    Write-Output "$checks build command checks passed. No real backend or database was started."
}
finally {
    $env:PATH = $originalPath
    foreach ($key in @('BACKEND_BUILD_TEST_LOG', 'BACKEND_BUILD_TEST_EXIT', 'BACKEND_BUILD_TEST_CLEANUP_FAIL')) {
        [Environment]::SetEnvironmentVariable($key, $null, 'Process')
    }
}
