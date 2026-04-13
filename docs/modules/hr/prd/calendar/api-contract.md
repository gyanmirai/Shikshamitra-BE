# Calendar -- API Contract

## Authentication

Bearer token + `__tenant` header required. `EventTypeController` and `HolidayController` enforce `[Authorize]`; other calendar controllers require authentication at the application level.

---

## Endpoints Overview

### Events (`api/calendar/events`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/calendar/events` | List events (paginated, filterable) |
| GET | `/api/calendar/events/{id}` | Get event by ID |
| POST | `/api/calendar/events?academicYearId={id}` | Create event |
| PUT | `/api/calendar/events/{id}` | Update event |
| DELETE | `/api/calendar/events/{id}` | Delete event |
| GET | `/api/calendar/events/by-type/{typeId}` | List events by type |
| GET | `/api/calendar/events/by-status/{status}` | List events by status |

### Event Types (`api/calendar/event-types`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/calendar/event-types` | List event types (paginated) |
| GET | `/api/calendar/event-types/{id}` | Get event type |
| POST | `/api/calendar/event-types` | Create event type |
| PUT | `/api/calendar/event-types/{id}` | Update event type |
| DELETE | `/api/calendar/event-types/{id}` | Delete event type |

### Holidays (`api/calendar/holidays`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/calendar/holidays` | List holidays (paginated, filterable) |
| GET | `/api/calendar/holidays/{id}` | Get holiday |
| POST | `/api/calendar/holidays?academicYearId={id}` | Create holiday |
| PUT | `/api/calendar/holidays/{id}` | Update holiday |
| DELETE | `/api/calendar/holidays/{id}` | Delete holiday |
| GET | `/api/calendar/holidays/by-type/{typeId}` | List holidays by type |
| GET | `/api/calendar/holidays/by-status/{status}` | List holidays by status |

### Holiday Types (`api/calendar/holiday-types`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/calendar/holiday-types` | List holiday types (paginated) |
| GET | `/api/calendar/holiday-types/{id}` | Get holiday type |
| POST | `/api/calendar/holiday-types` | Create holiday type |
| PUT | `/api/calendar/holiday-types/{id}` | Update holiday type |
| DELETE | `/api/calendar/holiday-types/{id}` | Delete holiday type |

### Calendar View (`api/calendar`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/calendar/view` | Unified calendar view (events + holidays) |
| GET | `/api/calendar/items/{itemId}` | Get calendar item details |

---

## Endpoint Details

### GET /api/calendar/events

**Query params (GetEventListRequestDto):**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| academicYearId | Guid | no | Filter by academic year |
| search | string | no | Substring match on title |
| typeId | Guid | no | Filter by event type |
| mode | int | no | 0=Online, 1=Offline |
| status | int | no | 0=Upcoming, 1=Active, 2=Completed |
| fromDate | DateTimeOffset | no | |
| toDate | DateTimeOffset | no | |
| skipCount | int | no | ABP pagination |
| maxResultCount | int | no | ABP pagination |
| sorting | string | no | |

**Response (200):** `PagedResultDto<EventDto>`

```
EventDto {
  id: Guid
  tenantId: Guid?
  academicYearId: Guid
  title: string
  eventTypeId: Guid
  eventType: EventTypeDto?
  description: string?
  poster: string?
  mode: int                      // 0=Online, 1=Offline
  venue: string?
  meetingDate: DateTimeOffset?
  startTime: string              // HH:mm
  endTime: string                // HH:mm
  isRecurring: bool
  recurrenceJson: string?
  audienceIds: Guid[]
  status: int                    // 0=Upcoming, 1=Active, 2=Completed
  creationTime: DateTime
  lastModificationTime: DateTime?
}
```

---

### POST /api/calendar/events?academicYearId={id}

**Query params:** `academicYearId` (Guid, required)

**Request body -- CreateUpdateEventDto:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| title | string | yes | max 256 |
| eventTypeId | Guid | yes | |
| description | string | no | max 500 |
| poster | string | no | |
| mode | int | no | 0=Online (default), 1=Offline |
| venue | string | no | |
| meetingDate | DateTimeOffset | conditional | Required for meeting-type events; forbidden for others |
| startTime | string | yes | `^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$` |
| endTime | string | yes | same regex |
| isRecurring | bool | no | |
| recurrenceJson | string | no | |
| audienceIds | Guid[] | no | |

**Response (201):** `EventDto`

**Status codes:** 201, 400 (validation), 404 (event type not found)

---

### POST /api/calendar/event-types

**Request body -- CreateUpdateEventTypeDto:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| name | string | yes | 1-256 chars |
| color | string | yes | valid hex (#RRGGBB or #RGB) |
| description | string | no | max 500 |

**Response (201):** `EventTypeDto { id, name, color, description, tenantId, creationTime }`

---

### POST /api/calendar/holidays?academicYearId={id}

**Query params:** `academicYearId` (Guid, required)

**Request body -- CreateUpdateHolidayDto:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| holidayName | string | yes | 1-256 chars |
| holidayTypeId | Guid | yes | |
| startDate | DateTimeOffset | yes | |
| endDate | DateTimeOffset | yes | must be >= startDate |
| description | string | no | max 500 |
| audienceIds | Guid[] | no | |
| attachments | string | no | |

**Response (201):** `HolidayDto`

```
HolidayDto {
  id: Guid
  tenantId: Guid
  academicYearId: Guid
  holidayName: string
  holidayTypeId: Guid
  holidayType: HolidayTypeDto?
  startDate: DateTimeOffset
  endDate: DateTimeOffset
  description: string?
  audienceIds: Guid[]
  attachments: string?
  status: int                    // 0=Upcoming, 1=Ongoing, 2=Completed
}
```

---

### POST /api/calendar/holiday-types

**Request body -- CreateUpdateHolidayTypeDto:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| name | string | yes | max 256 |
| color | string | yes | `^#[0-9A-Fa-f]{6}$` |
| description | string | no | max 500 |

**Response (201):** `HolidayTypeDto { id, name, color, description }`

---

### GET /api/calendar/view

**Query params (GetCalendarViewRequestDto):**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| academicYearId | Guid | yes | |
| eventTypeIds | Guid[] | no | |
| holidayTypeIds | Guid[] | no | |
| status | int | no | |
| search | string | no | |
| skipCount | int | no | ABP pagination |
| maxResultCount | int | no | ABP pagination |

**Response (200):** `PagedResultDto<CalendarViewItemDto>`

```
CalendarViewItemDto {
  id: Guid
  title: string
  calendarItemType: int          // 0=Event, 1=Holiday
  color: string
}
```

---

### GET /api/calendar/items/{itemId}

**Response (200):**

```
CalendarItemDetailsDto {
  id: Guid
  calendarItemType: int          // 0=Event, 1=Holiday
  status: int
  title: string
  date: DateTimeOffset?
  time: { startTime, endTime }?
  audienceCount: int?
  eventType: EventTypeDto?       // only for events
  holidayType: HolidayTypeDto?   // only for holidays
  mode: int?                     // only for events
  venue: { type, meetingLink?, address? }?
  description: string?
}
```

**Status codes:** 200, 404
