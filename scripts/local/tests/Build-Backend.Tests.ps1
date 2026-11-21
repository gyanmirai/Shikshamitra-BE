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
if ($env:BACKEND_BUILD_TEST_CLEANUP_FAIL -eq 'late' -and @((Get-Content $env:BACKEND_BUILD_TEST_LOG) | Where-Object { $_ -like 'cleanup:*' }).Count -eq 2) { throw 'Synthetic late cleanup failure' }
'@ | Set-Content "$fixture/scripts/local/Stop-BackendProcess.ps1"
@'
@echo off
echo build:%*>>"%BACKEND_BUILD_TEST_LOG%"
exit /b %BACKEND_BUILD_TEST_EXIT%
'@ | Set-Content "$fixture/tools/dotnet.cmd"
$testLog = "$fixture/events.txt"
$originalPath = $env:PATH
$checks = 0
foreach ($project in @('ShikshaMitra.HttpApi.Host', 'ShikshaMitra.DbMigrator')) {
    $output = "$fixture/.artifacts/vscode/bin/$project/debug"
    New-Item -ItemType Directory -Path "$output/runtimes" -Force | Out-Null
    Set-Content "$output/$project.dll" -Value 'synthetic assembly'
    Set-Content "$output/appsettings.json" -Value '{"synthetic":true}'
    Set-Content "$output/runtimes/native.dll" -Value 'synthetic native dependency'
}
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
        $project = if ($target -eq 'Host') { 'ShikshaMitra.HttpApi.Host' } else { 'ShikshaMitra.DbMigrator' }
        $runtime = "$fixture/.artifacts/vscode-runtime/$project"
        Assert-Build (Test-Path "$runtime/$project.dll") 'Debugger must launch a runtime snapshot separate from compiler outputs'
        Assert-Build (Test-Path "$runtime/runtimes/native.dll") 'Runtime snapshot must include nested dependencies'
        Assert-Build ((Get-Content "$runtime/appsettings.json" -Raw) -match 'synthetic') 'Runtime snapshot must include application configuration'
        Assert-Build (!(Test-Path "$runtime/stale.dll")) 'Snapshot replacement must discard obsolete runtime dependencies'
        $events = @(Get-Content $testLog)
        Assert-Build ($events.Count -eq 3 -and $events[0] -eq "cleanup:$target" -and $events[1].StartsWith('build:build ') -and $events[2] -eq "cleanup:$target") 'Cleanup must run before compilation and before replacing the runtime'
        Set-Content "$runtime/stale.dll" -Value 'obsolete dependency'
    }
    $env:BACKEND_BUILD_TEST_EXIT = '7'
    & powershell.exe -NoProfile -NonInteractive -File "$fixture/scripts/local/Build-Backend.ps1" -Target Host
    Assert-Build ($LASTEXITCODE -eq 7) 'Compiler failure must reach VS Code'
    Assert-Build (Test-Path "$fixture/.artifacts/vscode-runtime/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.dll") 'Compiler failure must preserve the previous runtime snapshot'
    Set-Content $testLog -Value '' -NoNewline
    $env:BACKEND_BUILD_TEST_CLEANUP_FAIL = '1'
    & powershell.exe -NoProfile -NonInteractive -File "$fixture/scripts/local/Build-Backend.ps1" -Target Host
    Assert-Build ($LASTEXITCODE -ne 0) 'Cleanup failure must fail the task'
    Assert-Build (@(Get-Content $testLog).Count -eq 1) 'Build must not run after cleanup fails'
    Set-Content $testLog -Value '' -NoNewline
    $env:BACKEND_BUILD_TEST_EXIT = '0'
    $env:BACKEND_BUILD_TEST_CLEANUP_FAIL = 'late'
    & powershell.exe -NoProfile -NonInteractive -File "$fixture/scripts/local/Build-Backend.ps1" -Target Host
    Assert-Build ($LASTEXITCODE -ne 0) 'A session appearing during compilation must block replacement if cleanup fails'
    Assert-Build (Test-Path "$fixture/.artifacts/vscode-runtime/ShikshaMitra.HttpApi.Host/stale.dll") 'Failed late cleanup must leave the previous runtime snapshot untouched'
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
