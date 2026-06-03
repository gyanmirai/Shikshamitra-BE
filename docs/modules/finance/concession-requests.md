# Finance Module — Concession Requests

## Overview

Concession requests let staff apply a **single concession type** per request, map **fee heads** with discount values, and submit in one step. New requests are created in **Pending** status (no draft wizard). Approvers allocate concession amounts to specific **`DueFee`** rows via `dueFeeId`.

---

## Status machine

| Value | Name | Notes |
|------:|------|-------|
| 0 | Pending | Editable; can delete, approve, or reject |
| 1 | Approved | Due fees updated; visible in fee collection student details |
| 2 | Rejected | Terminal |
| 3 | Cancelled | From approved only |

---

## API surface

Base route: `api/concession-request`

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `?academicYearId=` | Atomic create → Pending |
| PUT | `{id}` | Update pending request |
| DELETE | `{id}` | Delete pending request |
| GET | `{id}` | Detail |
| PUT | `{id}/documents` | Upload `{ documents: [{ documentId, url, status }] }` |
| GET | `concession-applications` | Inbox by board + medium |
| GET | `{id}/approval-context` | Due fee lines for approval UI |
| POST | `{id}/approve` | `{ message?, feeHeadApprovals[] }` with `dueFeeAllocations[]` |
| POST | `{id}/reject` | `{ message }` |

Student history: `GET api/concession-requests/{studentId}`

Cancel approved: `POST api/concession-requests/{id}/cancel`

Removed legacy step endpoints: `feeHeadsData`, `value-entry`, `submit`.

---

## Approve payload

```json
{
  "message": "Approved",
  "feeHeadApprovals": [
    {
      "feeHeadId": "guid",
      "approvedConcessionAmount": 1500,
      "dueFeeAllocations": [
        { "dueFeeId": "guid", "concessionAmount": 750 },
        { "dueFeeId": "guid", "concessionAmount": 750 }
      ]
    }
  ]
}
```

If `feeHeadApprovals` is omitted, the server auto-distributes across selectable due fees per fee head.

---

## Domain

| Type | Role |
|------|------|
| `ConcessionRequest` | Aggregate; scope in ExtraProperties |
| `ConcessionFeeHeadMapping` | Requested discount per fee head |
| `ConcessionDueFeeAllocation` | Approved allocation per due fee |
| `ConcessionRequestManager` | Create, validate, build allocations |
| `DueFee.ApplyConcession` | Updates outstanding after approval |

---

## Permissions

Under `Finance.Fees.Concession`: View, Add, Update, Approve, Reject.

---

## Frontend

- **Create:** `ConcessionRequestForm` (single scroll form)
- **Approve (fee-head lines):** `ConcessionFeeHeadApprovalView` + `GET approval-context`
- **Quick approve/reject:** `ConcessionRequestApprove` (message only; server auto-allocates)

---

## Database

- `AppConcessionRequests`
- `AppConcessionFeeHeadMappings`
- `AppConcessionRequestDocuments`
- `AppConcessionDueFeeAllocations` (migration `AddConcessionDueFeeAllocations`)
