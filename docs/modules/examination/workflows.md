---
module: examination
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-08-31
owner: backend-team
audience: humans-and-agents
---

# Trace Examination seating operations

TL;DR: prepare and validate plans while the examination is a draft, then validate and publish the complete examination in one transaction.

1. Load the V2 workspace for the exam and optional plan.
2. Open an existing plan or select one or many unassigned exam subjects.
3. Create the plan; one subject becomes individual, multiple subjects become shared, and repeat calls with the same set return the same plan.
4. Save room choices and rules with the workspace version.
5. Generate a proposal using only seats available to the current plan.
6. Apply the reviewed proposal; identical retries do not duplicate seats.
7. Run authoritative validation for capacity and cross-plan seat-time conflicts.
8. Return to subject planning and repeat until every scheduled subject belongs to exactly one valid plan.
9. Run examination validation; subject gaps, duplicate ownership, empty plans, or any plan blocker stop publication.
10. Publish once; all plans finalize and publish, audits and the examination publication record are written, and notification intent is emitted in the same transaction.
11. Read the one-exam dashboard summary, global publication receipt, or per-plan exports.

If another writer changes the plan between steps, the server rejects the stale version.
If a selected subject already belongs to another plan, creation stops and preserves the existing data for explicit review.
Before step 10, users may edit groups, clear a changed plan's allocations, delete empty plans, or reset all seating-plan data with a required reason. After step 10 every mutation rejects reopening the examination seating. Publication may be retried after timeouts without duplicate records, audits, or notification intent.
