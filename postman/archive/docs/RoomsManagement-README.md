# Rooms Management API - Postman Collection

## Quick Setup

1. Import `RoomsManagement.postman_collection.json`
2. Set environment variables:
   - `base_url`: https://localhost:44365
   - `access_token`: (from login)
   - `tenant_id`: Your tenant ID

## Endpoints

### Buildings
- **POST** `/api/institute-setup/rooms-management/buildings` - Create
- **GET** `/api/institute-setup/rooms-management/buildings` - List
- **PUT** `/api/institute-setup/rooms-management/buildings/{id}` - Update
- **DELETE** `/api/institute-setup/rooms-management/buildings/{id}` - Delete

### Room Types
- **POST** `/api/institute-setup/rooms-management/room-types` - Create
- **GET** `/api/institute-setup/rooms-management/room-types` - List
- **PUT** `/api/institute-setup/rooms-management/room-types/{id}` - Update
- **DELETE** `/api/institute-setup/rooms-management/room-types/{id}` - Delete

### Rooms
- **POST** `/api/institute-setup/rooms-management/rooms` - Create
- **GET** `/api/institute-setup/rooms-management/rooms` - List
- **PUT** `/api/institute-setup/rooms-management/rooms/{id}` - Update
- **DELETE** `/api/institute-setup/rooms-management/rooms/{id}` - Delete

## Sample Data

### Create Building
```json
{
  "buildingName": "Main Building",
  "totalFloors": 3,
  "description": "Main building"
}
```

### Create Room Type
```json
{
  "roomTypeName": "Classroom",
  "shortName": "CR"
}
```

### Create Room
```json
{
  "roomName": "101",
  "capacity": 50,
  "buildingId": "{{building_id}}",
  "floor": 1,
  "roomTypeId": "{{room_type_id}}",
  "totalBenches": 25,
  "benchCapacity": 2,
  "classBenchesRows": 5,
  "classBenchesColumns": 5,
  "features": "Projector, AC, Whiteboard"
}
```

## Usage

1. Login first to get `access_token`
2. Create a building and save its ID
3. Create a room type and save its ID
4. Create rooms using the building and room type IDs
5. IDs are auto-saved in environment variables
6. Use saved IDs for update/delete operations

## Workflow

1. **Create Building** → Save `building_id`
2. **Create Room Type** → Save `room_type_id`
3. **Create Room** → Use `building_id` and `room_type_id`
4. **List Rooms** → View all rooms with building and room type names
