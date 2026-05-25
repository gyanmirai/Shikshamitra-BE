# HR Audit Rollout Checklist

Use this checklist when extending the dedicated HR audit trail beyond the current policy, attendance register, and payroll lock coverage.

## Required Event Shape

- Write through `HrAuditWriter`; do not create module-specific audit tables.
- Include `entityType`, `entityId`, `entityName`, `action`, `summary`, `OccurredAt`, and actor snapshot.
- Include `reason` when the workflow already captures a business reason.
- Include changed fields for user-meaningful create/update/delete/status changes.
- Exclude system-only fields such as tenant id, ABP audit columns, soft-delete columns, and internal ids unless they are the business subject.
- Keep structured operational details in `ContextJson`; render only safe business fields by default in the UI.

## Permission Mapping

- Policy, policy version, and scoped assignment events: `HR.Policies.ViewAudit`.
- Attendance adjustment and evaluation events: `HR.AttendanceAndLeave.Evaluations.ViewAudit`.
- Payroll run lock events: `HR.PayrollManagement.RunPayroll.ViewAudit`.
- New HR audit surfaces must define their permission before exposing an audit drawer.

## Candidate Follow-up Areas

- Employee profile sections: personal, contact, family, additional, document, payroll, status, and reporting manager changes.
- Leave workflows: request, approve, reject, cancel, balance adjustment, and policy changes.
- Calendar workflows: holiday, event, holiday type, and event type changes.
- Payroll settings: earning components, deduction components, PF, professional tax, payroll structure, and salary slip lifecycle.
- Attendance imports: preview confirmation, row replacement, skipped rows, and source file/run metadata.

## Acceptance Criteria

- Events are paginated and server-filtered.
- The default order is newest first by `OccurredAt desc, Id desc`.
- UI shows precise date and time, actor, action, summary, reason, changed fields, and safe context.
- Same-day edits are visually distinguishable.
- Query paths remain covered by tenant/entity/time indexes or a documented new index.
