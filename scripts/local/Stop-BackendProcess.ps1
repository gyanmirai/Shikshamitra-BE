#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Host', 'DbMigrator')][string]$Target,
    [switch]$IncludeRelatedWorktreePortOwners
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This helper manages Windows development processes only.'
}
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $PSScriptRoot 'BackendProcess.psm1') -Force
$processes = @(Get-CimInstance Win32_Process)
$plan = Get-BackendStopPlan -Repository $repository -Processes $processes -Target $Target
$scopes = @([pscustomobject]@{ Repository = $repository; Plan = $plan; RuntimeIds = $null })
if ($IncludeRelatedWorktreePortOwners) {
    if ($Target -ne 'Host') { throw 'Related-worktree port cleanup applies only to the API Host.' }
    $portOwners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop |
        Where-Object { $_.LocalPort -in 44332, 5000 } | Select-Object -ExpandProperty OwningProcess -Unique)
    foreach ($worktree in @(Get-BackendRegisteredWorktrees -Repository $repository)) {
        if ($worktree.TrimEnd('\', '/') -ieq $repository.TrimEnd('\', '/')) { continue }
        $worktreePlan = Get-BackendStopPlan -Repository $worktree -Processes $processes -Target Host -RuntimeIds $portOwners
        $scopes += [pscustomobject]@{ Repository = $worktree; Plan = $worktreePlan; RuntimeIds = $portOwners }
    }
}

# Debugger first: it can hold DLL/symbol handles as well as the runtime itself.
foreach ($scope in $scopes) {
foreach ($record in @($scope.Plan.Debuggers) + @($scope.Plan.Runtimes)) {
    $current = Get-CimInstance Win32_Process -Filter "ProcessId=$($record.ProcessId)"
    if (!$current) { continue }
    if ($current.CreationDate -ne $record.CreationDate -or $current.ExecutablePath -ne $record.ExecutablePath) {
        throw "Process $($record.ProcessId) changed identity; cleanup stopped for safety."
    }
    if ([IO.Path]::GetFileName($current.ExecutablePath) -in @('vsdbg.exe', 'vsdbg-ui.exe')) {
        $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($record.ProcessId)")
        $planArguments = @{ Repository = $scope.Repository; Processes = (@($current) + $children); Target = $Target }
        if ($null -ne $scope.RuntimeIds) { $planArguments.RuntimeIds = $scope.RuntimeIds }
        $currentPlan = Get-BackendStopPlan @planArguments
        if (@($currentPlan.Debuggers).Count -ne 1) { throw 'The debug adapter no longer exclusively owns this backend target; cleanup stopped for safety.' }
    }
    elseif ((Get-BackendProcessTarget -Repository $scope.Repository -ProcessRecord $current) -ne $Target) {
        throw "Process $($record.ProcessId) no longer belongs to this backend target."
    }
    if ($PSCmdlet.ShouldProcess("$Target development process $($record.ProcessId)", 'Stop before build')) {
        $running = Get-VerifiedBackendProcess -ProcessRecord $record
        if ($running) {
            try {
                $running.Kill()
                if (!$running.WaitForExit(8000)) { throw "Process $($record.ProcessId) did not exit." }
            }
            finally { $running.Dispose() }
        }
        Write-Output "Stopped $Target development process $($record.ProcessId)."
    }
}
}
if ($WhatIfPreference) { return }

if ($Target -eq 'Host') {
    $listeners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object { $_.LocalPort -in 44332, 5000 })
    if ($listeners.Count -gt 0) {
        $owners = ($listeners.OwningProcess | Sort-Object -Unique) -join ', '
        throw "Backend ports 44332/5000 are still owned by unverified process(es): $owners. No unrelated process was stopped."
    }
}
Write-Output "$Target development output is ready for build."
