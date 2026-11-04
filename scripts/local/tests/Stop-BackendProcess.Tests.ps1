#requires -Version 7.2
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$helper = Join-Path $repository 'scripts/local/Stop-BackendProcess.ps1'
$module = Join-Path $repository 'scripts/local/BackendProcess.psm1'
$checks = 0
function Assert-Guard([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:checks++
}

Assert-Guard (Test-Path -LiteralPath $helper) 'Backend cleanup is missing: a previous runtime will still lock the next VS Code build.'
Import-Module $module -Force
$cases = @(
    @{ Path = "$repository/src/ShikshaMitra.HttpApi.Host/bin/Debug/net9.0/ShikshaMitra.HttpApi.Host.dll"; Target = 'Host' },
    @{ Path = "$repository/src/ShikshaMitra.DbMigrator/bin/Debug/net9.0/ShikshaMitra.DbMigrator.dll"; Target = 'DbMigrator' },
    @{ Path = "$repository/src/ShikshaMitra.HttpApi.Host/bin/Release/net9.0/ShikshaMitra.HttpApi.Host.dll"; Target = '' },
    @{ Path = "$repository/.artifacts/runtime/bin/ShikshaMitra.HttpApi.Host/debug/ShikshaMitra.HttpApi.Host.dll"; Target = 'Host' },
    @{ Path = "${repository}-other/src/ShikshaMitra.HttpApi.Host/bin/Debug/net9.0/ShikshaMitra.HttpApi.Host.dll"; Target = '' },
    @{ Path = "$repository/src/ShikshaMitra.Application/bin/Debug/net9.0/ShikshaMitra.Application.dll"; Target = '' },
    @{ Path = "$repository/publish/ShikshaMitra.HttpApi.Host.dll"; Target = '' }
)
foreach ($case in $cases) {
    $record = [pscustomobject]@{ Name = 'dotnet.exe'; ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe'; CommandLine = '"C:\Program Files\dotnet\dotnet.exe" "' + $case.Path + '"' }
    Assert-Guard ((Get-BackendProcessTarget -Repository $repository -ProcessRecord $record) -eq $case.Target) "Unexpected process ownership for $($case.Path)"
}
$testCommand = [pscustomobject]@{ Name = 'dotnet.exe'; ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe'; CommandLine = 'dotnet test --filter ShikshaMitra.HttpApi.Host' }
Assert-Guard ([string]::IsNullOrEmpty((Get-BackendProcessTarget -Repository $repository -ProcessRecord $testCommand))) 'A test runner must never be selected.'
$decoy = [pscustomobject]@{ Name = 'other.exe'; ExecutablePath = 'C:\other.exe'; CommandLine = 'other.exe "' + $cases[0].Path + '"' }
Assert-Guard ([string]::IsNullOrEmpty((Get-BackendProcessTarget -Repository $repository -ProcessRecord $decoy))) 'A DLL mentioned in an unrelated command must not select that process.'
$appHost = [pscustomobject]@{ Name = 'ShikshaMitra.HttpApi.Host.exe'; ExecutablePath = "$repository/src/ShikshaMitra.HttpApi.Host/bin/Debug/net9.0/ShikshaMitra.HttpApi.Host.exe"; CommandLine = '' }
Assert-Guard ((Get-BackendProcessTarget -Repository $repository -ProcessRecord $appHost) -eq 'Host') 'A native apphost in the development output must also be selected.'
$spacedRoot = 'C:\Development\repo with spaces'
$spacedRecord = [pscustomobject]@{ ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe'; CommandLine = '"C:\Program Files\dotnet\dotnet.exe" "C:\Development\repo with spaces\src\ShikshaMitra.HttpApi.Host\bin\Debug\net9.0\ShikshaMitra.HttpApi.Host.dll"' }
Assert-Guard ((Get-BackendProcessTarget -Repository $spacedRoot -ProcessRecord $spacedRecord) -eq 'Host') 'Quoted checkout paths with spaces must be recognized.'
$debugger = [pscustomobject]@{ ProcessId = 100; ParentProcessId = 1; CreationDate = [datetime]'2026-11-03T01:00:00'; ExecutablePath = 'C:\Extensions\vsdbg.exe'; CommandLine = 'vsdbg --interpreter=vscode' }
$debuggee = [pscustomobject]@{ ProcessId = 101; ParentProcessId = 100; CreationDate = [datetime]'2026-11-03T01:01:00'; ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe'; CommandLine = 'dotnet "' + $cases[0].Path + '"' }
$foreignChild = [pscustomobject]@{ ProcessId = 102; ParentProcessId = 100; ExecutablePath = 'C:\Windows\System32\cmd.exe'; CommandLine = 'cmd' }
$plan = Get-BackendStopPlan -Repository $repository -Processes @($debugger, $debuggee) -Target Host
Assert-Guard ($plan.Debuggers.Count -eq 1 -and $plan.Debuggers[0].ProcessId -eq 100) 'The adapter owning the selected runtime must be included to release its DLL handles.'
$plan = Get-BackendStopPlan -Repository $repository -Processes @($debugger, $debuggee, $foreignChild) -Target Host
Assert-Guard ($plan.Debuggers.Count -eq 0) 'An adapter with another child must not be stopped.'
$plan = Get-BackendStopPlan -Repository $repository -Processes @($debugger) -Target Host
Assert-Guard ($plan.Debuggers.Count -eq 0) 'An unassociated adapter must not be stopped.'
$debugger.CreationDate = [datetime]'2026-11-03T02:00:00'
$plan = Get-BackendStopPlan -Repository $repository -Processes @($debugger, $debuggee) -Target Host
Assert-Guard ($plan.Debuggers.Count -eq 0) 'A reused parent PID must not authorize stopping a newer unrelated debugger.'
$uiDebugger = [pscustomobject]@{ ProcessId = 110; ParentProcessId = 1; CreationDate = [datetime]'2026-11-03T01:00:00'; ExecutablePath = 'C:\Extensions\vsdbg-ui.exe'; CommandLine = 'vsdbg-ui' }
$uiDebuggee = [pscustomobject]@{ ProcessId = 111; ParentProcessId = 110; CreationDate = [datetime]'2026-11-03T01:01:00'; ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe'; CommandLine = 'dotnet "' + $cases[0].Path + '"' }
$console = [pscustomobject]@{ ProcessId = 112; ParentProcessId = 110; CreationDate = [datetime]'2026-11-03T01:00:01'; ExecutablePath = (Join-Path ([Environment]::SystemDirectory) 'conhost.exe'); CommandLine = 'conhost' }
$plan = Get-BackendStopPlan -Repository $repository -Processes @($uiDebugger, $uiDebuggee, $console) -Target Host
Assert-Guard ($plan.Debuggers.Count -eq 1 -and $plan.Debuggers[0].ProcessId -eq 110) 'The current VS Code vsdbg-ui adapter with its own console host must release its handles too.'
$plan = Get-BackendStopPlan -Repository $repository -Processes @($uiDebugger, $console) -Target Host
Assert-Guard ($plan.Debuggers.Count -eq 0) 'A console host alone cannot authorize debugger cleanup.'
$plan = Get-BackendStopPlan -Repository $repository -Processes @($uiDebugger, $uiDebuggee, $console) -Target Host -RuntimeIds @()
Assert-Guard ($plan.Runtimes.Count -eq 0 -and $plan.Debuggers.Count -eq 0) 'An empty port-owner selection must not stop any worktree runtime or debugger.'
$otherDebuggee = [pscustomobject]@{ ProcessId = 113; ParentProcessId = 110; CreationDate = [datetime]'2026-11-03T01:02:00'; ExecutablePath = 'C:\Program Files\dotnet\dotnet.exe'; CommandLine = 'dotnet "' + $cases[0].Path + '"' }
$plan = Get-BackendStopPlan -Repository $repository -Processes @($uiDebugger, $uiDebuggee, $otherDebuggee, $console) -Target Host -RuntimeIds @(111)
Assert-Guard ($plan.Runtimes.Count -eq 1 -and $plan.Runtimes[0].ProcessId -eq 111) 'Only the worktree runtime occupying a selected API port may be selected.'
Assert-Guard ($plan.Debuggers.Count -eq 0) 'A debugger with a non-selected worktree runtime must remain protected.'
$ownRecord = Get-CimInstance Win32_Process -Filter "ProcessId=$PID"
$verified = Get-VerifiedBackendProcess -ProcessRecord $ownRecord
try { Assert-Guard ($verified.Id -eq $PID) 'The retained handle must identify the authorized process.' }
finally { $verified.Dispose() }
$wrongRecord = [pscustomobject]@{ ProcessId = $PID; CreationDate = $ownRecord.CreationDate.AddMinutes(1); ExecutablePath = $ownRecord.ExecutablePath }
$identityRejected = $false
try { $unexpected = Get-VerifiedBackendProcess -ProcessRecord $wrongRecord; if ($unexpected) { $unexpected.Dispose() } }
catch { $identityRejected = $_.Exception.Message -like '*changed identity*' }
Assert-Guard $identityRejected 'A creation-time mismatch must reject a recycled process ID.'

# Real runtimes prove stopping, target isolation, and checkout boundaries without
# opening a database or running application seeders.
$testRoot = Join-Path $repository ('.artifacts/process-guard-' + [guid]::NewGuid().ToString('N'))
$outsideRoot = Join-Path ([IO.Path]::GetTempPath()) ('other-checkout-' + [guid]::NewGuid().ToString('N'))
$processes = @()
try {
    New-Item -ItemType Directory -Path $testRoot, $outsideRoot | Out-Null
    $project = Join-Path $testRoot 'Fixture.csproj'
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework></PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath $project
    @'
if (args.Length > 0) {
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, int.Parse(args[0]));
    listener.Start();
}
System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
'@ | Set-Content -LiteralPath (Join-Path $testRoot 'Program.cs')
    foreach ($name in @('ShikshaMitra.HttpApi.Host', 'ShikshaMitra.DbMigrator')) {
        $output = Join-Path $testRoot "$name/bin"
        & dotnet build $project "-p:AssemblyName=$name" -o $output --nologo --verbosity quiet -m:1 -nr:false *> (Join-Path $testRoot "$name.build.log")
        if ($LASTEXITCODE -ne 0) { throw "Fixture build failed: $testRoot" }
    }
    Copy-Item -Path (Join-Path $testRoot 'ShikshaMitra.HttpApi.Host/bin/*') -Destination $outsideRoot
    foreach ($dll in @(
        (Join-Path $testRoot 'ShikshaMitra.HttpApi.Host/bin/ShikshaMitra.HttpApi.Host.dll'),
        (Join-Path $testRoot 'ShikshaMitra.DbMigrator/bin/ShikshaMitra.DbMigrator.dll'),
        (Join-Path $outsideRoot 'ShikshaMitra.HttpApi.Host.dll')
    )) {
        $processes += Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList ('"' + $dll + '"') -WindowStyle Hidden -PassThru
    }
    Start-Sleep -Milliseconds 500
    Assert-Guard (@($processes | Where-Object HasExited).Count -eq 0) 'Fixture runtimes did not start.'
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $helper -Target DbMigrator
    Assert-Guard ($LASTEXITCODE -eq 0) 'DbMigrator cleanup failed.'
    Assert-Guard ($processes[1].WaitForExit(5000)) 'DbMigrator cleanup left its matching runtime running.'
    Assert-Guard (!$processes[0].HasExited) 'DbMigrator cleanup stopped the API host.'
    Assert-Guard (!$processes[2].HasExited) 'Cleanup stopped another checkout.'
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $helper -Target Host -WhatIf
    Assert-Guard (!$processes[0].HasExited) 'WhatIf stopped the API host.'
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $helper -Target Host
    Assert-Guard ($LASTEXITCODE -eq 0) 'Host cleanup failed.'
    Assert-Guard ($processes[0].WaitForExit(5000)) 'Host cleanup left its matching runtime running.'
    Assert-Guard (!$processes[2].HasExited) 'Host cleanup stopped another checkout.'
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $helper -Target Host
    Assert-Guard ($LASTEXITCODE -eq 0) 'Repeated cleanup should succeed with no matching processes.'
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 44332)
    try {
        $listener.Start()
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $helper -Target Host *> (Join-Path $testRoot 'unknown-port.log')
        Assert-Guard ($LASTEXITCODE -ne 0) 'An unknown port owner must block launch instead of being killed.'
        Assert-Guard ($listener.Server.IsBound) 'The unknown listener must remain running.'
    }
    finally { $listener.Stop() }

    # A disposable Git repository proves that related-worktree takeover requires
    # actual registration AND ownership of an API port, not just a similar path.
    $fixtureMain = Join-Path $testRoot 'git-main'
    $fixtureWorktree = Join-Path $fixtureMain 'registered worktree'
    $fixtureUnrelated = Join-Path $testRoot 'unregistered checkout'
    New-Item -ItemType Directory -Path $fixtureMain | Out-Null
    & git -C $fixtureMain init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Fixture Git initialization failed.' }
    & git -C $fixtureMain -c user.name=GuardFixture -c user.email=guard@example.invalid commit --allow-empty --quiet -m fixture
    if ($LASTEXITCODE -ne 0) { throw 'Fixture Git commit failed.' }
    & git -C $fixtureMain worktree add --detach --quiet $fixtureWorktree HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Fixture Git worktree registration failed.' }
    $fixtureScripts = Join-Path $fixtureMain 'scripts/local'
    New-Item -ItemType Directory -Path $fixtureScripts | Out-Null
    Copy-Item -LiteralPath $helper, $module -Destination $fixtureScripts
    $fixtureHelper = Join-Path $fixtureScripts 'Stop-BackendProcess.ps1'
    foreach ($root in @($fixtureWorktree, $fixtureUnrelated)) {
        $output = Join-Path $root '.artifacts/runtime/bin'
        New-Item -ItemType Directory -Path $output -Force | Out-Null
        Copy-Item -Path (Join-Path $testRoot 'ShikshaMitra.HttpApi.Host/bin/*') -Destination $output
    }
    $relatedDll = Join-Path $fixtureWorktree '.artifacts/runtime/bin/ShikshaMitra.HttpApi.Host.dll'
    $relatedIdle = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList ('"' + $relatedDll + '"') -WindowStyle Hidden -PassThru
    $processes += $relatedIdle
    $relatedPort = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList ('"' + $relatedDll + '" 44332') -WindowStyle Hidden -PassThru
    $processes += $relatedPort
    $unrelatedDll = Join-Path $fixtureUnrelated '.artifacts/runtime/bin/ShikshaMitra.HttpApi.Host.dll'
    $unrelated = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList ('"' + $unrelatedDll + '"') -WindowStyle Hidden -PassThru
    $processes += $unrelated
    Start-Sleep -Milliseconds 700
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $fixtureHelper -Target Host *> (Join-Path $testRoot 'worktree-opt-in.log')
    Assert-Guard ($LASTEXITCODE -ne 0 -and !$relatedPort.HasExited) 'Default cleanup must preserve even a registered worktree without explicit port takeover.'
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $fixtureHelper -Target Host -IncludeRelatedWorktreePortOwners
    Assert-Guard ($LASTEXITCODE -eq 0 -and $relatedPort.WaitForExit(5000)) 'Opted-in cleanup must release the registered worktree API port owner.'
    Assert-Guard (!$relatedIdle.HasExited) 'A registered worktree API using no shared port must remain running.'
    Assert-Guard (!$unrelated.HasExited) 'An unregistered checkout must remain running.'
    $unrelated.Kill(); $unrelated.WaitForExit()
    $unrelatedPort = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList ('"' + $unrelatedDll + '" 44332') -WindowStyle Hidden -PassThru
    $processes += $unrelatedPort
    Start-Sleep -Milliseconds 700
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $fixtureHelper -Target Host -IncludeRelatedWorktreePortOwners *> (Join-Path $testRoot 'unrelated-port.log')
    Assert-Guard ($LASTEXITCODE -ne 0 -and !$unrelatedPort.HasExited) 'Even opted-in cleanup must refuse a port-owning unregistered checkout.'
    $nestedFolder = Join-Path $fixtureMain 'not-a-worktree'
    New-Item -ItemType Directory -Path $nestedFolder | Out-Null
    $fixtureCommon = Join-Path $fixtureMain '.git'
    Assert-Guard (Test-BackendWorktreeIdentity -Worktree $fixtureWorktree -CommonDirectory $fixtureCommon) 'A valid registered worktree must match its own root and the common Git directory.'
    Assert-Guard (!(Test-BackendWorktreeIdentity -Worktree $nestedFolder -CommonDirectory $fixtureCommon)) 'An enclosing Git repository must not be mistaken for the candidate worktree root.'
    Write-Output "PASS: $checks process-guard checks."
}
finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
    # Retain generated fixture files under ignored artifacts for diagnosis.
    # The external fixture is also retained rather than recursively deleting a
    # directory outside the repository from a process-control test.
}
