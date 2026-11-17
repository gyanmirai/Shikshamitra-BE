#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Host', 'DbMigrator')][string]$Target
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
$sha = [Security.Cryptography.SHA256]::Create()
try { $hash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($repository.ToLowerInvariant()))).Replace('-', '') }
finally { $sha.Dispose() }
# Both targets share intermediate/reference outputs. Refuse a second build
# before stopping anything, rather than allowing concurrent writes to them.
$mutex = [Threading.Mutex]::new($false, "Local\ShikshaMitraBuild-$hash")
$ownsMutex = $false
$buildExitCode = 1
try {
    try { $ownsMutex = $mutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $ownsMutex = $true }
    if (!$ownsMutex) { throw 'Another backend build is already running in this checkout. Wait for it to finish.' }

    # Keep cleanup in the command itself: rerunning the terminal task must
    # execute it again, even when task dependencies are not replayed.
    $cleanupArguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', (Join-Path $PSScriptRoot 'Stop-BackendProcess.ps1'), '-Target', $Target)
    if ($Target -eq 'Host') { $cleanupArguments += '-IncludeRelatedWorktreePortOwners' }
    & powershell.exe @cleanupArguments
    if ($LASTEXITCODE -ne 0) { throw 'Backend cleanup failed; compilation was not started.' }

    $projectName = if ($Target -eq 'Host') { 'ShikshaMitra.HttpApi.Host' } else { 'ShikshaMitra.DbMigrator' }
    Push-Location -LiteralPath $repository
    try {
        & dotnet build (Join-Path $repository "src/$projectName/$projectName.csproj") `
            --artifacts-path (Join-Path $repository '.artifacts/vscode') `
            -m:1 -nr:false -p:UseSharedCompilation=false `
            /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
        $buildExitCode = $LASTEXITCODE
    }
    finally { Pop-Location }
}
catch { Write-Error -Message $_.Exception.Message -ErrorAction Continue }
finally {
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
exit $buildExitCode
