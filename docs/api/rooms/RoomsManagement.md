# Rooms Management API - Complete Guide

## 📋 Overview

The **Rooms Management API** is part of the **Institute Setup** module. It provides complete CRUD operations for managing buildings, room types, and rooms.

**Modules:**
- **Building Management**: Manage building information
- **Room Type Management**: Manage room types and their short names
- **Room Management**: Manage individual rooms with details

---

## 🚀 Quick Start

### 1. Run Database Migration
```bash
dotnet ef migrations add AddRoomsManagementEntities --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.HttpApi.Host
dotnet ef database update --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.HttpApi.Host
```

Or use DbMigrator:
```bash
dotnet run --project src/ShikshaMitra.DbMigrator
```

### 2. Start the API
```bash
dotnet run --project src/ShikshaMitra.HttpApi.Host
# Navigate to: https://localhost:44365/swagger
```

### 3. Import Postman Collection
- Collection: `postman/RoomsManagement.postman_collection.json`
- Environment: `postman/ShikshaMitra.postman_environment.json`

---

## 📊 API Endpoints

### Building Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/institute-setup/rooms-management/buildings` | List all buildings |
| GET | `/api/institute-setup/rooms-management/buildings/{id}` | Get single building |
| POST | `/api/institute-setup/rooms-management/buildings` | Create building |
| PUT | `/api/institute-setup/rooms-management/buildings/{id}` | Update building |
| DELETE | `/api/institute-setup/rooms-management/buildings/{id}` | Delete building |

### Room Type Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/institute-setup/rooms-management/room-types` | List all room types |
| GET | `/api/institute-setup/rooms-management/room-types/{id}` | Get single room type |
| POST | `/api/institute-setup/rooms-management/room-types` | Create room type |
| PUT | `/api/institute-setup/rooms-management/room-types/{id}` | Update room type |
| DELETE | `/api/institute-setup/rooms-management/room-types/{id}` | Delete room type |

### Room Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/institute-setup/rooms-management/rooms` | List all rooms |
| GET | `/api/institute-setup/rooms-management/rooms/{id}` | Get single room |
| POST | `/api/institute-setup/rooms-management/rooms` | Create room |
| PUT | `/api/institute-setup/rooms-management/rooms/{id}` | Update room |
| DELETE | `/api/institute-setup/rooms-management/rooms/{id}` | Delete room |

---

## 📝 Data Models

### Building

```typescript
Building {
  buildingName: string     // Required, max 128 characters
  totalFloors: int         // Required, minimum 1
  description: string      // Optional, max 500 characters
  
  // Auto-tracked
  id: Guid
  creationTime: DateTime
  creatorId: Guid?
  lastModificationTime: DateTime?
  lastModifierId: Guid?
}
```

### Room Type

```typescript
RoomType {
  roomTypeName: string     // Required, max 100 characters
  shortName: string        // Required, max 10 characters
  
  // Auto-tracked
  id: Guid
  creationTime: DateTime
  creatorId: Guid?
  lastModificationTime: DateTime?
  lastModifierId: Guid?
}
```

### Room

```typescript
Room {
  // Basic Details
  roomName: string         // Required, max 50 characters
  capacity: int            // Required, minimum 1
  buildingId: Guid         // Required, FK to Building
  floor: int               // Required, minimum 1
  roomTypeId: Guid         // Required, FK to RoomType
  
  // Benches Details
  totalBenches: int        // Required, minimum 0
  benchCapacity: int       // Minimum 0
  classBenchesRows: int    // Minimum 0
  classBenchesColumns: int // Minimum 0
  
  // Features
  features: string         // Optional, max 500 characters (comma-separated)
  
  // Auto-tracked
  id: Guid
  creationTime: DateTime
  creatorId: Guid?
  lastModificationTime: DateTime?
  lastModifierId: Guid?
}
```

---

## 🔐 Authentication

All endpoints require authentication. Include these headers:

```http
Authorization: Bearer {access_token}
__tenant: {tenant_id}
```

**Login Request:**
```http
POST /connect/token
Headers:
  __tenant: 3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e
  Content-Type: application/x-www-form-urlencoded

Body:
  grant_type=password&
  username=admin&
  password=Temp@123&
  client_id=ShikshaMitra_App&
  scope=offline_access ShikshaMitra
```

---

## 📖 API Examples

### Building Management

#### 1. Create Building

#### 1. Create Building

**Request:**
```http
POST /api/institute-setup/rooms-management/buildings
Authorization: Bearer {token}
__tenant: {tenant_id}
Content-Type: application/json

{
  "buildingName": "Main Building",
  "totalFloors": 3,
  "description": "This is the main building"
}
```

**Response (200 OK):**
```json
{
  "id": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
  "buildingName": "Main Building",
  "totalFloors": 3,
  "description": "This is the main building",
  "creationTime": "2024-01-15T10:30:00Z"
}
```

#### 2. Get All Buildings

**Request:**
```http
GET /api/institute-setup/rooms-management/buildings?SkipCount=0&MaxResultCount=10
Authorization: Bearer {token}
__tenant: {tenant_id}
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
      "buildingName": "Building 1",
      "totalFloors": 1,
      "description": "In this building, there is a total of 1 floor.",
      "creationTime": "2024-01-15T10:30:00Z",
      "creatorId": "39f4f2d2-c4c3-4b8a-8d5e-3c4a2b1f3e4d"
    },
    {
      "id": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
      "buildingName": "Building 2",
      "totalFloors": 2,
      "description": "In this building, there are a total of 2 floors.",
      "creationTime": "2024-01-15T11:00:00Z",
      "creatorId": "39f4f2d2-c4c3-4b8a-8d5e-3c4a2b1f3e4d"
    }
  ],
  "totalCount": 2
}
```

### 2. Get Building by ID

**Request:**
```http
GET /api/institute-setup/rooms-management/buildings/f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454
Authorization: Bearer {token}
__tenant: {tenant_id}
```

**Response (200 OK):**
```json
{
  "id": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
  "buildingName": "Building 1",
  "totalFloors": 1,
  "description": "In this building, there is a total of 1 floor.",
  "creationTime": "2024-01-15T10:30:00Z",
  "creatorId": "39f4f2d2-c4c3-4b8a-8d5e-3c4a2b1f3e4d"
}
```

### 3. Create Building

**Request:**
```http
POST /api/institute-setup/rooms-management/buildings
Authorization: Bearer {token}
__tenant: {tenant_id}
Content-Type: application/json

{
  "buildingName": "Main Building",
  "totalFloors": 3,
  "description": "This is the main building with 3 floors"
}
```

**Response (200 OK):**
```json
{
  "id": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
  "buildingName": "Main Building",
  "totalFloors": 3,
  "description": "This is the main building with 3 floors",
  "creationTime": "2024-01-15T10:30:00Z",
  "creatorId": "39f4f2d2-c4c3-4b8a-8d5e-3c4a2b1f3e4d"
}
```

### 4. Update Building

**Request:**
```http
PUT /api/institute-setup/rooms-management/buildings/f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454
Authorization: Bearer {token}
__tenant: {tenant_id}
Content-Type: application/json

{
  "buildingName": "Main Building (Updated)",
  "totalFloors": 4,
  "description": "Updated description with 4 floors"
}
```

**Response (200 OK):**
```json
{
  "id": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
  "buildingName": "Main Building (Updated)",
  "totalFloors": 4,
  "description": "Updated description with 4 floors",
  "creationTime": "2024-01-15T10:30:00Z",
  "creatorId": "39f4f2d2-c4c3-4b8a-8d5e-3c4a2b1f3e4d",
  "lastModificationTime": "2024-01-15T11:30:00Z",
  "lastModifierId": "39f4f2d2-c4c3-4b8a-8d5e-3c4a2b1f3e4d"
}
```

### 5. Delete Building

**Request:**
```http
DELETE /api/institute-setup/rooms-management/buildings/f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454
Authorization: Bearer {token}
__tenant: {tenant_id}
```

**Response:** `204 No Content`

### Room Type Management

#### 1. Create Room Type

**Request:**
```http
POST /api/institute-setup/rooms-management/room-types
Authorization: Bearer {token}
__tenant: {tenant_id}
Content-Type: application/json

{
  "roomTypeName": "Classroom",
  "shortName": "CR"
}
```

**Response (200 OK):**
```json
{
  "id": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
  "roomTypeName": "Classroom",
  "shortName": "CR",
  "creationTime": "2024-01-15T10:30:00Z"
}
```

#### 2. Get All Room Types

**Request:**
```http
GET /api/institute-setup/rooms-management/room-types
Authorization: Bearer {token}
__tenant: {tenant_id}
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
      "roomTypeName": "Classroom",
      "shortName": "CR",
      "creationTime": "2024-01-15T10:30:00Z"
    },
    {
      "id": "b2c3d4e5-f6g7-8h9i-0j1k-l2m3n4o5p6q7",
      "roomTypeName": "Laboratory",
      "shortName": "LAB",
      "creationTime": "2024-01-15T11:00:00Z"
    }
  ],
  "totalCount": 2
}
```

#### 3. Update Room Type

**Request:**
```http
PUT /api/institute-setup/rooms-management/room-types/a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6
Authorization: Bearer {token}
__tenant: {tenant_id}
Content-Type: application/json

{
  "roomTypeName": "Conference Room",
  "shortName": "CONF"
}
```

**Response (200 OK):**
```json
{
  "id": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
  "roomTypeName": "Conference Room",
  "shortName": "CONF",
  "creationTime": "2024-01-15T10:30:00Z",
  "lastModificationTime": "2024-01-15T12:00:00Z"
}
```

#### 4. Delete Room Type

**Request:**
```http
DELETE /api/institute-setup/rooms-management/room-types/a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6
Authorization: Bearer {token}
__tenant: {tenant_id}
```

**Response:** `204 No Content`

### Room Management

#### 1. Create Room

**Request:**
```http
POST /api/institute-setup/rooms-management/rooms
Authorization: Bearer {token}
__tenant: {tenant_id}
Content-Type: application/json

{
  "roomName": "101",
  "capacity": 50,
  "buildingId": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
  "floor": 1,
  "roomTypeId": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
  "totalBenches": 25,
  "benchCapacity": 2,
  "classBenchesRows": 5,
  "classBenchesColumns": 5,
  "features": "Projector, AC, Whiteboard"
}
```

**Response (200 OK):**
```json
{
  "id": "c1d2e3f4-g5h6-7i8j-9k0l-m1n2o3p4q5r6",
  "roomName": "101",
  "capacity": 50,
  "buildingId": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
  "buildingName": "Main Building",
  "floor": 1,
  "roomTypeId": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
  "roomTypeName": "Classroom",
  "totalBenches": 25,
  "benchCapacity": 2,
  "classBenchesRows": 5,
  "classBenchesColumns": 5,
  "features": "Projector, AC, Whiteboard",
  "creationTime": "2024-01-15T10:30:00Z"
}
```

#### 2. Get All Rooms

**Request:**
```http
GET /api/institute-setup/rooms-management/rooms
Authorization: Bearer {token}
__tenant: {tenant_id}
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "c1d2e3f4-g5h6-7i8j-9k0l-m1n2o3p4q5r6",
      "roomName": "101",
      "capacity": 50,
      "buildingId": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
      "buildingName": "Building A",
      "floor": 1,
      "roomTypeId": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
      "roomTypeName": "Lecture",
      "totalBenches": 25,
      "features": "Projector, AC, Whiteboard",
      "creationTime": "2024-01-15T10:30:00Z"
    },
    {
      "id": "d2e3f4g5-h6i7-8j9k-0l1m-n2o3p4q5r6s7",
      "roomName": "102",
      "capacity": 40,
      "buildingId": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
      "buildingName": "Building B",
      "floor": 1,
      "roomTypeId": "b2c3d4e5-f6g7-8h9i-0j1k-l2m3n4o5p6q7",
      "roomTypeName": "Lab",
      "totalBenches": 20,
      "features": "Computers, AC, Whiteboard",
      "creationTime": "2024-01-15T11:00:00Z"
    }
  ],
  "totalCount": 2
}
```

#### 3. Update Room

**Request:**
```http
PUT /api/institute-setup/rooms-management/rooms/c1d2e3f4-g5h6-7i8j-9k0l-m1n2o3p4q5r6
Authorization: Bearer {token}
__tenant: {tenant_id}
Content-Type: application/json

{
  "roomName": "101A",
  "capacity": 60,
  "buildingId": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
  "floor": 1,
  "roomTypeId": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
  "totalBenches": 30,
  "benchCapacity": 2,
  "classBenchesRows": 6,
  "classBenchesColumns": 5,
  "features": "Projector, AC, Whiteboard, Smart Board"
}
```

**Response (200 OK):**
```json
{
  "id": "c1d2e3f4-g5h6-7i8j-9k0l-m1n2o3p4q5r6",
  "roomName": "101A",
  "capacity": 60,
  "buildingId": "f8c3de3d-1fea-4d7c-a8b0-29f63c4c3454",
  "buildingName": "Main Building",
  "floor": 1,
  "roomTypeId": "a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6",
  "roomTypeName": "Classroom",
  "totalBenches": 30,
  "benchCapacity": 2,
  "classBenchesRows": 6,
  "classBenchesColumns": 5,
  "features": "Projector, AC, Whiteboard, Smart Board",
  "creationTime": "2024-01-15T10:30:00Z",
  "lastModificationTime": "2024-01-15T12:00:00Z"
}
```

#### 4. Delete Room

**Request:**
```http
DELETE /api/institute-setup/rooms-management/rooms/c1d2e3f4-g5h6-7i8j-9k0l-m1n2o3p4q5r6
Authorization: Bearer {token}
__tenant: {tenant_id}
```

**Response:** `204 No Content`

---

## ❌ Error Responses

### 400 Bad Request - Validation Error
```json
{
  "error": {
    "code": "400",
    "message": "Validation failed",
    "validationErrors": [
      {
        "message": "The BuildingName field is required.",
        "members": ["buildingName"]
      },
      {
        "message": "Total floors must be at least 1",
        "members": ["totalFloors"]
      }
    ]
  }
}
```

### 401 Unauthorized
```json
{
  "error": {
    "code": "401",
    "message": "Unauthorized"
  }
}
```

### 404 Not Found
```json
{
  "error": {
    "code": "404",
    "message": "Building not found"
  }
}
```

---

## 🏗️ Implementation Details

### File Structure

```
src/
├── ShikshaMitra.Domain.Shared/
│   └── TenantSettings/RoomsManagement/Buildings/
│       └── BuildingConsts.cs
├── ShikshaMitra.Domain/
│   └── TenantSettings/RoomsManagement/Buildings/
│       └── Building.cs
├── ShikshaMitra.Application.Contracts/
│   └── TenantSettings/RoomsManagement/Buildings/
│       ├── BuildingDto.cs
│       ├── CreateUpdateBuildingDto.cs
│       └── IBuildingAppService.cs
├── ShikshaMitra.Application/
│   └── TenantSettings/RoomsManagement/Buildings/
│       └── BuildingAppService.cs
├── ShikshaMitra.EntityFrameworkCore/
│   └── TenantSettings/RoomsManagement/Buildings/
│       └── BuildingConfiguration.cs
└── ShikshaMitra.HttpApi.Host/
    └── Controllers/TenantSettings/RoomsManagement/Buildings/
        └── BuildingController.cs
```

### Database Schema

**Table:** `App_Buildings`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| Id | uniqueidentifier | No | Primary Key |
| BuildingName | nvarchar(128) | No | Building name |
| TotalFloors | int | No | Number of floors |
| Description | nvarchar(500) | Yes | Optional description |
| CreationTime | datetime2 | No | Auto-tracked |
| CreatorId | uniqueidentifier | Yes | Auto-tracked |
| LastModificationTime | datetime2 | Yes | Auto-tracked |
| LastModifierId | uniqueidentifier | Yes | Auto-tracked |

---

## 🧪 Testing with Postman

### Setup
1. Import collection: `postman/ShikshaMitra.postman_collection.json`
2. Import environment: `postman/ShikshaMitra.postman_environment.json`
3. Login to get `access_token` (auto-saved)
4. Navigate to: **Institute Setup → Rooms Management → Buildings**

### Test Scenarios

#### Create 5 Buildings (Bulk)
Run requests in folder: **Buildings → Bulk Create**
- Creates Building 1 through Building 5
- Each with incremental floors (1-5)

#### Validation Tests
Run requests in folder: **Buildings → Validation Tests**
- Missing building name (expect 400)
- Invalid total floors (expect 400)
- Too long building name (expect 400)
- Too long description (expect 400)

#### Pagination Tests
- Get first 2 buildings: `?SkipCount=0&MaxResultCount=2`
- Get next 2 buildings: `?SkipCount=2&MaxResultCount=2`

#### Sorting Tests
- By name ascending: `?Sorting=buildingName asc`
- By name descending: `?Sorting=buildingName desc`
- By floors: `?Sorting=totalFloors desc`
- By creation time: `?Sorting=creationTime desc`

---

## 🔧 Troubleshooting

### Issue: Cannot create migration
**Solution:** Use DbMigrator instead
```bash
dotnet run --project src/ShikshaMitra.DbMigrator
```

### Issue: 401 Unauthorized
**Solution:** 
1. Verify you're logged in
2. Check `access_token` is valid and not expired
3. Ensure `__tenant` header is included

### Issue: 400 Bad Request
**Solution:** Verify request body meets validation rules:
- BuildingName is provided and ≤ 128 chars
- TotalFloors is ≥ 1
- Description is ≤ 500 chars (if provided)

### Issue: Build failed
**Solution:**
```bash
dotnet clean
dotnet build
```

---

## 📈 Features

| Feature | Status |
|---------|--------|
| Create Building | ✅ |
| Read Building (List) | ✅ |
| Read Building (Single) | ✅ |
| Update Building | ✅ |
| Delete Building | ✅ |
| Pagination | ✅ |
| Sorting | ✅ |
| Filtering | ❌ (Future) |
| Validation | ✅ |
| Authorization | ✅ |
| Audit Tracking | ✅ |
| Multi-tenancy | ✅ |
| Soft Delete | ❌ (Hard delete) |

---

## 🎯 Summary

**Status:** ✅ Ready for use (after migration)

**Created Files:**
- 8 new code files (Domain, Application, Infrastructure, Presentation)
- 2 modified files (DbContext, AutoMapper)
- 1 comprehensive documentation file

**Build Status:** ✅ Success

**Next Step:** Run database migration and start testing!

---

**Version:** 1.0.0  
**Last Updated:** 2024  
**Module:** Institute Setup → Rooms Management  
**License:** ABP Framework
