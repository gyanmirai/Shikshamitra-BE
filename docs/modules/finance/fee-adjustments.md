# Finance Module — Fee Adjustments

## Overview

Fee adjustments let staff create **credit pools** from paid fee lines (source `DueFee` rows) and apply that credit to reduce **pending** obligations on other installments. Cash returned to parents uses **Fee Refund**, not adjustments.

Base route: `api/fee-adjustments`

## Status machine

| Value | Name | Notes |
|------:|------|-------|
| 0 | Pending | Editable / deletable |
| 1 | Rejected | Terminal |
| 2 | PartiallyApplied | Some credit applied; remainder available |
| 3 | Applied | Fully applied |
| 4 | Cancelled | Deleted / cancelled while pending |

## API surface

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `eligibility?studentId=&academicYearId=` | Create form — adjustable balances |
| POST | `?academicYearId=` | Create request (source lines) |
| PUT | `{id}?academicYearId=` | Update pending (replaces request) |
| DELETE | `{id}` | Delete pending |
| GET | `{id}` | Detail |
| GET | `applications?boardId=&educationMediumId=&academicYearId=` | Inbox (+ optional `status`, `completedOnly`) |
| GET | `{id}/apply-context` | Pending targets for apply UI |
| POST | `{id}/apply` | Approve + apply allocations (header `Idempotency-Key`) |
| POST | `{id}/reject` | Reject with message |

## Domain

| Type | Role |
|------|------|
| `FeeAdjustmentRequest` | Aggregate root |
| `FeeAdjustmentSourceLine` | Credit source at create |
| `FeeAdjustmentTargetAllocation` | Applied credit per target `DueFee` |
| `FeeAdjustmentManager` | Create / apply / reject orchestration |
| `DueFee.ApplyAdjustmentCredit` | Reduces outstanding on target lines |

## Permissions

`Finance.Fees.Adjustment`: View, Add, Delete, Approve, Reject

## Maker-checker

Approver must differ from creator (`CreatorId`).
