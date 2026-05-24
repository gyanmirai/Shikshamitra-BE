# Calendar -- Behavior Specification

## Purpose

Manages school events and holidays within academic years. Provides a unified calendar view that merges events and holidays into a single feed, filterable by type and status.

Calendar holidays are neutral shared dates. Payroll and attendance policy modules interpret those dates separately for paid/unpaid treatment, attendance requirements, comp-off, overtime, and basis-day impact.

## Actors

| Actor | Description |
|-------|-------------|
| Admin | Creates/updates/deletes events, holidays, and their type master data |
| User | Views calendar, event details, holiday details |
| System | Auto-computes status (Upcoming/Active/Completed) based on dates |

---

## Behaviors

### 1. Events

1. Admin creates an event scoped to an academic year, with a title, event type, time range, mode (Online/Offline), and optional audience.
2. **Meeting events**: require a `meetingDate`. Status computed as Upcoming (future), Active (today), or Completed (past) based on `meetingDate` vs UTC now.
3. **Non-meeting events**: no `meetingDate`; status is always Active.
4. Events can be recurring (`isRecurring` + `recurrenceJson`).
5. Events can be filtered by type, status, date range, mode, or search term.

### 2. Event Types (Master Data)

1. Admin CRUDs event type categories (e.g., Meeting, Assembly, Sports).
2. Each type has a name, hex color, and optional description.
3. Color must be a valid hex code (`#RRGGBB`).

### 3. Holidays

1. Admin creates a holiday scoped to an academic year, with a name, holiday type, start/end date range, and optional audience.
2. Status computed automatically: Upcoming (before start), Ongoing (between start-end), Completed (after end).
3. Business rule: `StartDate <= EndDate` enforced in domain.
4. Holidays can be filtered by type, status, date range, or search term.
5. Holidays do not store payroll paid/unpaid behavior or attendance required/not-required behavior. Those are policy-owned decisions.

### 4. Holiday Types (Master Data)

1. Admin CRUDs holiday type categories (e.g., National Holiday, Festival, Declared Holiday).
2. Each type has a name, hex color (`^#[0-9A-Fa-f]{6}$`), and optional description.

### 5. Calendar View (Unified)

1. Returns a merged, paginated list of events and holidays for an academic year.
2. Each item shows: id, title, type (Event or Holiday), and color.
3. Item details endpoint returns full info including status, date/time, audience count, venue, type metadata, and description.
4. Filterable by event type IDs, holiday type IDs, status, and search term.

---

## Business Rules

- All calendar data is scoped to an academic year (`academicYearId`).
- Multi-tenancy: all entities implement `IMultiTenant`.
- Event/holiday status is computed at read time, not stored.
- Time format for events: `HH:mm` (24-hour, validated by regex).
- Holiday date range: `StartDate <= EndDate`.
- Event type and holiday type colors must be valid hex.
- The deprecated `EventAndHoliday` module (flat model) is superseded by this structured Calendar module.

---

## Permissions

No dedicated calendar permissions are currently defined. Endpoints use baseline `[Authorize]` (authenticated user) on `EventTypeController` and `HolidayController`. `EventController`, `HolidayTypeController`, and `CalendarViewController` do not enforce authorization at the controller level.

---

## Error Codes

| Code | Constant | When |
|------|----------|------|
| `HR:Calendar:040001` | EventTypeNotFound | Referenced event type does not exist |
| `HR:Calendar:040002` | EventMeetingDateRequired | Meeting date missing for a meeting-type event |
| `HR:Calendar:040003` | EventMeetingDateNotAllowed | Meeting date provided for a non-meeting event |
| `HR:Calendar:040004` | HolidayTypeNotFound | Referenced holiday type does not exist |

---

## Enums

| Enum | Values |
|------|--------|
| EventMode | Online=0, Offline=1 |
| EventStatus | Upcoming=0, Active=1, Completed=2 |
| HolidayStatus | Upcoming=0, Ongoing=1, Completed=2 |
| CalendarItemType | Event=0, Holiday=1 |

---

## Domain Events

None currently published by the Calendar submodule.

---

## Dependencies

- Academic Year -- events and holidays are scoped by `academicYearId`
- [Employees](../employees/prd.md) -- audience IDs may reference employees/groups
- [HR Policy Architecture](../../implementation/hr-policy-architecture.md) -- describes how payroll and attendance consume neutral calendar dates
