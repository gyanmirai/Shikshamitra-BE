# Consolidated Environment - Variable Reference

This document explains all variables in the consolidated `ShikshaMitra-Consolidated.postman_environment.json` file.

---

## 📋 Variable Categories

### 🌐 Connection Settings

| Variable | Default Value | Description | Required |
|----------|---------------|-------------|----------|
| `base_url` | `https://localhost:44300` | Local development API URL | ✅ Yes |
| `staging_url` | `https://staging-gyanmirai-be.azurewebsites.net` | Staging API URL | ❌ Optional |

**Usage:** Switch between local and staging by changing `base_url` value or enabling/disabling `staging_url`.

---

### 🔐 Authentication

| Variable | Default Value | Type | Description |
|----------|---------------|------|-------------|
| `tenant_id` | `3a1dc7ba-...` | GUID | Tenant identifier |
| `username` | `admin` | String | Login username |
| `password` | `Temp@123` | Secret | Login password |
| `access_token` | _(auto-saved)_ | Secret | Bearer token |
| `token_type` | `Bearer` | String | Token type |

**Auto-saved:** `access_token` is automatically set after successful login.

---

### 👤 User Management

| Variable | Default Value | Auto-saved | Used In |
|----------|---------------|------------|---------|
| `user_id` | _(empty)_ | ✅ Yes | User operations, UserScope |
| `user_id_2` | _(empty)_ | ✅ Yes | Secondary user (examples) |
| `concurrency_stamp` | _(empty)_ | ✅ Yes | Optimistic concurrency |

**Auto-saved from:** User creation/update responses

---

### 🎯 UserScope & Permissions

| Variable | Default Value | Auto-saved | Description |
|----------|---------------|------------|-------------|
| `scope_assignment_id` | _(empty)_ | ✅ Yes | UserScope assignment ID |
| `academic_year_id` | _(empty)_ | ❌ Manual | Current academic year |
| `board_id` | _(empty)_ | ❌ Manual | Education board |
| `medium_id` | _(empty)_ | ❌ Manual | Education medium |
| `stage_id` | _(empty)_ | ❌ Manual | Education stage |
| `standard_id` | _(empty)_ | ❌ Manual | Standard/class |
| `division_id` | _(empty)_ | ❌ Manual | Division/section |
| `division_a_id` | _(empty)_ | ❌ Manual | Division A (for examples) |
| `division_b_id` | _(empty)_ | ❌ Manual | Division B (for examples) |

**Get from database:** Use `DATABASE-SETUP.sql` or `UserScope-Database-Setup.sql` to retrieve these IDs.

---

### 👥 HR Module - Employees

| Variable | Default Value | Auto-saved | Used In |
|----------|---------------|------------|---------|
| `employee_id` | `3a1e19bb-...` | ❌ Manual | All employee operations |
| `document_id` | _(empty)_ | ✅ Yes | Document operations |

**Pre-configured:** `employee_id` is set to a test employee for quick testing.

---

### 📋 HR Module - Leave Management

| Variable | Auto-saved | Used In |
|----------|------------|---------|
| `leave_type_id` | ✅ Yes | Leave policy creation |
| `leave_policy_id` | ✅ Yes | Leave policy operations |

**Auto-saved from:** Leave type/policy creation responses

---

### 💰 HR Module - Payroll

| Variable | Auto-saved | Used In |
|----------|------------|---------|
| `earning_component_id` | ✅ Yes | Earning component operations |
| `deduction_component_id` | ✅ Yes | Deduction component operations |
| `payroll_structure_id` | ✅ Yes | Payroll structure operations |

**Auto-saved from:** Component creation responses

---

### 🏢 Rooms Management

| Variable | Auto-saved | Used In |
|----------|------------|---------|
| `building_id` | ✅ Yes | Room creation |
| `room_type_id` | ✅ Yes | Room creation |
| `room_id` | ✅ Yes | Room operations |

**Auto-saved from:** Building/room type/room creation responses

---

## 🔧 Setup Guide

### Quick Setup (Essential Variables)

1. **Import environment:**
   ```
   Import: ShikshaMitra-Consolidated.postman_environment.json
   ```

2. **Configure connection** (if not using defaults):
   ```json
   base_url = "https://localhost:44300"  // or your API URL
   tenant_id = "your-tenant-id"
   ```

3. **Set credentials** (if different):
   ```json
   username = "admin"
   password = "Temp@123"
   ```

4. **Login:**
   ```
   Run: Authentication → Login
   → access_token auto-saved ✅
   ```

5. **You're ready!** All other variables auto-save as you test.

---

### Full Setup (All Variables)

**For UserScope testing:**

1. Run SQL queries from `DATABASE-SETUP.sql` or `UserScope-Database-Setup.sql`
2. Update these variables manually:
   - `academic_year_id`
   - `board_id`, `medium_id`, `stage_id`
   - `standard_id`
   - `division_id`, `division_a_id`, `division_b_id`

**For HR Module testing:**

1. Set `employee_id` (or use default test employee)
2. All other HR variables auto-save during testing

**For Rooms Management:**

1. All variables auto-save during testing

---

## 📊 Variable Auto-Save Matrix

| Collection | Variables Auto-Saved |
|------------|---------------------|
| **Main** | `access_token`, `user_id`, `concurrency_stamp` |
| **UserScope** | `scope_assignment_id`, `user_id` |
| **HR Module** | `leave_type_id`, `leave_policy_id`, `earning_component_id`, `deduction_component_id`, `document_id` |
| **Rooms** | `building_id`, `room_type_id`, `room_id` |

---

## 🎯 Collection-Specific Usage

### Main Collection
**Required variables:**
- ✅ `base_url`
- ✅ `tenant_id`
- ✅ `username`, `password`

**Auto-saved:**
- `access_token`, `user_id`, `concurrency_stamp`

---

### HR Module Collection
**Required variables:**
- ✅ `base_url`, `tenant_id`, `username`, `password`
- ✅ `employee_id` (pre-configured with test employee)

**Auto-saved:**
- All leave, payroll, and document IDs

---

### UserScope Collection
**Required variables:**
- ✅ `base_url`, `tenant_id`, `username`, `password`
- ✅ Academic structure IDs (from database)

**Auto-saved:**
- `user_id`, `scope_assignment_id`

---

### Rooms Collection
**Required variables:**
- ✅ `base_url`, `tenant_id`, `username`, `password`

**Auto-saved:**
- All building, room type, and room IDs

---

## 💡 Best Practices

### ✅ Do

1. **Let variables auto-save** - Don't manually edit auto-saved IDs
2. **Use database scripts** - Get fresh IDs from `DATABASE-SETUP.sql`
3. **Keep environment selected** - Always verify dropdown shows correct environment
4. **Use {{variables}}** - Never hardcode GUIDs in requests
5. **Re-login when needed** - Tokens expire, just run Login again

### ❌ Don't

1. **Don't mix environments** - Use single consolidated environment for all
2. **Don't hardcode values** - Always use variable syntax
3. **Don't manually edit tokens** - Let auto-save handle it
4. **Don't skip login** - Always authenticate first

---

## 🔄 Environment Switching

### Local Development
```json
base_url = "https://localhost:44300"
```

### Staging
```json
base_url = "https://staging-gyanmirai-be.azurewebsites.net"
```

### Multiple Tenants
Duplicate environment and change:
```json
tenant_id = "different-tenant-id"
```

---

## 🐛 Troubleshooting

### Variables not working?
```
1. Verify environment is selected (top-right dropdown)
2. Check variable name matches exactly: {{employee_id}}
3. Ensure variable has value (click eye icon to check)
```

### Auto-save not working?
```
1. Check request has test script
2. Verify response is 200 OK
3. Look for console message: "✅ ID saved: ..."
```

### Token expired?
```
1. Run: Authentication → Login
2. Token auto-refreshes
3. Continue testing
```

---

## 📚 Related Documentation

- [Main README](./README.md) - Complete guide
- [OVERVIEW](./OVERVIEW.md) - Collection comparison
- [DATABASE-SETUP.sql](./DATABASE-SETUP.sql) - Get IDs from database
- [HR-Module-README.md](./HR-Module-README.md) - HR module guide

---

**Environment File:** `ShikshaMitra-Consolidated.postman_environment.json`  
**Total Variables:** 30  
**Auto-saved Variables:** 15+  
**Last Updated:** January 2025
