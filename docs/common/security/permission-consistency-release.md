---
module: common
domain: security
content_type: Troubleshooting
status: canonical
updated_on: 2026-09-16
owner: backend-team
audience: humans-and-agents
---

# Release permission consistency with a fresh FullDemo database

This runbook is for the operator-approved clean DDVS staging setup, not an automatic live-access repair.
The security changes do not require a database reset.
The coordinated candidate uses main's Finance migration `20260915122353_canonicalFeeAssignment`, which is additive on upgrade and preserves legacy financial rows.
A reset permanently removes employees, approvals, grants, scopes, and payroll history from that database unless they are recoverable from a backup.
Keep the finalized August payroll in a verified backup or archive before resetting.

## Current readiness and release prerequisites

The local candidate is ready for review with the explicitly recorded baseline failures and deferred risks.
Do not reset or deploy before the publication, review, CI, environment, and backup prerequisites below are complete.
The missing Finance migration from backend `ba9cf977` (PR #619) was supplied by subsequent main commits and is reconciled with the local rollback safeguards.
The synchronized migration chain passes local PostgreSQL upgrade and protected-rollback validation.
Fresh FullDemo and rerun preservation, focused HTTP/application/domain/PostgreSQL tests, production builds, and frontend checks pass on the synchronized September 16 candidate.
The final 35-case, five-browser matrix passes without retries, and authenticated Codex confirmation verifies the original layouts at mobile, tablet, and desktop sizes.
The user authorized fixing editor behavior within the old layout.
The current candidate includes full catalogue coverage, independent actions, complete bulk changes, direct/inherited grant labels, verified saves, and protected drafts in the original separate dialogs.
Use the final retained-layout validation results, not the earlier unchanged-UI smoke.
Publication authorization, independent review and GitHub checks, verified environment settings and backup remain required before an immediate database reset.
The [validation matrix](./permission-consistency-validation.md) separates local gates from unrun deployed acceptance.
It also records seven pre-existing Student documentation failures, the separately scoped identity role-assignment issue, and the existing Leadership dashboard error.
Do not treat this focused security release as a fix or acceptance result for those unrelated surfaces.
The user explicitly deferred PostgreSQL credential rotation for this release.
The exposed credential remains an outstanding security risk; neither deleting a database nor removing a tracked value revokes it.

## What triggers seeding

| Repository | Trigger | Current target | Behavior |
| --- | --- | --- | --- |
| Backend | Push or merge to `main` | Azure `stg-gyanmirai-be` | Security checks, build, startup tests, publish validation, DbMigrator, API deployment, startup check |
| Frontend | Push or merge to `main`, or manual workflow dispatch | Azure `dev-gyanmirai-fe` | Security checks, HR checks, build, UI deployment |
| Either feature branch | Push alone | No deployment | Does not run the deployment seeder |

The backend DbMigrator step explicitly enables `FullDemo` using `ShikshaMitra__Seeders__Enabled=true` and `ShikshaMitra__Seeders__Profile=FullDemo`.
Its database comes from the GitHub `DB_CONNECTION_STRING` secret.
The deployed API connection setting must identify that same intended database.
Target names are verified from the workflows, not proof of the current Azure or secret configuration.
Both deployment workflows wait for their reusable Security Regression job.
Backend migration and seeding occur only after tests and publish-profile validation succeed.
Deployment concurrency is serialized without cancelling an in-progress release.

## Before deleting anything

The Security UI redesign has been withdrawn at the user's request.
The old permission and visibility layouts remain, with user-approved behavior fixes inside them.
Earlier replacement-editor or unchanged-UI browser results do not validate the final retained-layout implementation.
Complete all current validation gates before release sign-off.
Credential rotation is an explicitly deferred risk, not evidence that the exposure is safe.

1. Review and commit the separate FE and BE `user/yvadher/security-permission-consistency` worktrees.
   Preserve unrelated work and exclude local `.artifacts`, authentication data, `next-env.d.ts`, and generated TypeScript include changes.
   Push the feature branches, open both PRs, and wait for the required checks and review.
   Consult the [validation matrix](./permission-consistency-validation.md) for actual evidence and remaining gates.
2. Record the owner's deferred credential-rotation decision and recovery owner.
   Rotation remains recommended, with both GitHub `DB_CONNECTION_STRING` and the Azure backend connection setting updated together when performed.
   Confirm the intended server, database, tenant, deployment targets, and frontend API URL privately without copying secrets into logs or this document.
3. Take a backup and verify that it can be restored.
   Record the matching currently deployed FE/BE versions and recovery owner.
   Do not proceed while release checks are failing or required external configuration is unverified.

## Reset and release order

1. Schedule a maintenance window and block user traffic.
   Stop the backend and background workers so the old application cannot write into the empty or partly seeded database.
2. Recreate only the explicitly verified staging database, with the required owner and privileges.
   Do not delete the PostgreSQL server or unrelated databases.
   Ensure the migrator identity can create and update schema objects; do not assume the application identity can create databases.
3. Merge the reviewed backend PR to `main`.
   Wait for Security Regression, startup tests, publish validation, and **Run DbMigrator** to succeed before accepting deployment.
   Check the migrator's successful completion log and investigate any partial or failed seed step.
   Confirm the backend is running and the workflow startup check passes at `https://stage-api.gyanmirai.in/.well-known/openid-configuration`.
   A deliberately stopped App Service may need to be started by the operator after deployment for this check.
4. Merge the reviewed frontend PR to `main` after backend success.
   Wait for its checks and deployment, then verify that the deployed UI uses the matching backend.
5. Sign in with an authorized administrator and deliberately assign the required operational scopes.
   Fresh Clerk and Office Assistant HR defaults are leave self-service only.
   HR-wide scope is not automatically created by the ESI seeder.
   FullDemo ESI starts **Ready**, not Active; an authorized HR user must review and activate it.
6. Secure or disable synthetic seed logins and rotate bootstrap passwords before broader access.
   The current staging workflow enables test logins; this is demo configuration, not a production identity policy.
7. Verify role/user grants through authoritative API reads, allowed and denied requests, scope-limited reads, and the ESI Ready state on the deployed pair.
   Removing a direct user grant does not remove access inherited from a role.
   Verify the effective result and remaining providers instead of treating an unchecked direct-grant switch as proof of complete access removal.
   Confirm restricted office roles cannot administer employees, payroll, or ESI.
   Record the deployed versions, check results, and any approved follow-up writes before reopening traffic.

Existing roles, passwords, memberships, revoked grants, and manual scopes are preserved on later seed reruns.
Changing a seed version or profile does not repair existing access.
Use approved, audited permission-management changes for intentional repairs.

## If a step fails

Keep traffic and background writes blocked.
Retain sanitized logs and identify whether failure occurred before seeding, during seeding, or after deployment.
Do not repeatedly delete the database to hide a failure.
Restore the verified backup with its matching application versions if rollback is necessary, then verify tenant and authorization behavior before reopening access.
The backend startup endpoint proves startup only, not full application or security acceptance.

No reset, live repair, commit, push, merge, or deployment was performed while preparing this release.
