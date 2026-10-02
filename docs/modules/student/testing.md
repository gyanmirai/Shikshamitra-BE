---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-10-01
owner: backend-team
audience: humans-and-agents
---

# Validate Student Enrollment Admissions changes

## Student Attendance verification

| Gate | Focus |
|---|---|
| Domain | `test/ShikshaMitra.Domain.Tests/Students/Attendance/`: 32/32 passed on 1 October 2026 after the final review, with no skips. Covers policy precedence/ambiguity, schedules, calendar closure/fail-closed behavior, provisional missing calendars, dated membership/range boundaries, common calculation and conflicting historical records. |
| HTTP | `test/ShikshaMitra.HttpApi.Tests/Students/Attendance/`: 27/27 passed on 1 October 2026 against the fresh final Application build, with no skips. Covers complete/partial roster, atomic invalid batches, stale/idempotent writes, individual schedules, review/no-self-approval, unknown start, calendar closure, settings authorization, school-day leave overlap, audited retroactive leave credits, immutable correction previews and stale leave-source approval rejection. Offset POST dates match date-only GET roster revisions. Tenant-wide, narrow-division and empty scope checks accompany real leave create/list/approval. Fixtures use formal enrollment and scalar academic queries. |
| Actual provider | `test/ShikshaMitra.EntityFrameworkCore.Tests/EntityFrameworkCore/StudentAttendancePostgres_Tests.cs`: passed on 1 October 2026 against a newly created empty loopback PostgreSQL database. Verified migration/backfill, unchanged Student identity, ambiguous history preservation, enrollment/date uniqueness, untouched rollback/re-upgrade and used-feature rollback guard. Requires a new empty attendance_tests_* database; an environment-skipped run is not provider verification. |
| Integration | Check register/profile/daily/dashboard agreement, a historical class move, null/provisional metrics, approved leave treatment and current-school target/cutoff. |
| UI | [Frontend Student checks](../../../../Shikshamitra-FE/docs/student/testing.md) own focused tests, lint/typecheck and authenticated browser walkthrough. Source checks do not replace browser evidence. |

Run focused checks from the backend root:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~Students.Attendance
dotnet test test/ShikshaMitra.HttpApi.Tests --filter 'FullyQualifiedName~StudentAttendanceApi_Tests|FullyQualifiedName~AttendanceSettingsAuthorizationApi_Tests|FullyQualifiedName~StudentLeaveApi_Tests'
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests --filter FullyQualifiedName~StudentAttendancePostgres_Tests
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
```

Only the disposable database supplied through `STUDENT_ATTENDANCE_TEST_POSTGRES` may be used for provider fixtures.
Never migrate, reset or seed an existing school database to obtain test results.
If a debugger locks normal Host outputs, build into isolated outputs; do not treat a running old Host as proof of the current implementation.

Final HTTP evidence is `.artifacts/attendance-verification/attendance-final-api.trx` and `.log` (27 executed, 27 passed; 10.5901 minutes including shared FullDemo startup). Application SHA-256 is `D28B1C567F38E328E25CD24446D0C35D11C3C74539F587E1277AF0332D24F587`; all six copied production assemblies were checked against their source outputs before runtime. The earlier 20-pass/7-fail run is retained as `attendance-before-date-scope-fixes.*`; its date-kind roster mismatch and tenant-wide leave-list mismatch were fixed rather than bypassed. Application, EntityFrameworkCore and HTTP compilation passed, alongside the focused Domain and actual-provider gates above. Authenticated browser inspection is limited to the frontend notes; live school save/reload and correction/report acceptance still require a walkthrough on the final Host build.

## Admission fee selection checks

The 1 October 2026 focused verification passed 26 Domain tests, 23 Application tests and four HTTP contract tests.
Application coverage includes the real Payment-first and Offer-first runtime, persisted selection and payment locks,
and Onboarding completion remaining blocked until the saved selection is confirmed despite settled Finance dues.
The focused Application build temporarily excluded an unrelated broken Student Records test without changing its source.
A subsequent normal build was attempted after that file was repaired by separate work; it is blocked by
`StudentRecordRenewalIntegration_Tests.cs:61` (`FeeHeadModule`) and `:70` (`ConcessionFeeHeadScope.All`).
These results do not certify the complete current test project or deployment readiness.

The signed-in local browser check on 1 October used isolated published QA references and synthetic
application `APP-2026-000002`. The selected published revision and mandatory/optional heads persisted
through reload. A simulated INR 200 admission cash receipt locked the selection and preserved existing
automatic progression; the later Offer retained the same selection. After admission finalization,
explicit Onboarding confirmation persisted through another full reload. No real money, external
acknowledgement or Student Finance assignment was created. Enrollment activation, Finance assignment
and full Onboarding completion were not exercised in this browser run; exact assignment matching and
completion gates remain covered by the focused integration tests.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~AdmissionFeeSelection|FullyQualifiedName~AdmissionPaymentStagePolicy"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~AdmissionFeeSelection|FullyQualifiedName~AdmissionFeeStructureReader|FullyQualifiedName~Fee_selection|FullyQualifiedName~OnboardingFeeReadinessReader"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~AdmissionFeeSelectionApiContract"
```

Coverage must include actual stage-entry ordering, both Offer/Payment orders, published class/year references, optional heads, missing selection gates, application concurrency, persistence reload and locks after pending payment.
Verify denied read capability, Onboarding-only access before handoff, tenant and academic scope, confirmation against the exact Finance revision and included heads, and provisioning completion after explicit confirmation.
Existing Payment, Offer and Finance-readiness tests remain required regression checks because selection does not replace their monetary or lifecycle rules.
HTTP reflection contract tests prove routes and declared action grants; they do not establish denied requests through a deployed host.

## Student Records checks

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~StudentRecord"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Students.Records|FullyQualifiedName~Record_|FullyQualifiedName~Directory_|FullyQualifiedName~Legacy_override_|FullyQualifiedName~Student_record_profile_|FullyQualifiedName~Enrolled_profile|FullyQualifiedName~StudentRecordsSettings_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~StudentDirectoryApiContract_Tests|FullyQualifiedName~StudentRecordWorkflowApiContract_Tests|FullyQualifiedName~StudentRecordsAuthorizationApi_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~StudentRecordsApplicationPostgres_Tests"
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~StudentRecordsPostgres_Tests"
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

### Final automated evidence (2026-10-01)

| Check | Result and limits |
| --- | --- |
| Domain | 25/25 passed, no skips. Includes policy, transition, change, report/duplicate and legacy-entry invariants. |
| Application | 113 selected cases verified across the combined run, focused repairs, six new apply-serialization regressions, two combined guardian/renewal journeys, four readiness error-contract cases and eight report release cases, with no skipped cases. The combined run passed 90/93; two graduation fixtures required actual certificate voiding before correction, and the repeated-return fixture required one published school policy and a preview request key. Both graduation cases and the return case subsequently passed; the added serialization selection passed 6/6, guardian journey selection passed 2/2, error-contract selection passed 4/4 and report release selection passed 8/8. This is not a single clean full-suite run. |
| HTTP contracts | 22/22 passed, no skips. Route/action-grant metadata, not authenticated deployed requests. |
| HTTP authorization pipeline | 13/13 passed, no skips. Real middleware/controllers/services with isolated SQLite data, the existing test authentication handler and header-based permission checker. This does not verify production OpenIddict or persisted grant-provider behavior. |
| Actual PostgreSQL | 1/1 passed, no skips, against fresh `records_tests_20261001_030417`. Tests the database mechanisms below, not complete application-service execution under PostgreSQL. |
| PostgreSQL application commands | 1/1 passed, no skips, against fresh `records_tests_app_968845d32e6c4a43a6d18bbe36ec3dd6`. Real HTTP settings, historical entry approval, preview and simultaneous transition commands; natural provider conflict, rollback, capacity/roll revalidation and idempotent replay. Uses test authentication/grants and model-created tables. |
| PostgreSQL renewal activation | 1/1 passed, no skips, against fresh `records_tests_renewal_11e44978987c461582531369ab48d9fe`. Two real HTTP activation requests for one PendingChecklist case; natural serialization conflict, one target enrollment/event, stable GR, stale rejection and retained Finance completion gate. Uses test authentication/grants and model-created tables. |
| PostgreSQL approval stage | 1/1 passed, no skips, against fresh `records_tests_approval_c980885677314b8f90d39877f4b8a5dd`. Two persisted reviewers concurrently approve the same stage; one commit and one natural optimistic HTTP 409, one stage/event, requester denied, stale stamp rejected and refreshed remaining review scheduled. Uses test authentication/grants and model-created tables. |
| PostgreSQL same-year return | 1/1 passed, no skips, against fresh `records_tests_return_05a768e2557e451a9da756666f6194c7`. Concurrent same-key ReuseProfile requests create one batch/return/review; replay before and after activation preserves IDs, and activation reopens the original enrollment once with stable GR and roll. Uses test authentication/grants and model-created tables. |
| PostgreSQL scheduled workers | 2/2 passed, no skips, against fresh `records_tests_midnight_0f79cec4326341b0aa7d169dcf7e15ff`. Actual change/transition worker selectors and application commands at the India institution-midnight boundary, recorded actor revocation and repeated ticks. Uses an isolated mutable clock and test authentication/grants. |
| PostgreSQL overlapping change workers | 1/1 passed, no skips, against fresh `records_tests_worker_5e74ef8aaf2d4af0b0ad9ba0a594d679`. Two actual worker scopes select one due change and reach separate serializable writes; natural provider conflict, preserved Applied winner, stable repeated tick and unchanged identity/placement/enrollment. Uses the isolated clock and test authentication/grants. |
| PostgreSQL overlapping transition workers | 1/1 passed, no skips, against fresh `records_tests_tworker_0084e6fac4c74803b65d0448217e2bb2`. Two actual worker scopes reach serializable enrollment updates for one effective withdrawal; natural provider conflict, one Applied event, preserved winning result and unchanged identity/placement. Repeated ticking preserves saved history and stamps. Uses test authentication/grants and the isolated clock. |
| Populated local migration copy | 1/1 passed, no skips, against `records_tests_copy_b491993868d642f7880542231f937a1e`, restored from the verified pre-Records local backup. All original columns in 478 tables / 16,440 rows, including 271 Students, and original indexes/constraints were preserved through exactly three migrations and idempotent reapply. This copy has zero enrollments. |
| Current API build | Host build passed with zero warnings/errors after refreshing shared contracts and Application dependencies. |
| Documentation | Student validation passed. The all-module validator stops on a mojibake issue in unchanged `docs/modules/finance/references/charge-management-e2e-test-plan.md`; that unrelated Finance document was not modified. |

TRX evidence is under the respective test project's `TestResults`: `records-domain-complete.trx`, `records-application-verified.trx`, `records-workflow-repairs.trx`, `records-sameyear-final.trx`, `records-http-verified.trx`, and `records-postgres-final.trx`. The workflow-repairs run passed both graduation cases and initially failed the return preview's missing request key; the sameyear-final run verifies the corrected return fixture.

`records-http-authorization.trx` records the separate 13-case HTTP pipeline run. It proves denied exact actions despite related View/Create grants, anonymous 401 and ungranted 403, disabled database identities despite a valid principal, tenant non-disclosure, forged admin claims without database scope, and forbidden placement/promotion targets within otherwise permitted academic hierarchies. Rejected targets leave no request, batch, transition or target enrollment and preserve the source class. The initial run exposed a real narrow-class scope bypass; the feature-local target guard now uses explicit wildcard/exact division grants. Scope BusinessExceptions retain the existing HTTP 400 plus `UserScope:AccessDenied` contract. Final no-mutation assertions project only the canonical Student class field, avoiding full navigation-graph materialization that exhausted SQLite temporary resources on earlier attempts. These tests neither change live grants nor establish production token authentication.

After tightening target scope, `records-target-scope-allowed-paths.trx` verifies four existing Application cases again, all passing with no skips: permitted placement validation plus Finance-backed class-impact rejection, paid annual renewal preserving both enrollments, repeated same-year returns preserving enrollment/renewal/Finance history, and reviewed short-renewal return with idempotent linkage. These are reruns within the 113 unique selected cases above, not four additional unique tests.

`records-serialization-apply.trx` records six passing cases in `StudentRecordApplySerialization_Tests.cs`. They run real apply services and repositories on a copied ordinary SQLite connection with a real EF transaction and transactional ABP unit of work. A one-time raw/wrapped provider-neutral SQLSTATE `40001` fault is injected after writes are flushed: student/enrollment/contact changes roll back, the case retains ConcurrencyConflict, a second automatic apply does nothing, explicit transition retry or replacement change succeeds, and unrelated exceptions propagate. The ordinary Application fixture uses `AddAlwaysDisableUnitOfWorkTransaction`; these tests override that behavior locally and restore their connection configuration afterward. Earlier attempts correctly failed rollback assertions against the nontransactional fixture. This is injected-error handling and real SQLite rollback proof, not a natural PostgreSQL application race.

Domain checks cover structural rules, immutable snapshots, condition matching, masks, approval ordering, self-approval, explicit retry, renewal state, shared owner roll-rule resolution and frozen proposals. Application scenarios execute real services for school/year policy precedence, stale updates, disabled database actors despite authenticated claims, canonical profile/scope, legacy entry, private photo expiry, changes, duplicate review, guardian reviews, issued documents, report jobs, exit idempotency, scheduled/future exits, corrections, and renewal/return.

`StudentRecordRenewalIntegration_Tests.cs` uses actual Finance collection services, persisted dues, a pinned collection policy and assignment snapshot, partial/full payments, and preserved enrollment/GR identity. Required-portal coverage checks wrong mailbox denial, actual Student/Parent account creation, owner action grants, verified saved email, idempotent linking, completion after required handoffs, and a persisted pending KeepUntilReview access review. It does not prove the complete guardian-change, access-review-resolution and renewal browser journey. The repeated-return case completes a paid annual renewal and two reviewed withdrawals/returns in the same year, reuses the same enrollment and Finance references, retains three completed reviews, and rejects a different-class return with an immutable fee assignment.

`records-guardian-renewal-journey.trx` records two passing cases from `StudentRecordGuardianRenewalJourney_Tests.cs`, one per SuspendUntilReview / KeepUntilReview school policy. Real SaveDraft/Publish and independent approval create a request-linked access review; a later opposite policy does not change the submitted snapshot. Pending review blocks a paid required-portal renewal under both modes. Wrong existing identity and unverified retained identity are rejected; the wrong-identity path uses a copied SQLite database and an explicit transactional unit of work to prove that flushed link revocation rolls back. Verified resolution restores readiness, completes renewal and preserves the sibling link and shared active guardian account. The fixture marks the parent's email confirmed directly; separate owner tests cover account setup. This is a combined service journey on SQLite, not browser execution or PostgreSQL guardian execution.

`records-readiness-owner-codes.trx` verifies four preview error-contract cases. Qualified Onboarding codes (`Students.Onboarding.DivisionFull` / `RollNumberDuplicate`) and existing colon-qualified codes retain their owner contract; only local short codes receive the Records prefix. The actual PostgreSQL command gate exposed the previous double-prefix projection defect after its concurrency assertions had already succeeded.

`records-report-release-gates.trx` verifies eight real-service cases with the actual production `ShikshaMitraPermissionChecker`, persisted ABP user grants and cache invalidation wired into the report graph. Removing the generation Add grant after completion still permits download with current Reports.View/source View; removing source View denies download. Removing a database identity-reader role defeats a stale role claim. Stricter masks invalidate completed/queued jobs, and newly generated XLSX XML contains the restricted identity instead of the original name. Completed/queued expiry blocks download/start. Revocation of Add/source View or expiry after actual workbook generation prevents completion and blob publication. The runner now rechecks Add and expiry before saving the file. Test construction supplies the unit of work normally provided by the ABP proxy for manual GetList calls; shared fixtures are unchanged. This is SQLite service/real permission-provider proof, not production OpenIddict HTTP or distributed cache/concurrency proof.

Actual-provider checks migrate a synthetic historical Student through all three Records migrations without changing identity; inspect concurrency metadata and the active-only renewal index; permit completed history plus one active review; reject a second active review; and guard downgrade when historical reviews cannot fit the old unique index. Two genuine SERIALIZABLE transactions read the same reservation predicate: the competing commit returns PostgreSQL `40001` and only one reservation persists. Additional checks prove identity/request rollback after a foreign-key failure, concurrent request-key uniqueness, stale-stamp rejection, populated-governance downgrade refusal, and unused-feature downgrade/reapply. These checks do not establish concurrent preview/approval/enrollment/roll allocation through the complete application. No whole-model snapshot-match claim is made because unrelated Attendance model changes remain pending in the shared checkout.

`records-postgres-real-application.trx` separately proves one complete concurrent application scenario: two independently reviewed historical students receive the same automatic roll proposal for one final seat, then submit real promotion commands on two PostgreSQL SERIALIZABLE connections. A test-only SQL barrier coordinates genuine inserts after predicate reads without changing commands or injecting an exception. The naturally observed `40001` yields one Applied winner and a loser rejected with RollProposalChanged; loser batch/transition/target enrollment and source mutations roll back. Winning-key replay returns the same IDs; a fresh loser preview reports the unchanged Onboarding DivisionFull contract. Both GR numbers remain stable. The factory refuses existing/non-loopback databases, retains its fresh database and creates tables from the current model; the separate migration gate remains authoritative for migration SQL. Test-only SplitQuery avoids an eager scope-seeder graph timeout encountered before Records commands in the first attempt. This case uses test authentication/header grants, not production tokens, and does not cover every approval, return, renewal or scheduled-job race.

`records-renewal-race.trx` adds one actual PostgreSQL renewal activation race. Historical entry and school/Onboarding configuration use real HTTP commands; the owner permits optional returning-student identity/family/contact evidence while permanent GR, placement and Finance completion remain mandatory. Two serializable requests reach the actual enrollment insert on distinct PostgreSQL connections. One wins; the natural `40001` loser receives `Students:Records:ConcurrencyConflict` after rollback. Exactly one target enrollment, one renewal and one RenewalActivated event persist; the source enrollment becomes inactive and GR remains unchanged. Reusing the stale stamp is rejected; a fresh stamp still requires missing Finance evidence before completion. The first run exposed a generic HTTP 500 for the loser; the existing Onboarding serialization classifier now translates that failure in the Records activation command. The repaired case passed with no skips. The barrier only coordinates real SQL and injects no failure. This covers PendingChecklist activation, not concurrent completion, return, approval or deployed authentication.

`records-approval-race.trx` verifies concurrent approval of stage one under a published two-stage school policy. Both separate reviewers have persisted database role membership in the isolated tenant; the requester is denied self-review. The actual ReadCommitted transition updates are coordinated on distinct PostgreSQL connections. One review commits and the optimistic loser returns HTTP 409; exactly one stage-one event and its winning actor persist, while Student status, GR and active source enrollment remain unchanged. A stale review then returns the Records concurrency code. Refreshing the remaining reviewer advances only stage two, records both reviewers once and schedules the future withdrawal without applying it. Production code and live grants were unchanged. The barrier now accepts the expected transaction isolation while retaining Serializable defaults for existing promotion/renewal tests. This covers concurrent approve/approve at one stage, not approve/reject, renewal completion, returns, worker deployment or production token/grant behavior.

`records-return-race.trx` verifies a same-year ReuseProfile return after a real HTTP withdrawal. Two same-key requests reach actual serializable writes on distinct PostgreSQL connections; one wins and the natural `40001` loser is rejected. One batch, return transition and PendingChecklist review persist; the source remains inactive and Student remains Left until reviewed activation. Request replay preserves transition/review IDs both before and after activation. Activation reaches Provisioning by reopening the original enrollment ID without an additional enrollment, changing class/division/roll or replacing GR; one RenewalActivated event persists. Optional returning-student evidence is configured through the real Onboarding owner API. Production code and live grants were unchanged. This does not prove Finance history preservation/completion, concurrent activation of that return, or FreshApplication/ResumeStage concurrency.

`records-midnight-boundary.trx` records two passing cases through actual PostgreSQL HTTP commands and the protected change/transition worker ticks. Both remain unchanged at 23:59:59 IST; a tick at 00:00 IST applies the scheduled contact change and effective-date withdrawal, and repeated ticks preserve saved stamps and one Applied transition event. Disabling the recorded database actor before the due tick instead persists Change Conflict / Transition Exception with AuthorityChanged and preserves contact, Student and enrollment state. The Changes worker now reads the same `IClock` as its application and the Transition worker. The first SQLite attempts exposed a large test proof query and unsupported DateTimeOffset selector translation; proof reads now use scalar projections and the final gate runs the unchanged real selectors on PostgreSQL. The factory reuses the strict fresh/loopback protections and test-only SplitQuery. It verifies due eligibility using the existing Asia/Kolkata calendar; live workers poll every minute, so this does not promise execution at the exact second or prove concurrent worker deployment behavior.

`records-worker-race.trx` verifies two actual change-worker ticks, each with a separate service scope, selecting the same due scheduled contact correction. Real HTTP commands establish historical entry, publish school rules and schedule the change. Both applications reach serializable contact updates on distinct PostgreSQL connections; PostgreSQL naturally raises `40001` for one. The committed change remains Applied with no conflict code, one change/contact row and no fabricated approval decisions. Student identity/placement fields and the enrollment stamp remain unchanged. Another actual tick preserves the complete saved state, including parent/change stamps and contact audit metadata. The first test run incorrectly required the parent stamp to stay unchanged during a valid contact edit; the final check compares business fields before/after and saved stamps after the repeat tick. Production code was unchanged. This proves overlapping worker scopes on one due contact edit, not separate deployed hosts, guardian side effects, transition-worker overlap or live polling timing.

`records-transition-worker-race-localization-failure.trx` preserves the first failed transition-worker gate. The HTTP validation stack raised an ABP localization exception before the first legacy-override command entered Records business logic: the physical Development resource folder contained both `en.json` and the untracked Attendance resource `en-attendance.json` declaring culture `en` in one JSON contributor. That fresh database contained no transition or transition-event rows. The user authorized the shared repair: all 35 Attendance translations were consolidated into the canonical English resource, all 630 existing translations were verified unchanged, and the duplicate file was removed. The Domain.Shared rebuild passed with zero errors and seven warnings. This initial failure was a shared localization integration failure, not evidence for or against the worker race.

`records-transition-worker-race.trx` verifies the repaired run through historical-entry approval, published school rules, future withdrawal preview/submission and actual transition-worker execution. A pre-midnight tick preserves the scheduled case and active roster. At the effective institution date, two separate worker scopes reach enrollment writes on distinct serializable PostgreSQL connections; the provider naturally raises `40001`. The winning case remains Applied without an error, the original enrollment becomes inactive, the Student becomes Left, and exactly one Applied event records the original actor. Identity, GR, roll and placement remain unchanged; another actual tick preserves the complete saved result and history. The Application DLL still matched SHA256 `6724A27552D68D305A457451DE5B88676DA8FD4A153DCE28719767A9BACB1913`; the rebuilt Domain.Shared DLL was `DF870E0B586E92370FD27E23756BB8E93D1741BF9CFDB0D7E3CF2D22CB4138E3`. This covers one Direct-policy effective withdrawal, not every transition outcome, separate deployed hosts or live polling timing.

`records-populated-copy-migration.trx` verifies preservation on a separately restored local backup (SHA256 `A44DC66F75B09FC6877E32081EB3DB98E42EE3002D7A787FDF4A94753B04056F`). The test refuses non-loopback targets and requires the strict disposable-copy name and exact pre-Records migration. It hashes sorted JSON of every original column so additive certificate metadata is allowed without hiding data rewrites, compares all original indexes/constraints, requires exactly the three Records history additions and rechecks after idempotent migration retry. No seeder or live-database write runs. The retained copy contains 271 Students but zero enrollments; populated enrollment, Finance and issued-document history still require a representative pilot copy alongside the synthetic provider tests.

`records-admission-return-race.trx` passed **1/1**, zero skips, in approximately three minutes on the disposable database `records_tests_aret_7a29effaeee64cddb62a1aa1325de87a`. `Record_postgres_fresh_admission_returns_serialize_same_and_distinct_keys_without_duplicate_journeys` verifies same-key Inquiry requests and distinct-key Direct Registration requests through authenticated HTTP. Each case reaches two serializable PostgreSQL connections, observes a natural `40001`, creates exactly one return/batch/handoff/applicant/owner journey, and replays identical linked IDs. The losing distinct key cannot create another pending return. Every source business column, actor/history column, and complete personal-detail row remains unchanged; adding the dependent transition advances only the Student/enrollment relationship audit revisions. This matches [ABP 9 relationship tracking](https://github.com/abpframework/abp/blob/9.0.0/framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/EntityFrameworkCore/ChangeTrackers/AbpEfCoreNavigationHelper.cs). Complete persisted rows, including audit revisions, stay identical after winning-key replay and a rejected competing retry. The barrier synchronizes the first parent-batch insert before a unique key can block the second request. Fixture validation uses the actual authenticated Offering availability endpoint; test-only HTTP timeouts are four minutes for constrained resources. This gate covers Fresh Application creation/replay, not concurrent finalization, Resume activation, multiple deployed hosts, or terminal-return closure. Production code, school data and live grants were not changed for this gate.

### Local runtime and browser evidence

After verified local backups, the three migrations were applied to localhost PostgreSQL (`localhost:5432/postgres`): `20260930175531_AddStudentRecordGovernanceAndOperations`, `20260930182702_AddStudentRecordTransitionTargetScope`, and `20261001021100_AddStudentRecordRenewalHistory`. The last migration only replaces the renewal unique index with an active-only predicate; completed/cancelled history remains intact. The current isolated Host was rebuilt and restarted; no seeders or staff-grant changes were performed. This is local verification, not a production rollout.

After the scheduling and report guards, the verified Records Host was refreshed to PID 26064. That Host build passed with zero warnings/errors. Its Host DLL SHA256 matched the one used by the passing midnight gate (`8E8B8ECD2C1E009F1FA3B9337DF92F1C40690DF366E3EFE965DBDB7493D679CF`), and its Application DLL matched the passing report-release runtime (`623F55844F5E70E762E18B576CEA110B3E797E89B9F0B7C283E78DD8C825AB39`). The authenticated browser reloaded Directory and Settings/Change Rules; Directory/options and Settings/roles requests returned HTTP 200. The selected school/year directory remained empty. Settings showed the saved draft and starter rules; no policy was saved or published during that smoke check.

After the renewal race fix, the Application build passed with zero errors and 130 warnings. The repaired PostgreSQL test passed against Application DLL SHA256 `6724A27552D68D305A457451DE5B88676DA8FD4A153DCE28719767A9BACB1913`. The local Host was refreshed to PID 2028 with a zero-warning/error Host build, and its Application copy matched that test runtime. Authenticated Directory/options reads returned HTTP 200 and the browser displayed the expected empty state against this Host. Both refreshes ran no migrations, seeders or grant changes.

The signed-in local browser loaded Directory, all four Settings task links, document policy choices, Certificates/Identity cards, and Reports. Temporary guardian-access edits were discarded after verifying dirty navigation protection. A real Directory report job reached Completed; its 6,344-byte Excel download returned HTTP 200 with the expected media type. The selected directory was empty, so this proves a zero-row export. Directory and Settings were rechecked against the refreshed Host. Direct Changes and Transitions navigation displayed No permission for the existing role. Directory, Settings overview, transition settings, Documents certificate/history and Reports preview were visually reviewed at 390×844, 768×1024 and 1440×900; a clipped mobile pagination footer was repaired and rechecked. Full student mutations, populated exports, document issuance and populated responsive browser journeys remain unverified.

### Known limitations and release gates

The latest browser connection recovered after earlier interaction timeouts. The existing signed-in school account rendered Directory's Active and Former views with filters, table headers, pagination and the expected zero-row empty state; neither view reported captured page errors/warnings. The user confirmed the school is still in testing with no expected record data. Year-End & Transitions still rendered No permission, and the sidebar omitted Transitions/Changes for this role. This verifies access denial and empty Directory rendering; it does not verify populated profiles or the return screens. No live student, policy or permission changes were made.

Class changes with an existing immutable Finance assignment are blocked at Placement request/application and same-year Return readiness/activation with `Students:Records:FinancialPlacementImpactRequired`. Finance currently has no class-impact/reassignment contract; its readiness reader cannot certify that an old assignment matches a changed class. The user deferred wrong-class renewal fee correction from this implementation. Before activation, PendingChecklist cancellation/recreation is available when no target dependencies exist. Completion cannot bypass these guards.

The approved Admissions return connection is implemented in source: Offering-configured Inquiry/DirectRegistration entry, explicit Both selection, frozen Records Status approval, server-bound configured intake and privacy masks, real New inquiry, AwaitingAdmission state, and tagged Onboarding return cases with reviewed same-year enrollment reuse. The focused frontend selection passed 20 tests across three files, scoped lint and affected-source TypeScript. The later full-project TypeScript check also passed with the project's configured 8 GiB heap; frontend-owned evidence is in [Student testing](../../../../Shikshamitra-FE/docs/student/testing.md). Browser retry attached successfully, but the signed-in role has no Transitions permission, so it displayed the expected denial screen; the full return journey remains unverified. Current Application source and referenced Domain/contracts build passed with zero errors after the final Resume options edit. The ten Domain transition tests passed without skips, including the three new return cases. EF source build including the additive return migration passed with zero errors; its idempotent SQL script generated successfully. Wrong-class fee correction is deferred.

The second nine-case Application return run (`records-admission-return-application.trx`) passed eight and failed the helper-only case (9 minutes 11 seconds; zero skips). Passing evidence includes actual configured Inquiry contact/qualification and application start/finalization, explicit Both entry, Records approval with self/unqualified/revoked-reviewer denials, public Direct finalization/activation/Finance collection/completion retry, public Resume finalization and activation, and independently reviewed historical first enrollment. Permanent GR/profile, shared guardian/sibling state and initial completed case history are asserted. The helper-only fixture omitted the save that real Onboarding activation performs before provisioning; its repaired focused rerun passed (`records-admission-return-helper.trx`, 3 minutes 30 seconds, zero skips). All nine unique return checks therefore passed across these focused runs; this is not a single clean nine-test run. An earlier run failed invalid Direct/Both fixture workflow configuration; production validators were preserved. These results do not establish production authentication, live browser journeys or fresh/resumed Admission return concurrency under PostgreSQL.

`StudentRecordAdmissionReturnMigration_Tests` passed both checks without skips (48 seconds; `records-admission-return-migration.trx`). The model check verifies the two affected Records entities match runtime and the target-model difference touches only the return-handoff/transition tables. The real PostgreSQL test used fresh `records_tests_admission_return_f4f7443d196745b694ce3d777ec899d9`, migrated from the preceding Attendance migration, preserved legacy duplicate application handoffs byte-for-byte, accepted a pending Inquiry with no Application ID, enforced new transition/application/inquiry uniqueness, refused a history-losing downgrade, and re-upgraded the unused fixture safely. This synthetic migration check does not replace representative school-copy or authenticated journey verification.

The reported local PostgreSQL `42703` for `AppStudentEnrollments.AttendancePlacementHistoryJson` was traced to the database ending at `20261001021100_AddStudentRecordRenewalHistory`. Applied only the existing owner migration `20261001043143_AddUniversalStudentAttendance` after a verified local backup. Transactional digest checks preserved Student, Identity, role membership, permission grants, Records tables and every original enrollment column; the migration's own Attendance bridge/backfill remains owner-defined. Both new enrollment columns and the applied migration history were verified. No seeders or host restart ran. Browser retry reached sign-in, so a signed-in dashboard/API retry remains unverified.

After the return migration/model checks passed, applied `20261001060643_AddStudentRecordAdmissionReturn` to the same verified local database with another inspected backup. Transactional original-row checks preserved Student/Identity/roles/grants, other Records tables, every existing enrollment and original Admission handoff data. All five additive handoff columns and the latest migration history were verified. The shared host was not restarted, and its live source/DLL freshness and authorized browser return journey remain separate checks.

Terminal Admission rejection or preactivation Onboarding cancellation currently leaves AwaitingAdmission open. Closing the linked Records case awaits the user's business choice; offer withdrawal/decline and reopenable Inquiry outcomes must retain their current owner lifecycle semantics. Required Inquiry Upload fields also retain the existing Admissions renderer limitation. Neither limitation is certified as a complete journey.

Automatic allocation reuses the Onboarding owner and supports validated compatible rules. Rules requiring roster recalculation, `preserveExisting=false`, first-academic-day/manual-bulk timing or destructive insertion remain blocked until owner workflows exist. Do not describe every advanced numbering configuration as operational.

Before production release, verify the new Admissions return entry/enrollment reuse journey and migration; verify migration/backfill on a representative copy with populated enrollment/Finance/document history; remaining mixed approval/Admissions-return/renewal-completion and other transition-outcome concurrency under PostgreSQL; production authentication and persisted permission grants; live guardian change and review resolution; live worker deployment/timing; distributed report authority/cache behavior; and authenticated full journeys across desktop, tablet and mobile. The passing local-copy preservation, promotion, renewal activation, approval-stage and same-year return races, midnight and overlapping change/withdrawal worker ticks, report release checks and SQLite guardian journey narrow these gates but do not complete them. Use authorized pilot roles and representative student data. Passing automated tests and local smoke checks do not certify these remaining gates. Wrong-class renewal fee correction remains deferred and must be governed by Finance if introduced later.

TL;DR: run domain, application contract, EF mapping, and HTTP contract tests for Admission Offering changes, then validate module docs and migration safety.

For onboarding service consent, run `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~Onboarding_service_consent_requires_an_audited_confirmation_without_bypassing_first_payment` and `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~StudentOnboardingApiContract_Tests`. The application scenario covers invalid and stale confirmation, an audited consent handoff, the still-required confirmed first payment, idempotent retry, and one enrollment; the HTTP contract checks route and permission. These do not replace live signed-record review or an authorized school-data walkthrough.

The room-derived class-capacity integration scenario allocates a persisted room, finalizes a direct RTE application, verifies the same occupied and remaining seats from a parallel General offering, and confirms that a full class still allows a new registration. Run `AdmissionWorkflowRuntimeIntegration_Tests` after changing routing or capacity; a calculator-only test does not cover the shared query. Applying the new migrations to a disposable PostgreSQL database and checking the live API remain release gates.

`20260924090000_AddAdmissionFormPurpose` uses active Offering references to classify existing Inquiry and Registration Forms; a name-based guess is only a fallback for unreferenced forms. If the same form is referenced in both slots, migration stops with a clear error instead of silently invalidating an active Offering. Resolve that existing-data conflict before rollout, then rerun migration verification against a disposable copy. The EF mapping test checks operation order and generates PostgreSQL Up/Down commands for all three new migrations without connecting to a database; only a disposable PostgreSQL migration run can prove SQL execution.

## Workflow route and document configuration checks

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~AdmissionWorkflowRouteDiagnostics|FullyQualifiedName~AdmissionWorkflowGraphValidator|FullyQualifiedName~AdmissionDocumentCollectionRules|FullyQualifiedName~AdmissionDocumentPolicyAndEvidence"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Managed_documents|FullyQualifiedName~Document_class_scope|FullyQualifiedName~Document_handoff|FullyQualifiedName~Document_finalization|FullyQualifiedName~Document_onboarding_replacement"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionDocumentApiContract
```

Route diagnostics reject identical simple conditions from one stage even when their priorities differ, unsupported managed Payment outcomes, and Offer/Seat outcomes excluded by the source policy. An Offer with Accepted disabled cannot use a default continuation; it needs a special outcome path. Complex preserved rules remain subject to the existing condition and graph validators.

The document integration files are partial members of `AdmissionWorkflowRuntimeIntegration_Tests`; filter by the method names above, not by their filenames. Confirm that the test report contains executed tests: a zero-test run can still return exit code zero.

On 25 September, this application filter executed and passed 24 tests with zero skips. Coverage includes early linked-Form collection and evidence reuse, class applicability, denied early review/unlinked upload, replacement review, private evidence scope, and finalization/handoff preservation and rollback. The Document HTTP contract suite passed nine tests. These are automated integration/contract results; live browser upload through finalization remains a separate acceptance check.

The focused `FullyQualifiedName~Rte_direct_registration_starts_at_form` selection subsequently passed both theory cases with zero failures or skips.
It verifies finalization from direct approval and from an accepted optional offer, and confirms an issued but unaccepted offer remains blocked.
Both cases execute actual finalization and Student/Onboarding linkage assertions.
The duplicate-offering fixture includes the required intake profiles so it continues to exercise duplicate rejection rather than failing earlier configuration validation.

The isolated Host build succeeded with zero errors and existing compiler/analyzer warnings.
Loading the current Host exposed missing intake-profile columns in the local PostgreSQL database.
After confirming `postgres` at `::1:5432`, a readable full custom-format backup was saved under ignored `.artifacts/backups/before-intake-profile-20260925.dump`.
The reviewed script applied only `20260925121515_AddAdmissionIntakeProfiles` from `20260925100000_AddAdmissionApplicationCounsellor`, in a transaction, with SHA256 `98DBADAE7B65C2771D7EAB6CBD7672EDE31B12F0201DCE43583D196CCA1D6FE0`.
It added two nullable Offering references, the intake-profile table, indexes, and foreign keys; the new table remained empty and no seeder ran.
The authenticated browser then finalized synthetic `APP-2026-000002` from its accepted optional offer and displayed the created Student identity and Continue onboarding link.
A scoped read-only query confirmed one retained PNG evidence row at Version 1 with its accepted review linked to that Student, and exactly one Student Onboarding case.
The browser opened that matching live case; the custom evidence had no document-category mapping, so this does not claim mapped onboarding-document completion or Student activation.
Student documentation validation and diff hygiene passed; the all-module documentation run is blocked separately by a mojibake error in the Finance charge-management test plan.

Document checks cover authoritative Offering class applicability, fail-closed missing class context, same-version forward Form collection links, shared evidence identity, and replacement invalidating prior review. Live API, permission-denial, finalization/handoff, and responsive browser acceptance remain separate release gates; domain-only success does not establish those outcomes.

## Admission Cycle validation

Run the focused domain and contract suites:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionCycle_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AdmissionCycleContract_Tests
```

Coverage includes code normalization, positive date ranges, Draft immutability boundaries,
publish/close/reopen/archive transitions, window revision, schedule ordering, institution-timezone
boundary evaluation, DTO validation, and the exact View/Add/Delete permission mapping. The frontend
pure-rule suite covers action availability and unused-Draft deletion.

Before release, generate and review the Admission Cycle migration, build the API independently from
any running debugger, and test one small, one medium, and one multi-board school configuration.
For each pilot, verify: future Academic Year selection; application dates beginning before the
Academic Year; open-date and close-date midnight behavior; publish readiness; active-Offering close
acknowledgement; reopen with extended date; archive dependency denial; stale concurrency denial;
server paging/search; View-only, Add, and Delete role behavior; and history visibility. Record any
board-specific result as configuration feedback, not a hard-coded board rule.

The migration `20260915105105_ImplementAdmissionCycleGovernance` is additive: it adds lifecycle and
schedule columns plus `AppAdmissionCycleHistories`. Automated tests and generation do not apply the
migration or restart the externally managed API host.

## Focused commands

Run from `Shikshamitra-BE`:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionOffering_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingContract_Tests
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingModelMapping_Tests
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingApiContract_Tests
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflowRuntimeIntegration
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

## Coverage map

| Surface | Evidence |
|---|---|
| Effective dates and availability boundaries | `test/ShikshaMitra.Domain.Tests/Students/Enrollment/AdmissionOffering_Tests.cs` |
| Bulk inactive/activation readiness and exact View/Add/Delete action aliases | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionOfferingContract_Tests.cs` |
| Defaults, nullability, indexes, restrictive FKs | `test/ShikshaMitra.EntityFrameworkCore.Tests/Students/Enrollment/AdmissionOfferingModelMapping_Tests.cs` |
| Required HTTP routes and concurrency inputs | `test/ShikshaMitra.HttpApi.Tests/Students/Enrollment/AdmissionOfferingApiContract_Tests.cs` |
| Track-aware same-class create/update rejection, shared room-derived capacity, non-blocking full-class warning, and inactive-enrollment seat release | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionWorkflowRuntimeIntegration_Tests.cs` and `AdmissionClassCapacityIntegration_Tests.cs` |
| Capacity read/configuration HTTP permissions and routes | `test/ShikshaMitra.HttpApi.Tests/Students/Enrollment/AdmissionOfferingApiContract_Tests.cs` |
| Same-cycle duplicate signals, scoped prior-cycle family linking, name-only rejection, and inaccessible-cycle denial | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionWorkflowRuntimeIntegration_Tests.cs` |
| Workflow graph, controlled conditions, handlers, and runtime state | `test/ShikshaMitra.Domain.Tests/Students/Enrollment/AdmissionWorkflowGraphValidator_Tests.cs` and `test/ShikshaMitra.Domain.Tests/Students/StudentEnrollments/Admissions/AdmissionWorkflowConditionEvaluator_Tests.cs` |
| Required-stage skip conflicts and runtime-unavailable publication blocks | `test/ShikshaMitra.Domain.Tests/Students/Enrollment/AdmissionWorkflowGraphValidator_Tests.cs` |
| Frozen published version, complete version-history definitions, per-version runtime analytics, transition history, retry idempotency, and future-application version selection | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionWorkflowRuntimeIntegration_Tests.cs` |
| Form version pinning and runtime lifecycle | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~Published_Form_Version_Should_Remain_Frozen` and `--filter FullyQualifiedName~Form_Runtime_Should_Save_Submit_Audit_And_Reopen` |
| Form aggregate lifecycle and soft-delete-safe defaults | `dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionFormDefinition` |
| Approved choice sources and authoritative option membership | Application build plus form runtime integration tests; source keys are allow-listed by `AdmissionFormDataSourceRegistry` and checked again during submit |

## Assessment validation

Use isolated output directories when the API host holds normal build output files open.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --artifacts-path .artifacts/assessment --filter "FullyQualifiedName~AdmissionAssessment|FullyQualifiedName~AdmissionWorkflowGraphValidator"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/assessment --filter "FullyQualifiedName~AdmissionWorkflowRuntimeIntegration|FullyQualifiedName~AdmissionAssessmentScope"
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --artifacts-path .artifacts/assessment --filter FullyQualifiedName~AdmissionAssessmentModelMapping
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/assessment --filter FullyQualifiedName~AdmissionAssessmentApiContract
```

Domain tests cover legacy policy compatibility, scoring bounds, attendance, review, retakes, waiver evidence, cancellation, and session capacity.
Application integration tests cover published managed workflows, blocked advancement, passing retakes, reviewer separation, wrong application/stage ownership, tenant isolation, scheduled capacity, stale stamps, and cancellation.
The scope query test checks that a narrow standard grant does not expand to another standard under the same board or another academic year.
Mapping and contract tests check persisted evidence, restrictive links, unique attempt numbers, concurrency, additive routes, and distinct permissions.
Generate and inspect the additive migration SQL without applying it to a shared database; verify the model snapshot matches the current EF model.

### Test infrastructure and lifecycle ordering

The certification fixture completes submitted-form stages before approving the application, pauses at the Approval stage, performs the actual review and approval decisions, and then completes the remaining workflow.
This preserves the production Form requirement for a submitted application and the Approval requirement for an approved application.
It refreshes application concurrency after workflow advancement before issuing an offer.

The shared HTTP factory no longer references the obsolete `AdmissionCrmTransactionFailureInterceptor` or seeds the deleted `AdmissionCrmRouteDefinition`; both belonged to the removed Admissions CRM HTTP suite.
Keep unrelated test-only interceptors, including SQLite owned-tag identity generation, intact when maintaining the factory.
Full HTTP tests bootstrap shared demo data and can take several minutes during academic permission scope synchronization. The focused reflection contract tests do not boot this host; report authenticated server checks separately from contract metadata checks.

## Managed Inquiry validation

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionInquiryPolicyAndManagement_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Managed_inquiry_should|FullyQualifiedName~Basic_inquiry_workflow_should|FullyQualifiedName~Inquiry_list_should_support_unassigned_and_counsellor_queues|FullyQualifiedName~Inquiry_communication_should_create_a_traceable_draft"
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~AdmissionInquiryManagementModelMapping_Tests
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionInquiryManagementApiContract_Tests
dotnet ef migrations has-pending-model-changes --project src/ShikshaMitra.EntityFrameworkCore/ShikshaMitra.EntityFrameworkCore.csproj --startup-project src/ShikshaMitra.DbMigrator/ShikshaMitra.DbMigrator.csproj --no-build
```

The inquiry runtime integration check covers unassigned and assigned queues, offering-scoped counsellor lookup, server-filtered first-contact and open-follow-up queues, list-row enrichment, editable preferred language, and a traceable one-recipient Communication draft. The HTTP contract check keeps edit, timeline, CSV export, bulk assignment, work-queue, row SLA/readiness, communication route, permission, request, and result fields public.

Application Experience coverage additionally verifies persisted form purpose, purpose-preserving clones and versions, purpose-safe Offering configuration, the seven canonical document-condition facts, and the nine lifecycle templates remaining in the Communication contract.

The 2026-09-11 focused verification passed 10 Domain policy/aggregate tests, two real Application lifecycle scenarios, one EF mapping test, and nine HTTP route, permission, and safe-contract checks.
The Basic scenario proves existing application start behavior remains available without an enabled Inquiry policy.
The Managed scenario proves contact and qualification gates, persisted linkage, and successful application start after readiness.
EF reports no model changes after the migration, and reviewed idempotent SQL contains only the two Inquiry tables, nine supporting indexes, restrictive foreign keys, and migration-history insert.
The migration has not been applied, and the externally managed running Host has not been restarted for this delivery.

## Payment validation

Payment policy and aggregate tests cover money precision, methods, installments, non-cash references, independent verification, rejection, gateway settlement, failure, reversal, waiver, and retry safety. The application lifecycle test proves one transaction survives record and verification retries, the same collector cannot satisfy dual control, pending payment blocks workflow completion, and verified settlement permits advancement. EF mapping checks cover restrictive foreign keys and unique reconciliation indexes; HTTP contract tests cover all five action permissions.

The additive Payment migration SQL was generated and reviewed from `20260911045603_AddAdmissionInquiryManagement` to `20260911064407_AddAdmissionPaymentRuntime`. It creates only `AppAdmissionPaymentTransactions` and its keys/indexes. It has not been applied.

## Acceptance gates

Document Verification checks use `.artifacts/document-verification` with `AdmissionDocumentPolicyAndEvidence` and `DocumentFileValidator` Domain filters, `Managed_documents`, `Document_handoff`, and `Document_finalization` Application filters, `AdmissionDocumentModelMapping` EF filter, and `AdmissionDocumentApiContract` / `StudentAdmissionEvidenceApiContract` HTTP metadata filters. Application tests execute real services and isolated test storage; HTTP reflection checks prove route/permission metadata, not an authenticated network request. Validate the additive migration script and pending-model state before any explicitly authorized database rollout.

Coverage includes legacy opt-in behavior, conditional missing/blank values, mandatory skip/override gates, stale stamps, replacement review reset, both-mode original checks, immutable reviews, independent collector separation, private downloads, tenant/stage scope, same-Student idempotent linkage, preservation of existing Student documents, and explicit transaction rollback during finalization.

The 2026-09-10 focused verification passed 92 Domain tests (documents, shared files, assessments, graph validation), 54 Application runtime/scope tests, 20 HTTP route/permission metadata checks, and 2 EF mapping checks. Application coverage includes rejecting an incomplete both-mode upload before original inspection, UTC evidence timestamps, and preserving the previous onboarding upload through rollback before cleaning it after a successful replacement. Generated migration SQL contains only the two new tables and supporting indexes/foreign keys; the EF model matches its snapshot. These automated checks do not substitute for a separately recorded authenticated browser/server smoke test.

Do not release if an active offering can bypass published configuration, an out-of-scope hierarchy is visible or writable, runtime versions are not frozen, identity changes after applications exist, or migration SQL drops historical data.

## Eligibility validation

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --artifacts-path .artifacts/eligibility-build --filter "FullyQualifiedName~Eligibility|FullyQualifiedName~AdmissionWorkflowGraphValidator"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/eligibility-build --filter "FullyQualifiedName~AdmissionWorkflowRuntimeIntegration|FullyQualifiedName~AdmissionAssessmentScope|FullyQualifiedName~SqliteEligibilityCalendarDate"
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --artifacts-path .artifacts/eligibility-build --filter FullyQualifiedName~AdmissionEligibilityModelMapping
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/eligibility-build --filter FullyQualifiedName~AdmissionEligibilityApiContract
```

Eligibility tests cover legacy/disabled policy compatibility, schema and calendar boundaries, unknown versus blank answers, decimal numeric equality, whole-choice membership, explicit upstream stage selection, linked-Student DOB freshness independent of the application stamp, immutable exception history, real configured role/separate reviewer checks, tenant and offering UserScope denial, negative routing, skip/override/finalization gates, successful finalization retry, and the real reopen/edit/resubmit/re-evaluate workflow. Document freshness tests append an external immutable correction after source completion to verify invalidation; normal document APIs continue to prevent historical edits.

Offering compatibility tests exercise exact application/inquiry form versions, create/update/activation rejection, incomplete inactive drafts, persisted mismatch readiness and list filtering, and legacy/disabled behavior. Keep captured query ID collections as `List<Guid>`: the current compiler can bind array `Contains` to a span overload that EF cannot evaluate. The SQLite application-test provider adds only the missing `DateTimeOffset.Date` translation; a focused SQL-filter test covers midnight, end-of-day, offset crossing and null. A separate Npgsql `ToQueryString` test verifies the mapped production date, membership and field-subquery predicates without opening a connection. Production queries continue to filter and paginate in SQL.

EF tests inspect tenant/concurrency mappings, unique evaluation sequence and exception decision keys, and restrictive foreign keys. HTTP tests inspect actual controller/service authorization metadata and response contracts; they do not claim an authenticated network request. Generate and review Eligibility's additive migration and verify the model snapshot before a separately authorized deployment.

Validation on 2026-09-10 passed 131 focused domain tests, 91 application regressions (including 54 existing admissions cases), 27 HTTP contract metadata checks, and four EF mapping/query-translation checks. The refreshed isolated host build passed, the model check found no pending changes, and all module documentation and diff checks passed. No tests were skipped. The initial offering regression failures identified a captured-array EF expression issue and SQLite's missing calendar-date member translation; both were corrected and the same assertions passed on the final run.

The reviewed guarded local rollout then applied `20260910133935_AddAdmissionEligibility` once to the local PostgreSQL database (`localhost:5432/postgres`, resolved to `::1/128`). Its result reported committed and verified: migration history contains 52 entries with this migration latest, both new tables are empty, and the schema has ten indexes including the two primary-key indexes plus six restrictive foreign keys. No seeds or unrelated migrations ran.

### Live verification on 2026-09-11

The API was restarted externally after the earlier automatic approval review rejected the agent's restart command.
That rejection remains historical evidence; a manual restart is no longer a pending action.

| Check | Observed result |
|---|---|
| Running host | PID 30256, parent 21436, started 07:38 IST; serves port 44332 from `src/ShikshaMitra.HttpApi.Host/bin/Debug/net9.0/ShikshaMitra.HttpApi.Host.dll` |
| Compiled implementation | Six running-output DLL/PDB identities match; all 57 checked Eligibility, Document, workflow/finalization and Offering source checksums match the current workspace |
| Public API metadata | Student Swagger returns HTTP 200 with normal TLS verification; includes six canonical Eligibility routes, seven Document Verification routes, current action flags, Student admission evidence and onboarding document routes |
| Database | The host has an established connection to `::1:5432`; a fresh read-only metadata query confirms local `postgres`, history 52, both migrations present, Eligibility latest, two Eligibility tables, ten indexes and six foreign keys |
| Authenticated setup reads | The Codex browser loaded real reviewer roles, published form choices and category metadata from the current API |

The existing offerings use legacy workflows, so a complete live managed applicant mutation journey has not been tested.
The Student admission-evidence live read initially failed because its profile client called a missing endpoint.
The profile repair described below is prepared in isolated output; the running debugger has not been rebuilt or restarted as part of that repair.
These coverage gaps remain distinct from the passing automated services/tests and the verified live setup reads.

## Student profile and admission-evidence integration

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/student-profile-readiness --filter "FullyQualifiedName~Student_record_profile|FullyQualifiedName~StudentRecordScope|FullyQualifiedName~Document_handoff|FullyQualifiedName~Document_finalization|FullyQualifiedName~Document_onboarding"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/student-profile-readiness --filter "FullyQualifiedName~StudentRecordProfileApiContract|FullyQualifiedName~StudentAdmissionEvidenceApiContract"
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj --artifacts-path .artifacts/student-profile-readiness
```

Profile service tests read canonical Student identity and placement, preserve null optional values, and prove that reads do not mutate the Student concurrency stamp.
They cover missing records, foreign tenants, absent scope, another standard in the same board, assigned and unassigned divisions, explicit wider grants, and the finalized Student GUID leading to accepted admission evidence.
The original narrow-standard denial assertion exposed an existing direct-record guard bug: ancestor IDs from narrow assignments were treated as wider grants.
The shared Student guard now checks only explicit wildcard grants or the exact division within the assigned academic year; profile and admission-evidence list/download denials use this guard.
Separate focused cases exercise each wider grant and the academic-year boundary.
Existing document handoff, finalization, onboarding readiness, and rollback checks run alongside the profile tests.
The HTTP tests inspect the explicit GUID route, both permission attributes, the result envelope and the scalar DTO allow-list; these are metadata checks, not authenticated network requests.
This repair requires no migration and does not modify the live database or running API process.

Final verification on 2026-09-11 passed all 36 selected Application tests, all six HTTP contract metadata tests, the isolated Host build, and the module documentation/diff checks, with no skipped tests.
The Application selection contains seven real profile scenarios, six explicit scope-grant/year cases, and 23 existing document handoff/finalization/onboarding regressions.

For onboarding configuration changes, validate schema version 1 and an object-shaped JSON payload, verify the filtered unique school and academic-year indexes in the generated migration, and exercise GET/PUT/DELETE concurrency behavior. Frontend persistence tests must also confirm that removing an academic-year override inherits the saved school configuration while removing the school override restores built-in defaults.
The prepared Host is `.artifacts/student-profile-readiness/bin/ShikshaMitra.HttpApi.Host/debug/ShikshaMitra.HttpApi.Host.dll`.
An external API restart on 2026-09-11 loaded the normal-output Host into PID 47960; the read-only proof matched the current profile and exact Student direct-record scope sources to the loaded build.
The public Swagger response included the GUID profile endpoint, and the process had established database connections only to local PostgreSQL at `::1:5432`.
The subsequent authenticated browser check populated the real Student name, GR number, roll number, class and medium, then loaded Documents and the expected empty linked admission-evidence result; Refresh also succeeded.
No Student or evidence mutation was performed during that live check.
The earlier restart restriction is historical and is not a current profile-delivery blocker.

## Review validation

Run from the backend repository:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --artifacts-path .artifacts/admission-review-build --filter FullyQualifiedName~AdmissionReview
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/admission-review-build --filter FullyQualifiedName~Review_
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --artifacts-path .artifacts/admission-review-build --filter FullyQualifiedName~AdmissionReviewModelMapping
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/admission-review-build --filter FullyQualifiedName~AdmissionReviewApiContract
```

The Application suite executes real service methods for draft/submit/decision separation, configured roles and assignees, tenant/stage/UserScope isolation, concurrency, immutable history, correction/resubmission, selected upstream facts, canonical identity and document freshness, explicit negative routes, skip/override denial, historical reaffirmation and successful finalization retries.
Offering compatibility tests exercise current reviewer role/activity, exact standard scope on create/update/activate, and disabled/legacy compatibility.
Run the existing application workflow, Assessment scope, Eligibility calendar, Student profile and direct-record scope selections alongside this change because they share decision, form and finalization paths.
HTTP tests inspect route/permission metadata and safe DTO boundaries; they do not claim authenticated network mutations.
Review migration `20260911034211_AddAdmissionReview` adds only the three Review tables, ten indexes and seven restrictive foreign keys; no seed data or existing records are changed by its Up operation.
Inspect the generated SQL and verify no pending EF model changes before any separately authorized rollout.

The 2026-09-11 automated verification passed 25 Review Application cases and 104 existing Application regressions, with zero failures or skips.
The combined Domain selection passed 157 tests, HTTP route/permission metadata passed 37 tests, and EF mapping/production-query translation passed five tests.
The initial Review test run exposed a missing condition-field registration; publication now explicitly supports only emitted `review.status` and `review.decision` fields and rejects invented Review fields.
Application evidence includes default-reviewer standard/role/activity compatibility, approval followed by skip denial, middle-name changes without an application-stamp change, and explicit negative decisions using the existing admission endpoints.
These isolated checks and frontend fixture checks do not establish a live configured applicant mutation journey.
The final isolated Host build succeeded with zero errors and seven existing compiler/analyzer warnings.
The compiled model has no pending changes, and the generated Eligibility-to-Review SQL contains only the reviewed additions and its migration-history insert within one transaction.
The prepared Host is `.artifacts/admission-review-build/bin/ShikshaMitra.HttpApi.Host/debug/ShikshaMitra.HttpApi.Host.dll`; loading it into the externally managed API remains a separate operational action.

The reviewed migration was committed and verified on local PostgreSQL (`postgres`, `::1/128`, port 5432) at 2026-09-11 03:53:53 UTC using a pinned SQL hash, a verified backup and transactional pre/post checks.
Migration history now contains 53 entries with `20260911034211_AddAdmissionReview` latest; its three tables have thirteen indexes including primary keys and seven restrictive foreign keys.
All three Review tables remain empty, no seeders ran, and no unrelated migration was applied.
The current API is still the older PID 47960; an external rebuild/restart must load the prepared Review implementation before authenticated Review HTTP behavior can be verified.
The successful profile/Documents read remains valid historical evidence and does not establish that the new Review endpoints are live.

## Managed Form validation

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionFormStagePolicy_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~Managed_Form_Should_Hold_The_Workflow_For_Verification_And_Audit_Corrections
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionFormStageApiContract_Tests
```

The Domain suite covers bounds, cross-field rules, section uniqueness, unsupported versions, and legacy opt-out. The real Application workflow test freezes a compatible form, submits without automatic advancement, verifies a section, requests correction, proves prior verification becomes stale, resubmits as attempt two, verifies again, advances, and checks immutable audit history. HTTP reflection tests cover all three routes, controller and service permissions, and safe DTO metadata. This feature reuses existing form persistence and needs no migration.

## Managed Offer validation

Run the focused Offer policy/aggregate/route tests, the real application lifecycle test, EF mapping test, and HTTP contract metadata tests. Coverage includes legacy opt-out, content and validity limits, issue retry safety, workflow blocking before response, acceptance confirmations, decline, extension, withdrawal, reissue, explicit negative-outcome routing, permission separation, safe DTOs, and the filtered idempotency index.

The 2026-09-11 focused run passed 26 Domain tests, one real Application workflow test, one EF mapping test, and seven HTTP contract tests with zero failures or skips. The DbMigrator and HTTP API builds completed with zero errors; existing repository warnings remain. Generated SQL from `20260911064407_AddAdmissionPaymentRuntime` to `20260911071841_AddManagedAdmissionOffers` adds only four nullable decision-history columns, one filtered unique index, and the migration-history row in a transaction. It has not been applied.

## Managed Seat validation

Run the focused Seat policy, allocation and route tests; the database-backed managed workflow tests; the EF mapping test; and the HTTP contract tests. Coverage includes legacy opt-out, policy bounds, automatic reservation on entry, capacity protection, idempotent retry, confirmation gating, FIFO waitlisting, promotion after capacity growth, post-advance release, explicit negative routing, tenant uniqueness, permission separation, and safe DTOs.

The 2026-09-11 focused run passed 16 Domain tests, two real Application workflow tests, one EF mapping test, and eight HTTP contract tests with zero failures or skips. The HTTP API build completed with zero errors. EF reports no pending model changes. Generated SQL from `20260911071841_AddManagedAdmissionOffers` to `20260911084131_AddManagedAdmissionSeats` creates only the two Seat tables, their foreign keys and indexes, and the migration-history row in a transaction. It has not been applied.

## Class capacity and counsellor validation (2026-09-25)

This review applied the current Admissions migrations to a newly created disposable localhost PostgreSQL database named `shikshamitra_admissions_disposable_20260925`; no live deployment or existing API restart was performed by this review. The disposable database's migration history contains 79 entries, ending at `20260925100000_AddAdmissionApplicationCounsellor`. Read-only checks confirmed the application counsellor column and that class-room allocation foreign keys reference `academic_Rooms`, `academic_Standards`, and `academic_Years`. The existing local `postgres` database was initially observed at 77 migrations and later at 80, including the two new Admissions migrations; the source of that concurrent change was not established, so do not assume it was unchanged. EF reported no pending model changes. The focused Application selection passed 25/25 tests, including RTE direct registration and managed-offer counsellor assignment; capacity boundary/integration selection passed 5/5; EF mapping/migration selection passed 6/6; and HTTP route/permission contracts passed 5/5. A later focused integration rerun also passed the explicit assertion that an RTE/Direct Registration offering cannot accept an Inquiry. Authenticated browser inspection reached the Applications list and direct-registration empty state, but no configured direct-registration offering was available for an end-to-end applicant journey.
