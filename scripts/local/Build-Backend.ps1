#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Host', 'DbMigrator')][string]$Target,
    [ValidateNotNullOrEmpty()][string]$ArtifactsPath = '.artifacts/vscode'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
$compilerArtifacts = [IO.Path]::GetFullPath([IO.Path]::Combine($repository, $ArtifactsPath))
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
            --artifacts-path $compilerArtifacts `
            -m:1 -nr:false -p:UseSharedCompilation=false `
            /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
        $buildExitCode = $LASTEXITCODE
        if ($buildExitCode -eq 0) {
            # Never run the debugger from compiler output. Stage the full
            # successful output first, then replace the stopped runtime copy.
            $buildExitCode = 1
            $output = Join-Path $compilerArtifacts "bin/$projectName/debug"
            if (!(Test-Path -LiteralPath (Join-Path $output "$projectName.dll"))) {
                throw 'The build did not produce the expected backend assembly.'
            }
            $runtimeRoot = [IO.Path]::GetFullPath((Join-Path $repository '.artifacts/vscode-runtime'))
            $runtime = [IO.Path]::GetFullPath((Join-Path $runtimeRoot $projectName))
            if (!$runtime.StartsWith($runtimeRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Unexpected runtime output path.'
            }
            $staging = Join-Path $runtimeRoot ('.staging/' + [Guid]::NewGuid())
            New-Item -ItemType Directory -Path $staging -Force | Out-Null
            Get-ChildItem -LiteralPath $output -Force | Copy-Item -Destination $staging -Recurse -Force
            # A session could have been launched while compilation was running.
            & powershell.exe @cleanupArguments
            if ($LASTEXITCODE -ne 0) { throw 'Runtime cleanup failed; the previous runtime snapshot was preserved.' }
            $backup = Join-Path $repository ('.artifacts/vscode-runtime-history/' + $projectName + '-' + [Guid]::NewGuid())
            if (Test-Path -LiteralPath $runtime) {
                if ((Get-Item -LiteralPath $runtime).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                    throw 'Runtime output must not be a junction or symbolic link.'
                }
                New-Item -ItemType Directory -Path (Split-Path $backup) -Force | Out-Null
                Move-Item -LiteralPath $runtime -Destination $backup
            }
            try { Move-Item -LiteralPath $staging -Destination $runtime }
            catch {
                if ((Test-Path -LiteralPath $backup) -and !(Test-Path -LiteralPath $runtime)) {
                    Move-Item -LiteralPath $backup -Destination $runtime
                }
                throw
            }
            Write-Output "Backend runtime ready: $(Join-Path $runtime "$projectName.dll")"
            $buildExitCode = 0
        }
    }
    finally { Pop-Location }
}
catch { Write-Error -Message $_.Exception.Message -ErrorAction Continue }
finally {
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
exit $buildExitCode
