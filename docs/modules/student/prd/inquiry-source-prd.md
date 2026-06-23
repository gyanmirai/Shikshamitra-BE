# Inquiry Source PRD

## Summary

Inquiry Source is the product term for the setting previously shown as Inquiry Preference. V1 keeps backend route and entity naming as `inquiry-preferences` for compatibility while updating UI and documentation language to Inquiry Source.

V1 upgrades the lookup from a name-only list into an ERP-safe setting with lifecycle status, description, display order, usage count, and delete eligibility.

## Purpose

Schools need to record where an enrollment lead came from without breaking existing student, inquiry, registration, or admission records when the source is no longer used. Inquiry Source must support operational cleanup without losing historical context.

## Actors

| Actor | Need |
|---|---|
| Admission staff | Select an active source while creating or editing inquiry and registration records. |
| Enrollment admin | Maintain source names, order, descriptions, and active status. |
| School leadership | Keep historical source data available for reporting and review. |
| Support/developer | Preserve API compatibility and avoid referential-data 500 errors. |

## V1 Scope

Included:

- Product copy uses Inquiry Source.
- Compatibility API routes remain `/api/student-enrollment/.../settings/inquiry-preferences`.
- Source fields: name, description, display order, active status, optional deactivation reason.
- Response fields: assigned students count and can delete.
- Active-only source selection for new or changed records.
- Inactive source display on existing records.
- Delete only when unused; deactivate even when used.

Excluded from V1:

- Source category.
- Default source.
- Color/icon.
- Campaign attribution fields.
- Per-academic-year default enforcement.

## Data Model

| Field | Type | Rule |
|---|---|---|
| `preferenceName` | string | Required. Existing API field name retained. UI label is Inquiry Source. |
| `description` | string | Optional, max 500 characters. |
| `displayOrder` | integer | Defaults to 0. Must be 0 or greater. |
| `isActive` | boolean | Defaults to true for existing and new rows. |
| `deactivationReason` | string | Optional, max 500 characters. Cleared when reactivated. |
| `assignedStudentsCount` | integer | Computed from student additional details using this source. |
| `canDelete` | boolean | Computed as true only when `assignedStudentsCount` is 0. |

## Behavior Rules

1. Settings list shows active and inactive sources.
2. Selection dropdowns for new records request active sources only.
3. Settings list sorting is active first, then display order, then name.
4. Existing inactive sources remain visible on records that already use them.
5. Saving an existing record with the same inactive source is allowed.
6. Creating a new inquiry, direct registration, or admission with an inactive source is rejected.
7. Editing a record from an active source to an inactive source is rejected.
8. Editing a record from inactive source A to inactive source B is rejected.
9. In-use source delete is blocked with a business error.
10. In-use source deactivation is allowed.
11. Unused inactive sources can be deleted.
12. Recreating a deleted unused source by the same name is allowed because uniqueness only applies to non-deleted rows.

## UI Requirements

Settings table:

- Show source name, description, active/inactive status, usage count, display order, and actions.
- Provide a status toggle that opens a confirmation dialog.
- Ask for optional deactivation reason when deactivating.
- Disable delete when `canDelete=false`.
- Keep edit available for name, description, and display order.

Inquiry and registration edit screens:

- Show existing inactive source as the selected value with an inactive label.
- Continue to allow save when the inactive source is unchanged.
- Prompt user to choose an active source when changing the value.
- Do not include inactive sources as selectable alternatives.

## API Contract Impact

See [student enrollment settings API](../contracts/curated/student-enrollment-settings-api.md).

Compatibility notes:

- Route segment remains `inquiry-preferences`.
- DTO field `preferenceName` remains the source name field.
- New query parameter `isActive` filters list results.
- New status endpoint activates or deactivates a source.

## Validation Matrix

| Scenario | Expected result |
|---|---|
| Existing sources after migration | `isActive=true`, `displayOrder=0`, optional fields null. |
| Active-only list after deactivation | Inactive source is excluded. |
| Create inquiry with inactive source | Reject with `InquiryPreference:Inactive`. |
| Create direct registration with inactive source | Reject with `InquiryPreference:Inactive`. |
| Edit inquiry with unchanged inactive source | Success. |
| Edit inquiry changing to inactive source | Reject with `InquiryPreference:Inactive`. |
| Delete in-use source | Reject with `InquiryPreference:InUse`. |
| Deactivate in-use source | Success. |
| Delete unused inactive source | Success. |
| Recreate deleted unused source name | Success. |

## Test Plan

Backend:

- Migration/default behavior for existing rows.
- Active-only filter excludes inactive rows.
- Create inquiry rejects inactive source.
- Direct registration rejects inactive source.
- Update inquiry succeeds with unchanged inactive source.
- Update inquiry rejects changed inactive source.
- In-use delete is blocked.
- In-use deactivation succeeds.
- Unused inactive/deleted source can be recreated by name.

Frontend:

- Settings table renders status, usage count, description, and disabled delete state.
- Settings status toggle opens confirmation and submits active status.
- Settings form submits description and display order.
- New-record dropdowns request active sources only.
- Edit form with inactive existing source shows inactive selected option.
- Changing to another inactive source is not possible from UI.

## Future Backlog

- Source category: Walk-in, Website, Social, Referral, Campaign, Partner, Other.
- Marketing attribution: campaign name, referral person, ad platform, cost per lead.
- Workflow defaults: default follow-up priority, next reminder days, owner/team assignment.
- Reporting: source-wise inquiry count, conversion to registration/admission, stale source cleanup.
- UX polish: color/icon and one active default source per academic year, only after usage proves it is needed.

## Open Decisions

- Whether inactive sources should remain selectable in historical report filters by default.
- Whether display order should be unique per academic year.
- Whether deactivation should require a reason after operational usage proves it is valuable.
