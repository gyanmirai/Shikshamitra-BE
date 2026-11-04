Set-StrictMode -Version Latest

function Get-BackendProcessTarget {
    param([string]$Repository, [object]$ProcessRecord)

    if ([string]::IsNullOrWhiteSpace($ProcessRecord.ExecutablePath)) { return '' }
    $executable = [IO.Path]::GetFileName($ProcessRecord.ExecutablePath)
    $application = $ProcessRecord.ExecutablePath
    if ($executable -ieq 'dotnet.exe') {
        # Only a direct `dotnet <application.dll>` invocation is a runtime.
        # In particular, dotnet build/test and DLLs mentioned in their arguments
        # must never authorize stopping a process.
        $arguments = [regex]::Matches([string]$ProcessRecord.CommandLine, '"([^"]*)"|(\S+)')
        if ($arguments.Count -lt 2) { return '' }
        $application = $arguments[1].Value.Trim('"')
        if ([IO.Path]::GetExtension($application) -ine '.dll') { return '' }
    }
    elseif ($executable -notin @('ShikshaMitra.HttpApi.Host.exe', 'ShikshaMitra.DbMigrator.exe')) {
        return ''
    }

    if (![IO.Path]::IsPathRooted($application)) { return '' }
    $application = [IO.Path]::GetFullPath($application).Replace('/', '\')
    $root = [IO.Path]::GetFullPath($Repository).TrimEnd('\', '/') + '\'
    foreach ($entry in @(
        @{ Project = 'ShikshaMitra.HttpApi.Host'; Target = 'Host' },
        @{ Project = 'ShikshaMitra.DbMigrator'; Target = 'DbMigrator' }
    )) {
        if ([IO.Path]::GetFileNameWithoutExtension($application) -ine $entry.Project) { continue }
        $normalOutput = $root + 'src\' + $entry.Project + '\bin\Debug\'
        $artifactOutput = $root + '.artifacts\'
        if ($application.StartsWith($normalOutput, [StringComparison]::OrdinalIgnoreCase) -or
            ($application.StartsWith($artifactOutput, [StringComparison]::OrdinalIgnoreCase) -and
             $application -match '\\bin\\' -and $application -notmatch '\\release\\')) {
            return $entry.Target
        }
    }
    return ''
}

function Get-BackendStopPlan {
    param([string]$Repository, [object[]]$Processes, [string]$Target, [int[]]$RuntimeIds)

    $limitIds = $PSBoundParameters.ContainsKey('RuntimeIds')
    $runtimes = @($Processes | Where-Object {
        (Get-BackendProcessTarget -Repository $Repository -ProcessRecord $_) -eq $Target -and
            (!$limitIds -or $_.ProcessId -in $RuntimeIds)
    })
    $selectedIds = @($runtimes | ForEach-Object { $_.ProcessId })
    $debuggers = @($Processes | Where-Object {
        $candidate = $_
        if ([IO.Path]::GetFileName([string]$candidate.ExecutablePath) -notin @('vsdbg.exe', 'vsdbg-ui.exe')) { return $false }
        $candidateCreated = $candidate.PSObject.Properties['CreationDate']
        if (!$candidateCreated -or !$candidateCreated.Value) { return $false }
        $children = @($Processes | Where-Object { $_.ParentProcessId -eq $candidate.ProcessId })
        # Newer C# extensions use vsdbg-ui plus a Windows console host. Require
        # at least one matching runtime and no unrelated child/session.
        $hasRuntime = @($children | Where-Object {
            $_.ProcessId -in $selectedIds
        }).Count -gt 0
        return $hasRuntime -and @($children | Where-Object {
            $childCreated = $_.PSObject.Properties['CreationDate']
            $isConsole = $_.ExecutablePath -ieq (Join-Path ([Environment]::SystemDirectory) 'conhost.exe')
            !$childCreated -or !$childCreated.Value -or $childCreated.Value -lt $candidateCreated.Value -or
            (!$isConsole -and $_.ProcessId -notin $selectedIds)
        }).Count -eq 0
    })
    [pscustomobject]@{ Runtimes = $runtimes; Debuggers = $debuggers }
}

function Test-BackendWorktreeIdentity {
    param([string]$Worktree, [string]$CommonDirectory)

    $top = @(& git -C $Worktree rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or $top.Count -ne 1 -or
        [IO.Path]::GetFullPath($top[0]).TrimEnd('\', '/') -ine [IO.Path]::GetFullPath($Worktree).TrimEnd('\', '/')) { return $false }
    $common = @(& git -C $Worktree rev-parse --path-format=absolute --git-common-dir 2>$null)
    return $LASTEXITCODE -eq 0 -and $common.Count -eq 1 -and
        [IO.Path]::GetFullPath($common[0]).TrimEnd('\', '/') -ieq [IO.Path]::GetFullPath($CommonDirectory).TrimEnd('\', '/')
}

function Get-BackendRegisteredWorktrees {
    param([string]$Repository)

    $common = @(& git -C $Repository rev-parse --path-format=absolute --git-common-dir)
    if ($LASTEXITCODE -ne 0 -or $common.Count -ne 1) { throw 'Cannot verify the backend Git repository.' }
    $commonPath = [IO.Path]::GetFullPath($common[0]).TrimEnd('\', '/')
    $listing = @(& git -C $Repository worktree list --porcelain -z)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify registered backend worktrees.' }
    foreach ($field in (($listing -join "`n").Split([char]0))) {
        if (!$field.StartsWith('worktree ')) { continue }
        $worktree = [IO.Path]::GetFullPath($field.Substring(9)).TrimEnd('\', '/')
        if (!(Test-Path -LiteralPath $worktree -PathType Container)) { continue }
        if (Test-BackendWorktreeIdentity -Worktree $worktree -CommonDirectory $commonPath) {
            $worktree
        }
    }
}

function Get-VerifiedBackendProcess {
    param([object]$ProcessRecord)

    try { $running = [Diagnostics.Process]::GetProcessById($ProcessRecord.ProcessId) }
    catch [ArgumentException] { return $null }
    try {
        # Pin the OS handle BEFORE checking identity. Kill/WaitForExit on this
        # Process then use that same handle even if Windows recycles its PID.
        [void]$running.Handle
        if ($running.HasExited) { $running.Dispose(); return $null }
        if ($null -eq $ProcessRecord.CreationDate -or
            $running.StartTime.ToUniversalTime().ToString('yyyyMMddHHmmssffffff') -ne
                $ProcessRecord.CreationDate.ToUniversalTime().ToString('yyyyMMddHHmmssffffff') -or
            $running.MainModule.FileName -ine $ProcessRecord.ExecutablePath) {
            throw "Process $($ProcessRecord.ProcessId) changed identity; cleanup stopped for safety."
        }
        return $running
    }
    catch { $running.Dispose(); throw }
}

Export-ModuleMember -Function Get-BackendProcessTarget, Get-BackendStopPlan, Test-BackendWorktreeIdentity, Get-BackendRegisteredWorktrees, Get-VerifiedBackendProcess
