# ADR-005: Earning-component dependency graph serialization

## Status

Accepted

## Context

Active earning components form a tenant-scoped dependency graph.
Every dependency target must remain active, and the graph must remain acyclic.
Validation based on an unlocked snapshot cannot protect these invariants when two requests mutate different component rows concurrently.
For example, reciprocal updates can each validate before either write becomes visible, and a dependency creation can race with retirement of its target.

## Decision

Serialize every earning-component dependency graph mutation with one lock per tenant.
Create, update, activate, deactivate, and confirmed delete operations must acquire the lock before reading or validating graph state.
These application operations must run inside an ABP transactional unit of work.
The PostgreSQL repository acquires `pg_advisory_xact_lock` with a stable signed 64-bit key derived from a fixed namespace and the current tenant identifier.
PostgreSQL transaction ownership keeps the advisory lock until commit or rollback, including the interval after the application method returns.
The repository rejects PostgreSQL lock acquisition when no database transaction exists.
The process-local tenant lock stays held through transactional unit-of-work disposal and supplies deterministic serialization for non-PostgreSQL test and development providers.
The process-local lock is not the cross-instance production authority.
Impact previews remain read-only and do not acquire the mutation lock.
Every mutation revalidates the submitted graph state after acquiring the lock.

## Consequences

Concurrent dependency changes for one school execute in a deterministic order across application instances.
A reciprocal update race cannot commit a cycle.
A dependency creation race with target retirement cannot leave an active component pointing to an inactive component.
Mutations for different tenants do not intentionally block each other.
The stable 64-bit key can theoretically collide, but a collision only adds serialization and does not weaken correctness.
Requests can wait behind another dependency graph mutation for the same tenant.
The PostgreSQL implementation depends on transactional unit-of-work behavior and must fail closed if that contract is lost.

## Verification

Application tests use separate scopes and units of work to race reciprocal updates.
Application tests also race dependency creation against target retirement.
Both tests verify the final persisted graph, not only the returned error code.
Application tests prove that sequential mutations can reenter the same tenant lock inside one transaction.
