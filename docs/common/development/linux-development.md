# Linux development notes

## Environment

- **OS:** Pop!_OS / Ubuntu 24.04 (x64)
- **Installed SDK:** .NET 10.0.103
- **Installed Runtime:** Microsoft.NETCore.App 10.0.3
- **Project Target:** `net9.0`

> Only .NET 10 runtime is installed, but the project targets `net9.0`. This causes runtime failures unless roll-forward is enabled.

---

## 1. `dotnet ef` Tool — Install & Run

### Problem

```
dotnet-ef does not exist
```

The `dotnet-ef` global tool was not installed.

### Fix

```bash
dotnet tool install --global dotnet-ef
```

### Problem After Install

```
Framework: 'Microsoft.NETCore.App', version '9.0.0' (x64)
```

`dotnet-ef` internally targets `net8.0/net9.0` but only .NET 10 runtime is available.

### Fix — Use `DOTNET_ROLL_FORWARD`

Prefix **all** `dotnet ef` commands with:

```bash
DOTNET_ROLL_FORWARD=LatestMajor dotnet ef <command>
```

### Common Commands

**Add migration:**

```bash
DOTNET_ROLL_FORWARD=LatestMajor dotnet ef migrations add v16 \
  --project src/ShikshaMitra.EntityFrameworkCore \
  --startup-project src/ShikshaMitra.HttpApi.Host
```

**Apply migration to database:**

```bash
DOTNET_ROLL_FORWARD=LatestMajor dotnet ef database update \
  --project src/ShikshaMitra.EntityFrameworkCore \
  --startup-project src/ShikshaMitra.HttpApi.Host
```

**Remove last migration (if not applied):**

```bash
DOTNET_ROLL_FORWARD=LatestMajor dotnet ef migrations remove \
  --project src/ShikshaMitra.EntityFrameworkCore \
  --startup-project src/ShikshaMitra.HttpApi.Host
```

---

## 2. DbMigrator Project — Connection String Not Initialized

### Problem

```
[ERR] An error occurred using the connection to database '' on server ''.
System.InvalidOperationException: The ConnectionString property has not been initialized.
```

Running `dotnet run --project src/ShikshaMitra.DbMigrator` fails with empty connection string even though `appsettings.json` has a valid one.

### Root Cause

**`src/ShikshaMitra.DbMigrator/Properties/launchSettings.json`** contains:

```json
{
  "profiles": {
    "DbMigrator (Development)": {
      "environmentVariables": {
        "ConnectionStrings__Default": ""   // <-- THIS OVERRIDES appsettings.json WITH EMPTY STRING
      }
    }
  }
}
```

The empty environment variable `ConnectionStrings__Default=""` takes **highest precedence** over `appsettings.json` and `appsettings.Development.json`, effectively clearing the connection string at runtime.

### Fix Option A — Bypass launchSettings (recommended)

```bash
DOTNET_ROLL_FORWARD=LatestMajor dotnet run --project src/ShikshaMitra.DbMigrator --no-launch-profile
```

### Fix Option B — Set connection string in launchSettings

Edit `src/ShikshaMitra.DbMigrator/Properties/launchSettings.json`:

```json
"ConnectionStrings__Default": "Host=localhost;Port=5432;Database=postgres;User ID=postgres;Password=postgres;"
```

### Fix Option C — Use `dotnet ef database update` instead

Skip DbMigrator entirely and apply migrations directly:

```bash
DOTNET_ROLL_FORWARD=LatestMajor dotnet ef database update \
  --project src/ShikshaMitra.EntityFrameworkCore \
  --startup-project src/ShikshaMitra.HttpApi.Host
```

This uses `ShikshaMitraDbContextFactory` which reads directly from `DbMigrator/appsettings.json` — no launchSettings involved.

> **Note:** This only applies schema changes. It does **not** run data seeders (OpenIddict apps, admin user, etc.). Use DbMigrator for initial setup.

---

## 3. Running the HttpApi.Host

```bash
DOTNET_ROLL_FORWARD=LatestMajor dotnet run --project src/ShikshaMitra.HttpApi.Host
```

---

## 4. Building the Solution

```bash
dotnet build ShikshaMitra.sln
```

> Build works fine with .NET 10 SDK targeting `net9.0` — no roll-forward needed for build.

---

## Quick Reference

| Task | Command |
|------|---------|
| Build | `dotnet build ShikshaMitra.sln` |
| Add migration | `DOTNET_ROLL_FORWARD=LatestMajor dotnet ef migrations add vXX --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.HttpApi.Host` |
| Apply migration | `DOTNET_ROLL_FORWARD=LatestMajor dotnet ef database update --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.HttpApi.Host` |
| Run DbMigrator | `DOTNET_ROLL_FORWARD=LatestMajor dotnet run --project src/ShikshaMitra.DbMigrator --no-launch-profile` |
| Run API server | `DOTNET_ROLL_FORWARD=LatestMajor dotnet run --project src/ShikshaMitra.HttpApi.Host` |

---

## Pro Tip — Set Roll-Forward Globally

Add to your `~/.bashrc` or `~/.zshrc` to avoid typing it every time:

```bash
export DOTNET_ROLL_FORWARD=LatestMajor
```

Then just run commands normally:

```bash
dotnet ef migrations add v16 --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.HttpApi.Host
dotnet run --project src/ShikshaMitra.DbMigrator --no-launch-profile
```
