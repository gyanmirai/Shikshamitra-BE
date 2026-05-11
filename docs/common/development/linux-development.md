# Linux development notes

## Copilot Testing Workflow — Activation Code: `957400 Authorization`

When the user shares a curl command copied from the browser (which includes cookies, `authorization: Bearer ...`, and other headers), Copilot should:

1. **Strip** the `authorization: Bearer ...` header entirely
2. **Strip** all `-b '...'` cookie headers
3. **Replace** the hostname with `https://localhost:44332`
4. **Add** `-k` flag (skip SSL verification)
5. **Do NOT add any Authorization header** — the `DevAutoAuthMiddleware` handles auth automatically

**This workflow is only activated when the user says: `957400 Authorization logic`**

### Example transformation

**Original (from browser):**
```bash
curl 'https://stg-gyanmirai-be.azurewebsites.net/api/app/academic-year' \
  -H 'authorization: Bearer eyJhbGci...' \
  -b 'ARRAffinitySameSite=abc...'
```

**Transformed (for local testing):**
```bash
curl -sk 'https://localhost:44332/api/app/academic-year'
```

---

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

## 3. Testing APIs Without a Token (Dev Auto-Auth Middleware)

During local development, the server automatically injects a Bearer token and tenant header into every request that has no `Authorization` header. You never need to manually obtain or pass a token.

### How it works

- Middleware: `src/ShikshaMitra.HttpApi.Host/Middleware/DevAutoAuthMiddleware.cs`
- Only active when `ASPNETCORE_ENVIRONMENT=Development` (already set in `launchSettings.json`)
- Authenticates as `admin` in the `ddvs` tenant on every unauthenticated request
- Token is cached in memory and refreshed automatically before expiry

### Start the server

```bash
cd src/ShikshaMitra.HttpApi.Host && dotnet run
# or from repo root:
dotnet run --project src/ShikshaMitra.HttpApi.Host
```

Default URL: `https://localhost:44332`

### Test any endpoint — no token needed

```bash
curl -sk https://localhost:44332/api/app/academic-year
```

That's it. No `Authorization` header required. You will get a real `200` response authenticated as the `ddvs` admin.

### Configuration (appsettings.Development.json)

```json
"DevAutoAuth": {
  "Username": "admin",
  "Password": "Temp@123",
  "Tenant": "ddvs",
  "ClientId": "ShikshaMitra_App"
}
```

To change the user or tenant, edit `src/ShikshaMitra.HttpApi.Host/appsettings.Development.json` and restart the server.

### Skip auto-auth for a specific request

Pass your own `Authorization` header and the middleware will step aside:

```bash
TOKEN=$(curl -sk https://localhost:44332/connect/token \
  -H "__tenant: ddvs" \
  -d "grant_type=password&username=admin&password=Temp%40123&client_id=ShikshaMitra_App&scope=openid+profile+email+roles+offline_access+ShikshaMitra" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

curl -sk https://localhost:44332/api/app/academic-year \
  -H "Authorization: Bearer $TOKEN"
```

### Notes

- The middleware skips `/connect/*` paths (token endpoint) to avoid recursion
- It has no effect in `Staging` or `Production` — only `Development`
- `__tenant` is sent as an **HTTP header** to the token endpoint (not a form field)

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

## 5. Setting `ASPNETCORE_ENVIRONMENT`

### Option A — Prefix a single command (recommended)

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile
```

### Option B — Export for the current terminal session

```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --no-launch-profile   # all commands in this session inherit it
```

### Option C — Set permanently in `~/.bashrc`

```bash
echo 'export ASPNETCORE_ENVIRONMENT=Development' >> ~/.bashrc
source ~/.bashrc
```

### Switching between Local and Staging DB

The API always runs on `https://localhost:44332` locally. The only thing that changes is the connection string in `appsettings.Development.json`:

| Target | `ConnectionStrings.Default` |
|--------|-----------------------------|
| Local DB | `Host=localhost;Port=5432;Database=shikshamitra_local;...` |
| Staging DB | `Host=stage-gyanmirai.postgres.database.azure.com;Port=5432;Database=stg_gyanmirai;User ID=postgresadmin;Password=...` |

`ASPNETCORE_ENVIRONMENT` stays `Development` in both cases when running locally.

> **Swagger OAuth with Staging DB:** `https://localhost:44332/swagger/oauth2-redirect.html` is registered as an allowed redirect URI in the staging OpenIddict DB, so Swagger authentication works even when pointed at staging. If you re-run DbMigrator against staging, use `ASPNETCORE_ENVIRONMENT=Staging`.

---

## Quick Reference

| Task | Command |
|------|---------|
| Build | `dotnet build ShikshaMitra.sln` |
| Add migration | `DOTNET_ROLL_FORWARD=LatestMajor dotnet ef migrations add vXX --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.HttpApi.Host` |
| Apply migration | `DOTNET_ROLL_FORWARD=LatestMajor dotnet ef database update --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.HttpApi.Host` |
| Run DbMigrator (local) | `DOTNET_ROLL_FORWARD=LatestMajor dotnet run --project src/ShikshaMitra.DbMigrator --no-launch-profile` |
| Run DbMigrator (staging) | `ASPNETCORE_ENVIRONMENT=Staging DOTNET_ROLL_FORWARD=LatestMajor dotnet run --project src/ShikshaMitra.DbMigrator --no-launch-profile` |
| Run API server | `ASPNETCORE_ENVIRONMENT=Development DOTNET_ROLL_FORWARD=LatestMajor dotnet run --project src/ShikshaMitra.HttpApi.Host` |

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
