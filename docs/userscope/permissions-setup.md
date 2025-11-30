# UserScope Permissions Setup Guide

## 🎯 Overview

The UserScope API endpoints require specific permissions. This guide shows you how to grant these permissions to users or roles.

---

## 📋 UserScope Permissions

| Permission Name | Description | Endpoints |
|-----------------|-------------|-----------|
| `ShikshaMitra.UserScope` | Parent permission for all UserScope features | - |
| `ShikshaMitra.UserScope.View` | View user scopes and assignments | GET /api/app/user-scope/{userId}<br>GET /api/app/user-scope/users-by-division/{id}<br>GET /api/app/user-scope/users-by-standard/{id} |
| `ShikshaMitra.UserScope.Assign` | Assign scope to a single user | POST /api/app/user-scope/assign |
| `ShikshaMitra.UserScope.BulkAssign` | Bulk assign scopes to multiple users | POST /api/app/user-scope/bulk-assign |
| `ShikshaMitra.UserScope.Revoke` | Revoke user scope assignments | DELETE /api/app/user-scope/{id}<br>DELETE /api/app/user-scope/revoke-all/{userId} |
| `ShikshaMitra.UserScope.Manage` | Manage scope assignments (activate, deactivate, update) | GET /api/app/user-scope/all-assignments<br>PUT /api/app/user-scope/{id}/activate<br>PUT /api/app/user-scope/{id}/deactivate<br>PUT /api/app/user-scope/{id}/description |

---

## 🚀 Quick Setup - Grant All Permissions to Admin Role

### Method 1: Using ABP Identity API (Recommended)

Run these API calls to grant all UserScope permissions to the `admin` role:

```http
PUT /api/permission-management/permissions
Content-Type: application/json
Authorization: Bearer {token}

{
  "permissions": [
    {
      "name": "ShikshaMitra.UserScope",
      "isGranted": true
    },
    {
      "name": "ShikshaMitra.UserScope.View",
      "isGranted": true
    },
    {
      "name": "ShikshaMitra.UserScope.Assign",
      "isGranted": true
    },
    {
      "name": "ShikshaMitra.UserScope.BulkAssign",
      "isGranted": true
    },
    {
      "name": "ShikshaMitra.UserScope.Revoke",
      "isGranted": true
    },
    {
      "name": "ShikshaMitra.UserScope.Manage",
      "isGranted": true
    }
  ],
  "providerName": "R",
  "providerKey": "admin"
}
```

### Method 2: Using Database

Insert permissions directly into the database:

```sql
-- Get the admin role ID
SELECT "Id" FROM "AbpRoles" WHERE "Name" = 'admin';

-- Insert permissions (replace {ROLE_ID} with the actual ID from above)
INSERT INTO "AbpPermissionGrants" ("Id", "TenantId", "Name", "ProviderName", "ProviderKey")
VALUES 
  (gen_random_uuid(), NULL, 'ShikshaMitra.UserScope', 'R', 'admin'),
  (gen_random_uuid(), NULL, 'ShikshaMitra.UserScope.View', 'R', 'admin'),
  (gen_random_uuid(), NULL, 'ShikshaMitra.UserScope.Assign', 'R', 'admin'),
  (gen_random_uuid(), NULL, 'ShikshaMitra.UserScope.BulkAssign', 'R', 'admin'),
  (gen_random_uuid(), NULL, 'ShikshaMitra.UserScope.Revoke', 'R', 'admin'),
  (gen_random_uuid(), NULL, 'ShikshaMitra.UserScope.Manage', 'R', 'admin');
```

---

## 🔒 Permission Providers

ABP supports multiple permission providers:

| Provider | Code | Description | Example ProviderKey |
|----------|------|-------------|---------------------|
| Role | `R` | Grant permission to a role | `admin`, `teacher`, `principal` |
| User | `U` | Grant permission to a specific user | User GUID (e.g., `3a1dc7ba-324a-8bad-5467-7485ff3019e2`) |
| Client | `C` | Grant permission to an OAuth client | `ShikshaMitra_App` |

---

## 📝 Granting Permissions to Specific User

To grant permissions to a specific user instead of a role:

```http
PUT /api/permission-management/permissions
Content-Type: application/json
Authorization: Bearer {token}

{
  "permissions": [
    {
      "name": "ShikshaMitra.UserScope.Assign",
      "isGranted": true
    }
  ],
  "providerName": "U",
  "providerKey": "{USER_GUID}"
}
```

Or via SQL:

```sql
INSERT INTO "AbpPermissionGrants" ("Id", "TenantId", "Name", "ProviderName", "ProviderKey")
VALUES 
  (gen_random_uuid(), NULL, 'ShikshaMitra.UserScope.Assign', 'U', '{USER_GUID}');
```

---

## 🧪 Testing Permissions

After granting permissions, test them:

1. **Login** to get a fresh token with the new permissions
2. **Call the API** endpoint
3. **Verify** you get data instead of 403 Forbidden

```bash
# Login to get new token
POST /connect/token
{
  "grant_type": "password",
  "client_id": "ShikshaMitra_App",
  "username": "admin",
  "password": "1q2w3E*",
  "scope": "ShikshaMitra"
}

# Test UserScope endpoint
POST /api/app/user-scope/assign
Authorization: Bearer {new_token}
{
  "userId": "{USER_GUID}",
  "standardId": "{STANDARD_GUID}",
  "divisionId": "{DIVISION_GUID}",
  "description": "Test assignment"
}
```

---

## 🔧 Troubleshooting

### Error: "Authorization failed! Given policy has not granted."

**Cause:** The user/role doesn't have the required permission.

**Solution:**
1. Grant the permission using one of the methods above
2. **Re-login** to get a new token with updated permissions
3. Try the API call again

### Error: "The AuthorizationPolicy named: 'X' was not found."

**Cause:** Permissions are not defined properly.

**Solution:**
1. Verify `ShikshaMitraPermissionDefinitionProvider.cs` has the permission definitions
2. Verify localization strings exist in `en.json`
3. **Restart the application** to reload permission definitions

### Permissions Not Showing in UI

**Cause:** Permission definitions are cached or not loaded.

**Solution:**
1. Clear browser cache
2. Restart the application
3. Check if localization strings are defined

---

## 📚 Related Files

- **Permission Definitions:** `src/ShikshaMitra.Application.Contracts/Security/Permissions/ShikshaMitraPermissionDefinitionProvider.cs`
- **Permission Constants:** `src/ShikshaMitra.Application.Contracts/Security/Permissions/ShikshaMitraPermissions.cs`
- **Localization:** `src/ShikshaMitra.Domain.Shared/Localization/ShikshaMitra/en.json`

---

## 💡 Best Practices

1. ✅ **Grant permissions to roles**, not individual users (easier management)
2. ✅ **Use least privilege principle** - only grant necessary permissions
3. ✅ **Test with non-admin users** to ensure proper access control
4. ✅ **Document permission requirements** for each API endpoint
5. ✅ **Re-login after permission changes** to get updated token

---

**Last Updated:** December 2024  
**Version:** 1.0
