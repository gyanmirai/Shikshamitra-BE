# Class Management API - Postman Collection

## 📋 Overview

This Postman collection provides comprehensive API testing coverage for the **ShikshaMitra Class Management Module**, including:

- **Teacher Assignment** (Subject-wise teacher assignments to classes and divisions)
- **Class Teacher Assignment** (Class teacher assignments - one teacher per class-division)
- **Chapter Progress** (Track and update chapter completion progress)
- **Student Elective Assignment** (Assign elective subjects to students)
- **Class Teacher (Legacy)** (Legacy endpoints for class teacher management)

## 🚀 Quick Start

### 1. Import Collection & Environment

1. Open Postman
2. Click **Import** button
3. Import both files:
   - `ClassManagement.postman_collection.json`
   - `ShikshaMitra.postman_environment.json`
4. Select the **"ShikshaMitra Environment"** environment from the environment dropdown

### 2. Configure Environment Variables

Update these variables in your environment:

| Variable | Description | Example Value |
|----------|-------------|---------------|
| `base_url` | Local development URL | `https://localhost:44300` |
| `staging_url` | Staging server URL | `https://staging-gyanmirai-be.azurewebsites.net` |
| `tenant_id` | Tenant identifier | `11111111-1111-1111-1111-111111111111` |
| `username` | Login username | `teacher1` |
| `password` | Login password | `Temp@123` |
| `academic_medium_id` | Academic medium ID | (Auto-populated or use `medium_id`) |
| `class_id` | Class/Standard ID | (Auto-populated or use `standard_id`) |
| `division_id` | Division ID | (Auto-populated) |
| `subject_id` | Subject ID | (Auto-populated) |
| `teacher_id` | Teacher ID | (Auto-populated) |
| `organization_id` | Organization ID | (Auto-populated) |
| `elective_group_id` | Elective group ID | (Auto-populated) |
| `student_id` | Student ID | (Auto-populated) |
| `chapter_id` | Chapter ID | (Auto-populated) |

### 3. Authenticate

1. Navigate to **Authentication → Login**
2. Click **Send**
3. The `access_token` will be automatically saved to your environment

## 📖 API Endpoints

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/connect/token` | Login and get access token |

### Teacher Assignment

APIs for managing subject-wise teacher assignments to classes and divisions.

#### Get Teacher Assignments for Class

**GET** `/api/teachers/assignments`

Get all teacher assignments for a specific class. Returns assignments grouped by division and subject.

**Query Parameters:**
- `academicMediumId` (required) - Academic medium ID
- `classId` (required) - Class/Standard ID

**Response:** `TeacherAssignmentGridResponseDto`

#### Save Teacher Assignments for Class

**PUT** `/api/teachers/assignments`

Save or update teacher assignments for a class. Each assignment specifies division, subject, and assigned teachers.

**Request Body:**
```json
{
    "academicMediumId": "guid",
    "classId": "guid",
    "assignments": [
        {
            "divisionId": "guid",
            "subjectId": "guid",
            "teacherIds": ["guid1", "guid2"]
        }
    ]
}
```

**Response:** `ApiSuccessResponseDto`

#### Get Teachers

**GET** `/api/teachers`

Get list of all teachers. Optionally filter by search term.

**Query Parameters:**
- `search` (optional) - Search term to filter teachers by name

**Response:** `List<TeacherInfoDto>`

### Class Teacher Assignment

APIs for managing class teacher assignments (one teacher per class-division).

#### Get Class Teacher Assignments

**GET** `/api/class-teachers/assignments`

Get all class teacher assignments for an organization. Returns assignments grouped by class and division.

**Query Parameters:**
- `organizationId` (required) - Organization ID

**Response:** `List<ClassTeacherRowDto>`

**Permission Required:** `ClassManagement.ClassTeacherAssignment.View`

#### Save Class Teacher Assignments

**PUT** `/api/class-teachers/assignments`

Save or update class teacher assignments. Each assignment specifies one teacher per class-division combination.

**Request Body:**
```json
{
    "organizationId": "guid",
    "assignments": [
        {
            "classId": "guid",
            "divisionId": "guid",
            "teacherId": "guid"
        }
    ]
}
```

**Response:** `ApiSuccessResponseDto`

**Permission Required:** `ClassManagement.ClassTeacherAssignment.Add`

### Chapter Progress

APIs for tracking and updating chapter completion progress.

#### Get Subjects for Class Division

**GET** `/api/subjects`

Get all subjects for a specific class and division. Returns subjects with their chapter progress information.

**Query Parameters:**
- `academicMediumId` (required) - Academic medium ID
- `classId` (required) - Class/Standard ID
- `divisionId` (required) - Division ID

**Response:** `List<ClassDivisionSubjectDto>`

#### Get Chapter Progress List

**GET** `/api/chapters/progress`

Get paginated list of chapters with their progress for a specific class, division, and subject.

**Query Parameters:**
- `academicMediumId` (required) - Academic medium ID
- `classId` (required) - Class/Standard ID
- `divisionId` (required) - Division ID
- `subjectId` (required) - Subject ID
- `search` (optional) - Search term to filter chapters

**Response:** `PaginatedChapterProgressResponseDto`

#### Update Chapter Progress

**PUT** `/api/chapters/{chapterId}/progress`

Update the progress percentage (0-100) for a specific chapter in a division.

**Path Parameters:**
- `chapterId` (required) - Chapter ID

**Request Body:**
```json
{
    "divisionId": "guid",
    "progress": 75,
    "description": "Chapter completed with exercises"
}
```

**Validation:**
- `progress` must be between 0 and 100
- `description` cannot exceed 1000 characters

**Response:** `ApiSuccessResponseDto`

### Student Elective Assignment

APIs for managing elective subject assignments to students.

#### Get Student Elective Assignments

**GET** `/api/electives/assignments`

Get paginated list of students with their elective subject assignments for a specific class, division, and elective group.

**Query Parameters:**
- `academicMediumId` (required) - Academic medium ID
- `classId` (required) - Class/Standard ID
- `divisionId` (required) - Division ID
- `electiveGroupId` (required) - Elective group ID
- `search` (optional) - Search term to filter students by name or roll number

**Response:** `PaginatedStudentElectiveResponseDto`

**Permission Required:** `ClassManagement.StudentElectiveAssignment.View`

#### Save Student Elective Assignments

**PUT** `/api/electives/assignments`

Save or update elective subject assignments for students. Each assignment specifies a student and their selected elective subject.

**Request Body:**
```json
{
    "academicMediumId": "guid",
    "electiveGroupId": "guid",
    "assignments": [
        {
            "studentId": "guid",
            "selectedSubjectId": "guid"
        }
    ]
}
```

**Response:** `ApiSuccessResponseDto`

**Permission Required:** `ClassManagement.StudentElectiveAssignment.Add`

### Class Teacher (Legacy)

Legacy APIs for class teacher management. These endpoints use `EducationMediumId` from headers instead of query parameters.

#### Assign or Update Class Teachers

**PUT** `/api/class-teacher/assign`

Assign or update class teachers. Requires `EducationMediumId` header. Accepts array of assignments.

**Headers:**
- `EducationMediumId` (required) - Academic medium ID

**Request Body:**
```json
[
    {
        "teacherId": "guid",
        "classId": "guid",
        "divisionId": "guid"
    }
]
```

**Response:** Success message

#### Get All Class Teachers

**GET** `/api/class-teacher/all`

Get all class teacher assignments. Requires `EducationMediumId` header.

**Headers:**
- `EducationMediumId` (required) - Academic medium ID

**Response:** `List<ClassTeacherDto>`

## 🔐 Permissions

Some endpoints require specific permissions:

| Permission | Endpoint |
|------------|----------|
| `ClassManagement.ClassTeacherAssignment.View` | GET `/api/class-teachers/assignments` |
| `ClassManagement.ClassTeacherAssignment.Add` | PUT `/api/class-teachers/assignments` |
| `ClassManagement.StudentElectiveAssignment.View` | GET `/api/electives/assignments` |
| `ClassManagement.StudentElectiveAssignment.Add` | PUT `/api/electives/assignments` |

## 📝 Usage Examples

### Example 1: Assign Teachers to Subjects

1. **Get Teachers** - Get list of available teachers
2. **Get Teacher Assignments for Class** - View current assignments
3. **Save Teacher Assignments for Class** - Update assignments

```json
{
    "academicMediumId": "{{academic_medium_id}}",
    "classId": "{{class_id}}",
    "assignments": [
        {
            "divisionId": "{{division_id}}",
            "subjectId": "{{subject_id}}",
            "teacherIds": ["{{teacher_id}}"]
        }
    ]
}
```

### Example 2: Track Chapter Progress

1. **Get Subjects for Class Division** - Get available subjects
2. **Get Chapter Progress List** - View current progress
3. **Update Chapter Progress** - Update completion percentage

```json
{
    "divisionId": "{{division_id}}",
    "progress": 75,
    "description": "Chapter completed with exercises"
}
```

### Example 3: Assign Elective Subjects

1. **Get Student Elective Assignments** - View current assignments
2. **Save Student Elective Assignments** - Update assignments

```json
{
    "academicMediumId": "{{academic_medium_id}}",
    "electiveGroupId": "{{elective_group_id}}",
    "assignments": [
        {
            "studentId": "{{student_id}}",
            "selectedSubjectId": "{{subject_id}}"
        }
    ]
}
```

## 🔄 Workflow

### Typical Class Management Workflow

1. **Setup Phase:**
   - Ensure academic medium, class, and division are configured
   - Ensure subjects are assigned to classes
   - Ensure teachers are available in the system

2. **Teacher Assignment:**
   - Assign subject teachers to class divisions
   - Assign class teachers to class divisions

3. **Academic Progress:**
   - Track chapter completion progress
   - Update progress as chapters are completed

4. **Elective Management:**
   - Assign elective subjects to students
   - Update assignments as needed

## ⚠️ Important Notes

1. **Tenant Header:** All requests require the `__tenant` header with the tenant ID
2. **Authentication:** Bearer token authentication is required for all endpoints
3. **Legacy Endpoints:** The "Class Teacher (Legacy)" endpoints use headers instead of query parameters
4. **Variable Mapping:** `academic_medium_id` maps to `medium_id`, and `class_id` maps to `standard_id` in the environment
5. **Permissions:** Some endpoints require specific permissions - ensure your user has the necessary permissions

## 🐛 Troubleshooting

### Common Issues

1. **401 Unauthorized:**
   - Ensure you've logged in and the token is valid
   - Check that the `access_token` environment variable is set

2. **400 Bad Request:**
   - Verify all required query parameters are provided
   - Check that GUIDs are valid and not empty
   - Ensure request body matches the expected schema

3. **403 Forbidden:**
   - Check that your user has the required permissions
   - Verify the permission names match exactly

4. **404 Not Found:**
   - Verify that the IDs (class, division, subject, etc.) exist in the system
   - Check that the academic medium is correctly configured

## 📚 Related Documentation

- [API Reference](../docs/api/README.md)
- [UserScope Usage](../docs/platform/UserScope-Usage.md)
- [Multi-tenancy Guide](../docs/multi-tenancy/README.md)

## 🔗 Related Collections

- [HR Module Collection](./HR-Module.postman_collection.json)
- [Rooms Management Collection](./RoomsManagement.postman_collection.json)
- [UserScope API Collection](./UserScope-API.postman_collection.json)

