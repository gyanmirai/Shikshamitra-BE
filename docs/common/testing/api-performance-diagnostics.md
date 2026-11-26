# API performance diagnostics

The API host measures requests under `/api` without logging bodies, query strings,
authorization headers, SQL text or SQL parameters. Authentication, tenant and scope
checks still run normally. No database migration or additional service is required.

## Response timing

`Server-Timing` reports milliseconds measured when response headers start:

| Metric | Meaning |
|---|---|
| `app` | Time since the request entered the API timing middleware |
| `db` | Sum of EF command execution durations, including failed commands |
| `db-open` | Sum of completed EF connection-open durations, including provider/pool waiting inside Open |

The database metric measures command execution through EF events. It does not
include all reader consumption, EF model building, query compilation or non-EF
database calls. Concurrent durations can overlap; do not subtract their sum from
request duration to calculate an exact non-database duration.

With `PerformanceDiagnostics__IncludeRequestPhases=true` (on in Staging only),
the same header also shows completed pipeline phases:

| Metric | Meaning |
|---|---|
| `pre-auth` | Timing middleware to authentication, including routing, rate limiting and CORS |
| `auth` | Authentication through authorization, including tenant resolution, unit-of-work setup and dynamic claims |
| `post-auth` | Authorization to the endpoint boundary, including the general request middleware |
| `endpoint` | Endpoint dispatch, MVC filters, application work and start of the response |

A short circuit omits phases without both timestamps. The durations end at
response start, not the end of body transfer. Do not add `db` to the request phases:
it measures work that overlaps them. Azure queueing before middleware is not included.

## Slow-request log

Requests taking at least 2,000 ms produce one `SlowApiRequest` warning with:
`TraceId`, method, parameterized route, status, elapsed time, command count,
total command time, maximum command time and connection-open time.

`PerformanceDiagnostics__SlowRequestMilliseconds` overrides the threshold.
Zero logs every API request for a short diagnostic run; the default avoids that
volume. Invalid or negative values fall back to 2,000 ms. Normal EF SQL logging
and sensitive-data logging stay disabled.

## Investigating high browser TTFB

| Evidence | Next check |
|---|---|
| High `app` and high maximum command time | Database query plan, locks and database resources for that route |
| High command count and cumulative command time | Repeated reads and avoidable sequential round trips |
| High `db-open` | Connection-pool saturation and application-to-database connectivity |
| High `app` with low recorded database time | Unmeasured processing/dependencies, reader consumption, cold EF initialization or host contention |
| Low `app` with high browser TTFB | Time before middleware entry, hosting/proxy queues and network path |

These are investigation directions, not automatic diagnoses. Compare the same
request and check cold and warm requests separately before changing hosting,
pool limits, indexes or caching. Headers and warnings become available only
after the updated backend is deployed.

## Validation

`ApiRequestTiming_Tests` uses an actual SQLite command through EF diagnostics and
an HTTP test server to check timing headers, unchanged response behavior, absence
of request/SQL secrets in warnings, non-API bypass, concurrent-request isolation,
and configured phase timings with normal and short-circuited requests.

Source: `src/ShikshaMitra.HttpApi.Host/Diagnostics/` and
`src/ShikshaMitra.HttpApi.Host/Middleware/ApiRequestTimingMiddleware.cs`.
