---
module: common
domain: development
content_type: How-to
status: canonical
updated_on: 2026-10-31
owner: backend-team
audience: humans-and-agents
---

# Build the backend under local memory pressure

TL;DR: use the serialized local build helper when the large EF migration history exhausts compiler memory.
It preserves all source files, migration designers, nullable analysis and analyzers.

Run from the backend repository with PowerShell 7.2 or later and the SDK selected by `global.json`:

```powershell
pwsh -NoProfile -File scripts/local/Build-BackendLowMemory.ps1
pwsh -NoProfile -File scripts/local/Build-BackendLowMemory.ps1 -Project test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj -ArtifactsPath .artifacts/verification
```

The default project is HttpApi.Host and the default output is `.artifacts/local-memory-build`.
Use `-Configuration Release` when required; use `-NoRestore` only after restoring the same project graph into the same artifacts path.
The helper builds project references normally, returns the child build exit code and does not stop existing hosts or compiler servers.
Avoid building the same project/output concurrently, and wait for the complete graph before testing its reference assemblies.

The child process uses one MSBuild worker, standalone serialized compilation (`backend-compiler.rsp`), one reported processor, workstation GC and increased memory conservation.
Inherited `DOTNET`/`COMPlus` GC heap hard limits are removed only from the child environment; parent settings and application configuration are unchanged.
Fixed 2/4/6 GiB caps were insufficient for this checkout's generated model.
No source exclusions, disabled analyzers, prebuilt dependency substitutions or migration edits are part of this helper.

The October 30 local full EF source build, before warning cleanup, completed with zero errors and 12 warnings in 25m08s.
An observed compiler private-memory sample was approximately 10.4 GiB on a 24 GiB desktop; this is one local observation, not a guaranteed peak or production timing.
Cold compilation can remain slow under paging; incremental builds are much cheaper.
See [Student validation](../../modules/student/testing.md#student-records-permission-menu-alignment) for the associated permission-check evidence and its release boundaries.

## Preserve behavior when correcting compiler diagnostics

Keep nullable analysis and analyzers enabled, and verify each affected project with the normal compiler before declaring warnings resolved.
Request fields retain their current validation, requiredness and runtime defaults; accurately nullable response metadata must be checked for reuse as an input.
Framework-populated fields need a traced binding, mapping or ORM initialization boundary rather than invented default values.
Optional navigation annotations must match the existing FK mapping; run `CashManagementDeploymentModel_Tests` in its own test process to check the complete model against the unchanged snapshot.
`AdmissionProviderTimeMapping_Tests` covers the existing PostgreSQL and fallback time-conversion branches.

Preserve constructor dependency signatures and base forwarding when replacing primary-constructor captures.
Changes to EF string comparisons must retain provider translation, normalization and null semantics; a new `StringComparison` overload is not automatically SQL-compatible.
Quote SQL identifiers with the provider's `ISqlGenerationHelper` and keep data values parameterized.
Compare broad regression failures against a retained pre-change assembly before classifying them as new defects; record existing failures separately instead of changing business rules to make a cleanup gate pass.
