# ADR-002: Employee distributed caching

## Status

Accepted

## Context

Employee reads are frequent (auth flows, dashboards, repeated lookups by user id or code). Every read hitting the database created a bottleneck under concurrency (e.g. many logins or list refreshes).

## Decision

Apply a **cache-aside** pattern with **ABP `IDistributedCache`** (Redis-compatible):

- **Primary cache**: serialized employee summary (`EmployeeCacheItem`) with fields needed for common reads; denormalize `RoleName` where it avoids extra joins after hydration.
- **Lookup caches**: mappings UserId -> EmployeeId and EmployeeCode -> EmployeeId (short TTL where appropriate).
- **Key naming**: centralized provider (e.g. patterns like `e:id:{id}`, `e:uid:{userId}`, `e:code:{code}`) for consistency and targeted invalidation.
- **TTL**: ~15 minutes for full employee cache entries; shorter for lightweight lookup entries where documented.
- **Invalidation**: on create/update/delete affecting an employee, invalidate that employee's keys (and related user/code keys) via `IEmployeeCacheManager` (or equivalent).

Repository methods try cache first, then load from DB and populate cache.

## Consequences

**Positive**

- Large reduction in database read volume on hot paths (historical summary: **~90%** DB load reduction at high cache-hit rates).
- Sub-millisecond-style responses on cache hits vs tens of ms from DB.

**Negative**

- Stale reads possible within TTL; mitigated by invalidation on writes.
- Operational dependency on distributed cache availability and serialization compatibility when changing `EmployeeCacheItem` shape.

## Key details

- Cache types must be serializable; use wrapper types for value types (e.g. `Guid`) where required by the cache API.
- Invalidation must cover all code paths that mutate employee identity or lookup keys.
