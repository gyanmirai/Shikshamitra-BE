#requires -Version 7.2
[CmdletBinding()]
param(
    [string]$Project = 'src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$ArtifactsPath = '.artifacts/local-memory-build',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$buildRepository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$buildProject = [System.IO.Path]::GetFullPath($Project, $buildRepository)
if (!(Test-Path -LiteralPath $buildProject -PathType Leaf) -or
    [System.IO.Path]::GetExtension($buildProject) -ne '.csproj') {
    throw 'Project must identify an existing backend .csproj file.'
}

$buildStart = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
$buildStart.UseShellExecute = $false
$buildStart.CreateNoWindow = $true
$buildStart.RedirectStandardOutput = $true
$buildStart.RedirectStandardError = $true
$buildStart.WorkingDirectory = $buildRepository
foreach ($buildArgument in @(
    'build', $buildProject, '--configuration', $Configuration,
    '--artifacts-path', [System.IO.Path]::GetFullPath($ArtifactsPath, $buildRepository),
    '-m:1', '-p:BuildInParallel=false', '-p:UseSharedCompilation=false',
    ('-p:CompilerResponseFile=' + (Join-Path $PSScriptRoot 'backend-compiler.rsp'))
)) {
    $buildStart.ArgumentList.Add($buildArgument)
}
if ($NoRestore) { $buildStart.ArgumentList.Add('--no-restore') }

# Keep all sources, nullable analysis and analyzers enabled. Large generated EF
# models need more live memory than the old 2/4/6 GiB verification caps allowed.
# Scope adaptive GC and serialization to this child build, never the app/OS.
foreach ($buildEnvironmentKey in @($buildStart.Environment.Keys)) {
    if ($buildEnvironmentKey -match '^(DOTNET|COMPlus)_GCHeapHardLimit') {
        [void]$buildStart.Environment.Remove($buildEnvironmentKey)
    }
}
$buildStart.Environment['DOTNET_PROCESSOR_COUNT'] = '1'
$buildStart.Environment['DOTNET_GCConserveMemory'] = '9'
$buildStart.Environment['DOTNET_gcServer'] = '0'

Write-Output 'Building with serialized compilation and adaptive GC; all compiler checks remain enabled.'
$buildProcess = [System.Diagnostics.Process]::Start($buildStart)
$buildErrors = $buildProcess.StandardError.ReadToEndAsync()
try {
    while ($null -ne ($buildLine = $buildProcess.StandardOutput.ReadLine())) {
        Write-Output $buildLine
    }
    $buildProcess.WaitForExit()
    $buildErrorText = $buildErrors.GetAwaiter().GetResult()
    if (![string]::IsNullOrWhiteSpace($buildErrorText)) { Write-Output $buildErrorText }
    $buildExitCode = $buildProcess.ExitCode
}
finally {
    if (!$buildProcess.HasExited) {
        $buildProcess.Kill($true)
        $buildProcess.WaitForExit()
    }
    $buildProcess.Dispose()
}
exit $buildExitCode
