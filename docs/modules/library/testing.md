---
module: library
domain: testing
content_type: Troubleshooting
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Validate Library backend changes

TL;DR: run the docs validator for documentation changes and targeted Library tests for behavior changes.
Do not claim Library behavior is validated unless the relevant tests or a stronger suite ran.

## Documentation checks

Run this command after editing active Library docs.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module library
```

## Focused backend tests

Use these tests as the first backend validation set.

| Risk area | Command |
|---|---|
| Core workflows | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter LibraryCoreWorkflow_Tests` |
| Reports and export behavior | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter LibraryPhase3A_Tests` |
| Notifications | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter LibraryPhase3BNotification_Tests` |
| HTTP release gate | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter LibraryReleaseGateApi_Tests` |

## Source checks

Use these checks when docs or routes change.

```powershell
rg -n "api/library|LibraryPermission|Library.*Controller" src test docs/modules/library
git diff --check
```

## Add coverage when needed

Add or update tests when a change affects these areas.

| Change | Expected coverage |
|---|---|
| Copy availability or issue status | Application workflow test |
| Public route shape | HTTP API test |
| Report export | Report service or application test |
| Notification timing or recipients | Notification application test |
| Permission behavior | HTTP API authorization test |
