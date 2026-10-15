# Set up roles

This guide explains how ShikshaMitra roles relate to permissions and data scope.
Use it when you create test roles, verify seeded roles, or debug role assignment failures.

## Role basics

Roles define what a user can do.
UserScope defines which tenant data rows a staff user can see.
Never use a role name as a substitute for tenant or data-scope filtering.

## Create roles through the Identity API

Use the ABP Identity API when you need to create a role manually.

```http
POST /api/identity/roles
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "teacher",
  "isDefault": false,
  "isPublic": true
}
```

## Create roles through SQL only for local repair

Use direct SQL only for local development repair.
Do not use this path as the normal production workflow.

```sql
INSERT INTO "AbpRoles" ("Id", "TenantId", "Name", "NormalizedName", "IsDefault", "IsStatic", "IsPublic", "ConcurrencyStamp")
VALUES
  (gen_random_uuid(), NULL, 'teacher', 'TEACHER', false, false, true, gen_random_uuid()::text),
  (gen_random_uuid(), NULL, 'principal', 'PRINCIPAL', false, false, true, gen_random_uuid()::text),
  (gen_random_uuid(), NULL, 'student', 'STUDENT', false, false, true, gen_random_uuid()::text);
```

## Use lower-case role names

Role names are case-sensitive in user creation payloads.
Use the exact lower-case role name created in Identity.

```json
{
  "userName": "teacher.john",
  "name": "John",
  "surname": "Doe",
  "email": "john@school.com",
  "phoneNumber": "+1234567890",
  "roleNames": ["teacher"],
  "password": "Teacher@123"
}
```

## Recommended role names

Use concise names that map to school responsibilities.

| Role name | Description |
|---|---|
| `teacher` | Teachers who teach subjects |
| `principal` | School principal |
| `vice_principal` | Vice principal |
| `hod` | Head of department |
| `librarian` | Library staff |
| `accountant` | Finance staff |
| `admin_staff` | Administrative staff |
| `student` | Enrolled student |
| `parent` | Parent or guardian |

## Troubleshoot role assignment

Use this checklist when user creation fails with a missing role error.

1. Query available roles with `SELECT "Name" FROM "AbpRoles";`
2. Verify exact case in `roleNames`
3. Create the missing role through the Identity API
4. Re-run the user creation request
5. Check `src/ShikshaMitra.HttpApi.Host/Logs/` if the error persists

## Delete an unused role

`DELETE /api/identity/roles/{id}` requires `AbpIdentity.Roles.Delete` and current-tenant ownership.
`SchoolIdentityRoleAppService` checks dependencies before delegating to ABP, retaining its static-role protection and normal deletion behavior.

| Dependency | Response |
| --- | --- |
| Direct user membership or a linked employee account | 409, `ShikshaMitra:RoleAssignedToUsers`, `data.UserCount` (distinct accounts), and instructions to reassign or remove users first |
| Employee record without a login, including archived records | 409, `ShikshaMitra:RoleReferencedByEmployees`, `data.EmployeeCount`, and instructions to reassign employees first |
| No user or employee references | 204; the role is deleted |

Employee references are checked with soft-delete filtering disabled while tenant isolation remains enabled.
PostgreSQL deletion holds a role row lock through the dependency check and commit, so an in-flight foreign-key assignment cannot slip between the check and deletion.
The `AppEmployees.RoleId` restrictive foreign key remains intact; no migration or automatic reassignment is required.
Unexpected 500 responses include `traceId`, which is also written to the request's server error log.

Focused validation:

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter 'FullyQualifiedName~RoleDeletion|FullyQualifiedName~ApiProblemDetails'
```

Set `ROLE_DELETION_TEST_POSTGRES` to a loopback administrator connection to include the PostgreSQL gate.
It creates and removes a new `role_deletion_tests_*` database, reproduces the employee foreign-key violation, verifies the HTTP contracts and checks concurrent assignment.
It never reuses or resets the configured school database.

## Related docs

- [Modules index](../../modules/README.md)
- [UserScope usage guide](../platform/UserScope-Usage.md)
- [Seeded accounts and access](../testing/seeded-accounts-access.md)
