# Room Management API - Quick Reference

## 🎯 What Was Created

A complete **Room Management API** with foreign key relationships to Buildings and Room Types.

---

## 📊 Room Entity Structure

```
Room
├── Basic Details
│   ├── RoomName (string, required, max 50)
│   ├── Capacity (int, required, min 1)
│   ├── BuildingId (Guid, required, FK → Building)
│   ├── Floor (int, required, min 1)
│   └── RoomTypeId (Guid, required, FK → RoomType)
│
├── Benches Details
│   ├── TotalBenches (int, required, min 0)
│   ├── BenchCapacity (int, min 0)
│   ├── ClassBenchesRows (int, min 0)
│   └── ClassBenchesColumns (int, min 0)
│
└── Features
    └── Features (string, optional, max 500)
        Examples: "Projector, AC, Whiteboard"
```

---

## 🔗 Relationships

```
Building (1) ─────< (N) Room
RoomType (1) ─────< (N) Room
```

A Room belongs to one Building and one RoomType.

---

## 📝 API Example

### Create Room
```json
POST /api/institute-setup/rooms-management/rooms

{
  "roomName": "101",
  "capacity": 50,
  "buildingId": "guid-of-building",
  "floor": 1,
  "roomTypeId": "guid-of-room-type",
  "totalBenches": 25,
  "benchCapacity": 2,
  "classBenchesRows": 5,
  "classBenchesColumns": 5,
  "features": "Projector, AC, Whiteboard"
}
```

### Response Includes Related Data
```json
{
  "id": "room-guid",
  "roomName": "101",
  "buildingName": "Main Building",    // From Building entity
  "roomTypeName": "Classroom",         // From RoomType entity
  "floor": 1,
  "capacity": 50,
  "totalBenches": 25,
  "features": "Projector, AC, Whiteboard",
  ...
}
```

---

## 🔧 Implementation Details

### Files Created (8)
1. `RoomConsts.cs` - Constants
2. `Room.cs` - Entity with FK relationships
3. `RoomDto.cs` - DTO with building/room type names
4. `CreateUpdateRoomDto.cs` - Input DTO with validation
5. `IRoomAppService.cs` - Service interface
6. `RoomAppService.cs` - Service with join logic
7. `RoomConfiguration.cs` - EF Core config with indexes
8. `RoomController.cs` - HTTP API controller

### Key Features
- ✅ Foreign key validation (Building & RoomType must exist)
- ✅ Automatic name resolution (BuildingName, RoomTypeName in response)
- ✅ Database indexes on BuildingId and RoomTypeId for performance
- ✅ Full CRUD operations
- ✅ Pagination & sorting support

---

## 🚀 Usage Workflow

1. **Create Building**
   ```json
   POST /buildings → Returns buildingId
   ```

2. **Create Room Type**
   ```json
   POST /room-types → Returns roomTypeId
   ```

3. **Create Room**
   ```json
   POST /rooms
   Use buildingId and roomTypeId from steps 1 & 2
   ```

4. **List Rooms**
   ```json
   GET /rooms
   Returns rooms with building names and room type names
   ```

---

## 📋 Validation Rules

| Field | Rules |
|-------|-------|
| roomName | Required, max 50 chars |
| capacity | Required, min 1 |
| buildingId | Required, must exist in Buildings table |
| floor | Required, min 1 |
| roomTypeId | Required, must exist in RoomTypes table |
| totalBenches | Required, min 0 |
| benchCapacity | Min 0 |
| classBenchesRows | Min 0 |
| classBenchesColumns | Min 0 |
| features | Optional, max 500 chars |

---

## 🎨 UI Mapping (from image)

| UI Field | API Property |
|----------|--------------|
| Room Name/No. | roomName |
| Capacity (Total) | capacity |
| Building | buildingId (dropdown) |
| Floor | floor |
| Room Type | roomTypeId (dropdown) |
| Total Benches | totalBenches |
| Bench Capacity | benchCapacity |
| Class Benches Rows | classBenchesRows |
| Class Benches Columns | classBenchesColumns |
| Features (checkboxes) | features (comma-separated) |

---

## ✅ Testing Checklist

- [ ] Create a building first
- [ ] Create a room type
- [ ] Create a room using valid buildingId and roomTypeId
- [ ] Verify room list shows building and room type names
- [ ] Update room details
- [ ] Delete room
- [ ] Test validation (invalid buildingId should fail)

---

**Status**: ✅ Ready for migration  
**Build**: ✅ Successful  
**Next**: Run database migration
