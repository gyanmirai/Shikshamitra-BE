---
module: academic
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-11-22
owner: backend-team
audience: humans-and-agents
---

# Trace Academic guide workflows

TL;DR: read visit status to decide whether to present the guide, record only an actual guide/configuration entry, and save advisory year progress separately from operational configuration.
Screen and route behavior belongs to the [frontend workflows](../../../../Shikshamitra-FE/docs/academic/workflows.md).

## First school entry

1. An authenticated school account reads visit status.
2. The server validates active identity and determines whether any of the nine setting Views is granted.
3. A general Academic module visit performs no metadata write.
4. On actual guide or configuration entry, the caller posts `welcome`, null or the exact setting key.
5. The server checks the corresponding View and records actor/time only if no school marker exists.
6. The first successful writer receives `firstVisit: true`; subsequent visitors receive false and the preserved timestamp.

Two clients may both have read an unvisited snapshot.
The POST result resolves that race; the read result alone is not a reservation.
Automatic presentation must not treat a losing POST as a new first visit.
An existing marker applies across accounts, browsers and academic years.

## Resume and configure a year

1. Select a tenant-owned academic year and optionally a medium and class.
2. Read year state with those context IDs.
3. The server validates the hierarchy and caller scope, returns shared progress and queries only permitted settings.
4. Save cursor/deferred changes with the returned AcademicYear stamp.
5. Submit real configuration changes through their existing endpoint family.
6. Refetch guide state in the same context to reflect qualifying records.

Saving guide metadata alone cannot persist a partially filled form or change an `empty` setting to `configured`.
Weekly lectures and lecture slots require a class selection; the other guide statuses require a medium.
Changing context changes the queried configuration state without changing the school visit marker.

## Defer and review

An authorized caller may leave known viewable settings deferred.
Hidden deferred keys remain stored when a partial-permission caller saves their visible subset.
A valid stage may have a null current setting.
Only `finishReview: true` at the `review` stage records server review actor/time.
Changing cursor or deferred membership clears that evidence.
Review does not certify domain readiness and does not require all nine settings to be populated.

## Recover from conflicts or invalid state

| Condition | Recovery |
|---|---|
| First-visit compare-and-swap collision | Server retries with a fresh unit of work and returns the immutable winner |
| Visit retry budget exhausted | Refetch visit status before deciding whether an entry request still needs retrying |
| Stale AcademicYear stamp | Refetch current progress/context, preserve newer changes and resubmit deliberately |
| No configuration View | Do not record a visit or request year progress |
| Wrong year/medium/class relationship or missing scope | Correct the selected context or have an administrator review access |
| Invalid stored metadata | Surface the error for administrator investigation; do not clear the marker or reset configuration |

No recovery path reseeds identities, restores grants or resets school records.

## Release Academic notifications

1. Apply the additive notification migrations before deploying the backend and website together. Keep `Notifications:QueueWorker:Enabled` false on all API replicas during the initial rollout. Academic Notifications shows the paused worker configuration for the API replica answering its catalogue request. Staff can check the catalogue and template editor while the dispatch and reminder workers are paused.
2. Use an isolated test school for the first send. Grant Notification View to reviewers, Add to people who manage rules or templates, and Send to people who confirm messages from Academic records. Each source still requires its owning permission and current school scope.
3. Verify that the backend's `App:ClientUrl` points to the HTTPS website for the environment. The renderer checks links in sample previews and outgoing messages against that website's origin. Initialize and preview the draft template variants, then publish the intended variants per recipient, channel and language. An external channel needs its school provider setup. WhatsApp also needs an approved binding and consent, and website push needs a supported device with an active subscription.
4. Enable the queue worker in the test environment. Start with one published Homework reminder or notice for a designated class and test contacts. Verify the primary guardian, any selected eligible extra relationships or student account, channel exclusions, and each recorded delivery outcome. Use the [notification tests](testing.md#notification-catalogue-coverage) for catalogue evidence.
5. Enable the approved rules by school, event and scope after provider and device checks. Review inherited rules at the intended class or division. Expand by event family after its source and audience checks. The [catalogue contract](api.md#catalogue-availability) lists events that still need an owning workflow.

For an existing backend database, start the reviewed idempotent script range immediately before the earliest missing Academic notification migration. A database can already have later Library and Payroll migration IDs. Check the actual migration history against the [verified upgrade range](testing.md#academic-notification-migration-rehearsal) before applying it.

## Monitor Academic notification delivery

The health worker visits one page of active schools every five minutes while `Notifications:QueueWorker:Enabled` is true. Look for these events in the API host logs.

| Event | Read it for |
|---|---|
| `AcademicNotificationQueueHealth` | Due intents and the age of the oldest one. For WhatsApp, it also reports unmatched or unapplied receipts and the age of the oldest pending receipt. |
| `AcademicNotificationChannelHealth` | Pending, due, processing and unknown-acceptance counts per school and channel. It also reports the oldest due age, scheduled retries, provider throttling and quota delays, plus failure, expired-subscription and dispatch-exclusion counts for rows updated in the last hour. |
| `AcademicNotificationQueueRecovery` | Recovered intent and dispatch leases and expired unclaimed work after a successful maintenance turn. |
| `AcademicNotificationPlanningExclusion` | Saved consent and channel-setup exclusions by school, channel and reason after the planning transaction commits. |

Queue ages are in seconds. A missing age means there is no matching work. A channel that has no matching work is absent from the channel samples. Use the latest sample per school and channel when reviewing queue health across replicas. Route operational alerts through the platform log monitor. These log events carry counts, school IDs and fixed reason codes.

A failed reminder scan logs the school, source key and failure type. It keeps that source's saved position, defers its retry for two minutes, and allows other due sources to continue. Each page read has its own time budget so the worker can record a timeout and continue within the school turn.

## Pause Academic notifications

Set `Notifications:QueueWorker:Enabled` to false on all API replicas to stop new worker turns. A turn already processing work may finish. Check Delivery History for queued, in-progress and provider-accepted work. The pause leaves the existing queue and delivery evidence in place. Source capture and reviewed confirmation can continue creating queued work while the workers are paused.

Platform operators can set `Notifications:QueueWorker:Tenants:<tenantIdN>:Paused` to `true` to hold new intent and delivery claims and reminder scans for one school. Set `Notifications:QueueWorker:Tenants:<tenantIdN>:Channels:<channel>:Paused` to `true` to hold just that channel. Use the school's tenant ID without hyphens and a channel name of `InApp`, `Email`, `WhatsApp`, or `WebPush`. Apply the same values across worker replicas and set the scoped key to `false` to resume it.

Scoped pauses preserve queued state and attempt counts. Already claimed work may finish. Workers continue receipt reconciliation, lease recovery, and normal expiry. The staff catalogue and reviewed-send responses report the current pause settings for the school.

Disable the affected automatic rules, including scoped overrides, if the school also needs to stop capturing new notices. Cancel eligible pending operations through Delivery History when their work should end. Provider-accepted messages stay in history. Confirm any application rollback against the applied schema and retain the additive tables and business records. Before resuming workers, review remaining queued work and the current rules, recipients, templates and providers.
