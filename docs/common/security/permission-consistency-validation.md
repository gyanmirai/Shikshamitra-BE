# Permission consistency validation

Date: 2026-09-16.
Branch in both repositories: `user/yvadher/security-permission-consistency`.
Work is uncommitted and not deployed.
The candidate checkouts are `.worktrees/security-permission-consistency-fe` and `.worktrees/security-permission-consistency-be` under the wrapper.
Other worktrees were preserved.

## Current candidate and release hold

Frontend is synchronized to `0d72df9d4`; backend is synchronized to `9b282838`.
Both synchronized with `main` on September 16.
Saved dirty-file content was compared before and after synchronization; deliberate Finance migration reconciliation is listed below.
The frontend retains the original Role/User screens, separate dialogs, and three-panel layout.
The user explicitly approved fixing behavior within that layout after the unchanged-UI validation exposed remaining defects.

The candidate now includes the approved catalogue, draft, inheritance, independent-toggle, and complete-bulk behavior fixes.
Current evidence below supersedes the earlier unchanged-UI results where the implementation changed.
This is local release-candidate validation, not authorization to reset or deploy and not exhaustive whole-application security sign-off.
Follow the [release and recovery runbook](./permission-consistency-release.md) after code review and the required checks.
A feature-branch push alone does not deploy or run the deployment seeder.

## Finance dependency reconciliation

Backend main now supplies migration `20260915122353_canonicalFeeAssignment`, whose class is `AddCanonicalStudentFeeAssignments`.
The independently generated, unpublished local migration was archived under ignored local artifacts to prevent duplicate columns and duplicate migration classes.
The upstream generated designer and current model snapshot are retained unchanged.
The upstream six-decimal allocation precision, index name, and restrictive assignment foreign key are retained.

The migration's Down path now checks all five new/nullable evidence fields under a transaction-held table lock before destructive schema changes.
It refuses rollback when assignment evidence or nullable concession values exist and does not fabricate empty concession IDs.
A real PostgreSQL fixture proves legacy financial rows remain unchanged on upgrade, legacy-only rollback works, each evidence field independently prevents rollback, and all current migrations leave no pending model change.
No staging migration or database reset was performed.

The updated Finance HTTP fixture uses synthetic enrolled-student prerequisites, real publication and preview/assignment endpoints, annual-total overrides, and retry assertions.
It does not claim to exercise admission approval.
A two-minute hang capture reproduced an assignment stall at the full auto-included student graph lookup.
The candidate replaces that lookup with a tracked student-root repository query after the existing authorization and scope checks.
HTTP assertions also verify the student fee-structure link persists.
A second captured stall in collection details was removed by projecting the existing student snapshot fields rather than materializing the auto-included graph.
The final HTTP run passes all seven Finance cases together with seventeen permission and ESI cases.
The audit also reproduced and fixed an Edit-only ESI classification request incorrectly requiring View through its response reader.
Independent Activate permission, scoped denial, cross-tenant employee IDs, and all eleven unproxied ESI application methods have explicit coverage.
Nine collection runtime regressions pass with canonical pinned-policy fixtures, including explicit rejection of a missing pinned policy.
No policy fallback or monetary assertion was weakened to make these tests pass.

## Delivered security scope

- Both permission-service entry points validate exact complete batches and current-tenant providers before ABP-managed writes.
- The retained frontend layout uses the full catalogue, exact independent actions, complete bulk subtrees, and changed-only verified direct-grant saves.
- Flat HR catalogues, missing navigation mappings, future actions, and nested visibility flags remain reachable without mixing navigation and action grants.
- Dirty drafts survive failures and provider changes; pending writes disable edits and closing.
- ESI employee operations require the exact action plus employee scope, without unrelated Employee Edit.
- Scope resolution reads current database state with unit-of-work-local reuse and mutation invalidation.
- Default grants are initialized only when a role is created.
- Existing users retain passwords and memberships on seeder reruns.
- Fresh Clerk and Office Assistant roles receive HR leave self-service only; their other module defaults are unchanged.
- FullDemo ESI remains Ready and does not create or reactivate HR-wide role scope.
- Academic-derived synchronization retains ownership boundaries and does not overwrite manual assignments.
- Canonical security guidance, AGENTS instructions, PR templates, and CI regression jobs document these rules.
- Deployment workflows require the security job and serialize releases; backend migration/seeding follows prerequisite checks.

## Current validation matrix

Only completed September 16 results support the synchronized candidate.
Earlier results remain useful historical evidence but do not replace a current gate.

| Case | Result | Evidence or limitation |
| --- | --- | --- |
| Current-main FE/BE synchronization and conflict resolution | Passed | Both aligned at the hashes above; remote main hashes rechecked after implementation; original Security layout retained |
| Scope, baseline-role, seeder, Finance calculator, and deployment-wiring domain regressions | Passed | 71 tests, `rc-0916-domain.trx` |
| Academic/manual scope ownership, Finance migration contract, and Finance workspace application tests | Passed | 49 tests, `rc-0916-application.trx` |
| Real PostgreSQL upgrade/protected rollback, migration baseline, and FullDemo ESI EF tests | Passed | 8 tests, `rc-0916-ef.trx`; zero skipped |
| HTTP permission and ESI boundaries, PostgreSQL grant/seeder preservation, scope latency | Passed | 16 tests, `rc-0916-security-http.trx` |
| Finance assignment HTTP plus independent ESI and permission boundaries | Passed | 7 Finance cases plus 17 permission/ESI cases in `rc-0916-permission-audit-http.trx`; zero failed/skipped |
| Expanded real-PostgreSQL permissions, all ESI application boundaries, and seed preservation | Passed | `rc-0916-permission-audit-postgres.trx`; one aggregate fixture, zero failed/skipped |
| Pinned-policy collection reads, late fees, disabled late fees, and missing-policy rejection | Passed | 9 tests, `rc-0916-collection-runtime-r3.trx` |
| Final retained-layout, Security, HR, and shared frontend regressions | Passed | 636 tests in 109 files, `rc-0916-permissions-final-vitest.log`; focused role suite has 46 tests |
| Current backend and frontend production builds | Passed | `rc-0916-editor-api-publish.log` and `rc-0916-permissions-final-build.log` |
| Final TypeScript and touched-file lint | Passed | `rc-0916-permissions-final-typecheck.log` and `rc-0916-permissions-final-eslint.log`; both exit zero |
| Deployment certificate/startup and workflow wiring regressions | Passed | 4 tests in `rc-0916-startup-r2.trx` and 1 test in `rc-0916-final-workflow-r2.trx` |
| Security, HR, and Finance documentation | Passed | FE Security/HR/Finance and BE HR/Finance validators; paired candidate paths used for cross-repository links |
| Student documentation baseline | Failed | Seven pre-existing broken source/doc references on `main`; unchanged files, outside this security patch |
| Final whitespace | Passed | `git diff --check` in both candidate repositories after final evidence updates |
| Current fresh FullDemo and repeat-run identity/grant/scope preservation | Passed | `rc-0916-fresh-verification.log`; 27 seed steps, restricted office-role defaults, one Ready ESI, no HR Manager role scope, unchanged rerun hashes |
| Current five-browser retained-layout behavior and smoke | Passed | 35 cases, zero failed/skipped/flaky/retried, 11.6 minutes; `rc-0916-permissions-final-browser.log` |
| Final authenticated Codex smoke | Passed | Final production UI and API, three viewports, retained separate dialogs, ESI actions, nested visibility, inherited access, and focus return; read-only |
| Full catalogue behavior and 44px dialog actions | Passed | Catalogue/model regressions, five-browser workflow matrix, and measured Codex footer targets at every requested viewport |
| Every action in every Student, Employee, and Payroll operation | Not run | Representative permission coverage is not exhaustive module acceptance |
| Deployed finalized August payroll readback | Not run | No deployed payroll write or calculation change authorized |
| GitHub checks, push, merge, and deployment | Not run | No publication authorized for this candidate |
| Live access repair or deployed write-based acceptance | Not run | Explicitly excluded |

Backend logs/results are under ignored `.artifacts/security-tests`.
Frontend logs/results are under ignored `.artifacts/security-local`.
The PostgreSQL migration fixture refuses non-loopback, non-`payroll_tests_*`, or nonempty databases rather than resetting them.

## Browser and retained-layout boundary

The replacement-editor redesign remains withdrawn.
The approved implementation fixes behavior inside the existing Role/User screens, three-panel permission dialog, and separate visibility dialog.
Its acceptance suite performs local role grant/revoke/save/reopen round trips, direct-user grant removal with inherited access retained, search-hidden bulk operations, and unchanged-grant comparisons.
It also covers failed reads/writes, keyboard operation, dirty Cancel/Escape/history, pending-save controls, focus return, contained scrolling, and measured 44px footer actions.
The final Chromium, Firefox, WebKit, mobile Chrome emulation, and mobile Safari emulation projects each pass seven cases.
The JSON report under frontend `e2e/security/.artifacts/security-browser/results.json` records 35 expected, zero unexpected, zero skipped, zero flaky, and no runner errors.
The mobile Safari project is slow on the local runner, taking five minutes for seven cases, but no retry or timeout extension was used in the final run.

Authenticated Codex confirmation uses the same production UI and backend build and finishes with a fresh page read after the automated suite.
All five Statutory Schemes actions and nested Payroll Management / Settings visibility are reachable in the original separate dialogs.
User ESI actions clearly identify inheritance from HR Manager and state that direct-grant removal leaves inherited access effective.
No grants were submitted during this final manual confirmation, and Cancel returned focus to More actions.

| Codex viewport | Document width | Cancel / Submit height | Footer bottom |
| --- | --- | --- | --- |
| 390 x 844 | 390 px | 44 / 44 px | 828 px |
| 768 x 1024 | 768 px | 44 / 44 px | 1008 px |
| 1440 x 900 | 1440 px | 44 / 44 px | 884 px |

Both footer buttons were wider than 80 px at every viewport.
The document did not overflow horizontally; content scrolling remained inside the dialog.
The temporary Codex viewport override and agent-created tab were cleared after confirmation.

All write-based checks use a disposable loopback FullDemo database and local mail sink.
They create isolated synthetic roles and use the already seeded synthetic HR Manager account with verified role membership.
The full user catalogue is compared before and after the direct-grant round trip.
No staging grants, scopes, employees, compensation, ESI activation, or payroll were changed.

The existing identity-create path accepted the browser fixture's roleNames but left it without role membership.
The test setup was corrected to use verified FullDemo membership instead of pretending that inheritance existed.
This does not validate or fix the separately scoped identity role-assignment issue.
The pre-existing Leadership dashboard actions-load error also remains outside this patch.

## Runner and warning notes

The machine-default .NET installation has runtime 10 only; two final no-build checks initially aborted before executing tests.
Both passed using the existing project-local .NET 9 environment, matching the target runtime and CI SDK major version.
The final frontend unit run reported happy-dom request-abort and loopback network teardown warnings while all 636 tests passed.
The backend application test build retains an unrelated nullable warning in `AdmissionCertificationScenario_Tests.cs`.
These warnings are not represented as clean-log validation or new release regressions.
Backend cross-repository documentation links were checked through isolated junctions pointing to the matching candidate checkouts.
The shared worktree alias points to another feature, and Windows PowerShell has a long-path limitation in the isolated layout; PowerShell 7 passed against the correct pair without changing documentation assertions.
Frontend Finance validation uses the same paired paths rather than resolving its backend references against an unrelated worktree.
Its two references to the removed fixed-action helper were updated to the delivered catalogue model and tests.
Student documentation validation still reports seven baseline failures in unchanged README and admissions-reference files: absent onboarding documents, stale CRM/test source paths, and a missing Excalidraw asset.
Those references were confirmed present in the current `main` versions; no unrelated Student documentation rewrite is included.

## Performance evidence

The original hierarchy request exceeded the 30-second client timeout; its query was still active after 106 seconds before cancellation in a disposable fixture.
There is no valid original 100-request p95.
The September 16 HTTP/PostgreSQL gate measures 35, 50, and 1,000 synthetic employees after five warm-ups with 100 complete response reads and bounded query counts.
Its test output in `rc-0916-security-http.trx` is authoritative for measured values.
The measured query count is eleven for every population size.

| Employees | First request | Warm p95 |
| --- | --- | --- |
| 35 | 923.69 ms | 28.90 ms |
| 50 | 19.77 ms | 19.61 ms |
| 1,000 | 29.09 ms | 32.77 ms |

Only the first fixture has a process-cold first request; later fixtures reuse the process.
This uses test authentication in the HTTP pipeline, not deployed network/OpenIddict latency.
No scope index migration was introduced.
The Finance schema migration is a separate dependency, not a hierarchy performance change.

## Publication refresh on September 16

The publication candidate also incorporates backend main `fca55adde62a29cb308c19d67468d1da1390d3d0` and frontend main `1f59f2a1b9ec6aa44db96724c80395c779cec81b`.
The integration retains both employee-scope application methods and does not restore the upstream existing-role grant mirroring.
Refreshed local checks passed 53 domain tests, 20 application tests, 48 HTTP tests, frontend TypeScript, and 633 frontend tests across 110 files.
The earlier 35 browser cases are retained evidence from before this integration, not a newly executed browser matrix.

The first backend PR run exposed test-process interference in the migration model assertion.
The ABP fixture enables Npgsql legacy timestamp mappings process-wide, whereas the migration factory uses modern mappings.
A standalone model comparison passed; explicitly reproducing the ABP switch produced 1,135 timestamp-only differences and no other SQL changes.
The workflow now runs migration/model checks and the FullDemo ABP fixture in separate processes, with all previous assertions retained and an additional model-diff diagnostic assertion.
The workflow regression test requires both independent invocations.
The corrected local invocations passed seven migration/baseline/model cases and two FullDemo repeat-run cases, with zero failures or skips.
This correction introduces no production timestamp change or database migration.

The first frontend PR run also exposed a documentation reference to an ignored local browser report.
The documentation now identifies it as generated local evidence rather than a required clean-checkout file.
The documentation validator itself is unchanged.

Latest PR checks and deployment outcomes must be read from backend PR #624 and frontend PR #1245 rather than inferred from this historical local report.

## Audit and deferred risks

Earlier read-only DDVS grant/scope exports remain in restricted, ignored `.artifacts/security-local/restricted-audit`.
They are historical audit evidence, not proof of current deployed grants or intent.
Broad grants are not automatically classified as incorrect, and uncertain provenance remains uncertain.
No deployed grants, manual scopes, passwords, or memberships were repaired.

The user explicitly deferred rotation of the exposed PostgreSQL credential.
The exposure remains a security risk; database reset and removal from a tracked file do not revoke a password.
The existing ABP user-role assignment policy mismatch also needs a separately scoped review.
This validation did not demonstrate a successful role-assignment bypass.

Generated `next-env.d.ts`, TypeScript build includes, logs, dumps, authentication state, and local tooling must remain outside the release commit.
