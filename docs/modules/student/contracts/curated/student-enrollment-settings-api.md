---
module: student
domain: enrollment-settings
status: canonical
source: ../../prd/inquiry-source-prd.md
updated_on: 2026-06-21
owner: backend-team
---

# Student Enrollment Settings API

## Purpose

Enrollment settings APIs manage student enrollment lookup configuration. The Inquiry Source setting is exposed through the existing `inquiry-preferences` route for V1 compatibility.

## Endpoint(s)

| Method | Route | Purpose | Permission |
|---|---|---|---|
| `GET` | `/api/student-enrollment/{academicYearId}/settings/inquiry-preferences` | List Inquiry Sources for an academic year. | `Students.Enrollment_SettingsInquiryPreference.View` |
| `GET` | `/api/student-enrollment/settings/inquiry-preferences/{id}` | Get one Inquiry Source. | `Students.Enrollment_SettingsInquiryPreference.View` |
| `POST` | `/api/student-enrollment/{academicYearId}/settings/inquiry-preferences` | Create an Inquiry Source. | `Students.Enrollment_SettingsInquiryPreference.Add` |
| `PUT` | `/api/student-enrollment/settings/inquiry-preferences/{id}` | Update name, description, and display order. | `Students.Enrollment_SettingsInquiryPreference.Add` |
| `PATCH` | `/api/student-enrollment/settings/inquiry-preferences/{id}/status` | Activate or deactivate an Inquiry Source. | `Students.Enrollment_SettingsInquiryPreference.Add` |
| `DELETE` | `/api/student-enrollment/settings/inquiry-preferences/{id}` | Delete an unused Inquiry Source. | `Students.Enrollment_SettingsInquiryPreference.Delete` |

## Auth & permissions

All endpoints require an authenticated user with the listed student enrollment settings permission. Academic-year scope is enforced for the requested source's academic year.

## Request headers

- `Authorization: Bearer <token>`
- Tenant context is resolved by the platform authentication/tenant pipeline.

## Path/query/body contract

List query:

| Query | Type | Rule |
|---|---|---|
| `skipCount` | integer | Pagination offset. |
| `maxResultCount` | integer | Page size. |
| `sorting` | string | Accepted for compatibility; V1 applies fixed source order. |
| `filterText` | string | Optional name/description search. |
| `isActive` | boolean | Optional active-status filter. Omit to return active and inactive sources. |

Create/update body:

```json
{
  "preferenceName": "Website",
  "description": "School website inquiry form",
  "displayOrder": 10
}
```

Status body:

```json
{
  "isActive": false,
  "deactivationReason": "Merged into Website"
}
```

Response item:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "preferenceName": "Website",
  "description": "School website inquiry form",
  "academicYearId": "00000000-0000-0000-0000-000000000000",
  "displayOrder": 10,
  "isActive": true,
  "deactivationReason": null,
  "assignedStudentsCount": 0,
  "canDelete": true
}
```

## Response contract (success + errors)

Success:

- List returns `PagedResultDto<InquiryPreferenceDto>`.
- Create/update/status/get return `InquiryPreferenceDto`.
- Delete returns no payload on success.

Business errors:

| Code | Meaning |
|---|---|
| `InquiryPreference:NotFound` | Source does not belong to current tenant or cannot be found. |
| `InquiryPreference:Inactive` | Source is inactive and is being used on a new or changed record. |
| `InquiryPreference:InUse` | Delete was requested for a source assigned to at least one student. |
| `InquiryPreference:InvalidDisplayOrder` | Display order is negative. |

## Business rules / invariants

- Existing API naming stays `InquiryPreference` and `inquiry-preferences`; product copy says Inquiry Source.
- New sources default to `isActive=true` and `displayOrder=0`.
- List order is active sources first, then display order, then source name.
- Active-only selection is done by calling list with `isActive=true`.
- Inactive sources are valid only when unchanged on an existing record.
- Delete is allowed only when `assignedStudentsCount=0`.
- Deactivation is allowed even when `assignedStudentsCount>0`.
- The non-deleted unique key allows recreating a previously deleted source name.

## Idempotency / concurrency notes (if relevant)

Status updates are repeat-safe for the same target status. Standard entity concurrency/audit behavior applies.

## Examples (minimal, copy-ready)

```http
GET /api/student-enrollment/{academicYearId}/settings/inquiry-preferences?isActive=true&maxResultCount=1000
Authorization: Bearer <token>
```

```http
PATCH /api/student-enrollment/settings/inquiry-preferences/{id}/status
Authorization: Bearer <token>
Content-Type: application/json

{
  "isActive": false,
  "deactivationReason": "No longer used for new inquiries"
}
```

## Change log

- 2026-06-21: Added Inquiry Source lifecycle fields, active-only filter, status endpoint, usage count, delete eligibility, and compatibility notes.
