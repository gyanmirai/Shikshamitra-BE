---
title: HR
language_tabs:
  - shell: Shell
  - http: HTTP
  - javascript: JavaScript
  - ruby: Ruby
  - python: Python
  - php: PHP
  - java: Java
  - go: Go
toc_footers: []
includes: []
search: true
code_clipboard: true
highlight_theme: darkula
headingLevel: 2
generator: "@tarslib/widdershins v4.0.30"

---

# HR

API contract for managing employee payroll details.

Base URLs:

# Authentication

- HTTP Authentication, scheme: bearer

- HTTP Authentication, scheme: bearer

# Employee /Employee Salary Structure

<a id="opIdupdateEmployeeSalaryStructure"></a>

## PUT Update employee salary structure

PUT /api/payroll/employees/{employeeId}/salary-structure

> Body Parameters

```json
{
  "earningComponents": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true,
      "isTemp": false,
      "applicableMonth": "2019-08-24T14:15:22Z"
    }
  ],
  "deductionComponents": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true,
      "isTemp": false,
      "applicableMonth": "2019-08-24T14:15:22Z"
    }
  ]
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|object| yes |none|
|» earningComponents|body|[oneOf]| yes |none|
|»» id|body|string(uuid)¦null| no |none|
|»» componentId|body|string(uuid)¦null| no |none|
|»» name|body|string| yes |none|
|»» amount|body|number¦null| no |none|
|»» isFixed|body|boolean| no |none|
|»» isTemp|body|boolean| yes |none|
|»» applicableMonth|body|string(date-time)| yes |none|
|»» *anonymous*|body|object| no |none|
|»»» isTemp|body|boolean| no |none|
|»» *anonymous*|body|object| no |none|
|»»» isTemp|body|boolean| no |none|
|» deductionComponents|body|[oneOf]| yes |none|
|»» id|body|string(uuid)¦null| no |none|
|»» componentId|body|string(uuid)¦null| no |none|
|»» name|body|string| yes |none|
|»» amount|body|number¦null| no |none|
|»» isFixed|body|boolean| no |none|
|»» isTemp|body|boolean| yes |none|
|»» applicableMonth|body|string(date-time)| yes |none|
|»» *anonymous*|body|object| no |none|
|»» *anonymous*|body|object| no |none|

#### Enum

|Name|Value|
|---|---|
|»»» isTemp|false|
|»»» isTemp|true|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Salary structure updated successfully|None|

<a id="opIdgetEmployeeSalaryStructure"></a>

## GET Get employee salary structure

GET /api/payroll/employees/{employeeId}/salary-structure

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "earningComponents": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true,
      "isTemp": false,
      "applicableMonth": "2019-08-24T14:15:22Z"
    }
  ],
  "deductionComponents": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true,
      "isTemp": false,
      "applicableMonth": "2019-08-24T14:15:22Z"
    }
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Salary structure fetched successfully|Inline|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Salary structure not found|None|

### Responses Data Schema

HTTP Status Code **200**

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|» earningComponents|[oneOf]|false|none||none|
|»» id|string(uuid)¦null|false|none||none|
|»» componentId|string(uuid)¦null|false|none||none|
|»» name|string|true|none||none|
|»» amount|number¦null|false|none||none|
|»» isFixed|boolean|false|none||none|
|»» isTemp|boolean|true|none||none|
|»» applicableMonth|string(date-time)|true|none||none|

*oneOf*

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|»» *anonymous*|object|false|none||none|
|»»» isTemp|boolean|false|none||none|

*xor*

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|»» *anonymous*|object|false|none||none|
|»»» isTemp|boolean|false|none||none|

*continued*

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|» deductionComponents|[oneOf]|false|none||none|
|»» id|string(uuid)¦null|false|none||none|
|»» componentId|string(uuid)¦null|false|none||none|
|»» name|string|true|none||none|
|»» amount|number¦null|false|none||none|
|»» isFixed|boolean|false|none||none|
|»» isTemp|boolean|true|none||none|
|»» applicableMonth|string(date-time)|true|none||none|

*oneOf*

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|»» *anonymous*|object|false|none||none|

*xor*

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|»» *anonymous*|object|false|none||none|

#### Enum

|Name|Value|
|---|---|
|isTemp|false|
|isTemp|true|

# Data Schema

<h2 id="tocS_EmployeeSalaryComponent">EmployeeSalaryComponent</h2>

<a id="schemaemployeesalarycomponent"></a>
<a id="schema_EmployeeSalaryComponent"></a>
<a id="tocSemployeesalarycomponent"></a>
<a id="tocsemployeesalarycomponent"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
  "name": "string",
  "amount": 0,
  "isFixed": true,
  "isTemp": false,
  "applicableMonth": "2019-08-24T14:15:22Z"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)¦null|false|none||none|
|componentId|string(uuid)¦null|false|none||none|
|name|string|true|none||none|
|amount|number¦null|false|none||none|
|isFixed|boolean|false|none||none|
|isTemp|boolean|true|none||none|
|applicableMonth|string(date-time)|true|none||none|

oneOf

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|object|false|none||none|
|» isTemp|boolean|false|none||none|

xor

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|object|false|none||none|
|» isTemp|boolean|false|none||none|

#### Enum

|Name|Value|
|---|---|
|isTemp|false|
|isTemp|true|

<h2 id="tocS_ErrorResponse">ErrorResponse</h2>

<a id="schemaerrorresponse"></a>
<a id="schema_ErrorResponse"></a>
<a id="tocSerrorresponse"></a>
<a id="tocserrorresponse"></a>

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields failed validation",
    "fields": {
      "mediumId": "Must be a valid UUID"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object¦null|false|none||Field-specific validation errors|
|»» **additionalProperties**|string|false|none||none|

#### Enum

|Name|Value|
|---|---|
|code|VALIDATION_ERROR|
|code|INVALID_REQUEST|
|code|UNAUTHORIZED|
|code|FORBIDDEN|
|code|RESOURCE_NOT_FOUND|
|code|DUPLICATE_RESOURCE|
|code|INVALID_STATE_TRANSITION|
|code|RATE_LIMIT_EXCEEDED|
|code|INTERNAL_SERVER_ERROR|

<h2 id="tocS_StandardError">StandardError</h2>

<a id="schemastandarderror"></a>
<a id="schema_StandardError"></a>
<a id="tocSstandarderror"></a>
<a id="tocsstandarderror"></a>

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields failed validation",
    "fields": {
      "boardId": "boardId must be a valid UUID v4",
      "studentId": "No student found for the provided divisionId"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||none|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error description|
|» fields|object¦null|false|none||Field-level validation errors (key = field name, value = reason)|
|»» **additionalProperties**|string|false|none||none|

<h2 id="tocS_ErrorResponse1">ErrorResponse1</h2>

<a id="schemaerrorresponse1"></a>
<a id="schema_ErrorResponse1"></a>
<a id="tocSerrorresponse1"></a>
<a id="tocserrorresponse1"></a>

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields failed validation",
    "fields": {
      "accessionNo": "Accession number is required"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object¦null|false|none||Field-specific validation errors|
|»» **additionalProperties**|string|false|none||none|

#### Enum

|Name|Value|
|---|---|
|code|VALIDATION_ERROR|
|code|INVALID_REQUEST|
|code|UNAUTHORIZED|
|code|FORBIDDEN|
|code|RESOURCE_NOT_FOUND|
|code|DUPLICATE_RESOURCE|
|code|INVALID_STATE_TRANSITION|
|code|RATE_LIMIT_EXCEEDED|
|code|INTERNAL_SERVER_ERROR|

<h2 id="tocS_ErrorResponse2">ErrorResponse2</h2>

<a id="schemaerrorresponse2"></a>
<a id="schema_ErrorResponse2"></a>
<a id="tocSerrorresponse2"></a>
<a id="tocserrorresponse2"></a>

```json
{
  "success": false,
  "error": {
    "code": "INVALID_REQUEST",
    "message": "Request validation failed",
    "fields": {
      "educationMediumStageId": "At least one education medium stage ID is required",
      "members": "At least one member must be selected"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||Always false for error responses|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object¦null|false|none||Field-specific validation errors|
|»» **additionalProperties**|string|false|none||none|

<h2 id="tocS_ErrorResponse3">ErrorResponse3</h2>

<a id="schemaerrorresponse3"></a>
<a id="schema_ErrorResponse3"></a>
<a id="tocSerrorresponse3"></a>
<a id="tocserrorresponse3"></a>

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Request validation failed",
    "fields": {
      "obtainedMarks": "Obtained marks cannot exceed total marks",
      "status": "Status must be a valid integer"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||Always false for error responses|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object¦null|false|none||Field-specific validation errors|
|»» **additionalProperties**|string|false|none||none|

#### Enum

|Name|Value|
|---|---|
|code|INVALID_REQUEST|
|code|VALIDATION_ERROR|
|code|UNAUTHORIZED|
|code|FORBIDDEN|
|code|RESOURCE_NOT_FOUND|
|code|DUPLICATE_RESOURCE|
|code|INVALID_STATE_TRANSITION|
|code|RATE_LIMIT_EXCEEDED|
|code|INTERNAL_SERVER_ERROR|

<h2 id="tocS_ErrorResponse4">ErrorResponse4</h2>

<a id="schemaerrorresponse4"></a>
<a id="schema_ErrorResponse4"></a>
<a id="tocSerrorresponse4"></a>
<a id="tocserrorresponse4"></a>

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid input data provided",
    "fields": {
      "amount": "Amount must be greater than 0",
      "dueDate": "Due date must be in the future"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||Always false for error responses|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object|false|none||Field-specific error messages (for validation errors)|
|»» **additionalProperties**|string|false|none||none|

<h2 id="tocS_ErrorResponse7">ErrorResponse7</h2>

<a id="schemaerrorresponse7"></a>
<a id="schema_ErrorResponse7"></a>
<a id="tocSerrorresponse7"></a>
<a id="tocserrorresponse7"></a>

```json
{
  "success": true,
  "error": {
    "code": "string",
    "message": "string"
  },
  "meta": {
    "requestId": "d385ab22-0f51-4b97-9ecd-b8ff3fd4fcb6",
    "timestamp": "2019-08-24T14:15:22Z"
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||none|
|error|object|true|none||none|
|» code|string|false|none||none|
|» message|string|false|none||none|
|meta|object|true|none||none|
|» requestId|string(uuid)|false|none||none|
|» timestamp|string(date-time)|false|none||none|

<h2 id="tocS_ErrorResponse6">ErrorResponse6</h2>

<a id="schemaerrorresponse6"></a>
<a id="schema_ErrorResponse6"></a>
<a id="tocSerrorresponse6"></a>
<a id="tocserrorresponse6"></a>

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Request validation failed",
    "fields": {
      "standardId": "Must be a valid UUID",
      "progress": "Must be between 0 and 100"
    }
  },
  "meta": {
    "requestId": "550e8400-e29b-41d4-a716-446655440999",
    "timestamp": "2026-01-28T10:30:00Z"
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||Always false for error responses|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object¦null|false|none||Field-specific validation errors|
|»» **additionalProperties**|string|false|none||none|
|meta|[ResponseMetadata7](#schemaresponsemetadata7)|true|none||none|

#### Enum

|Name|Value|
|---|---|
|code|INVALID_REQUEST|
|code|VALIDATION_ERROR|
|code|UNAUTHORIZED|
|code|FORBIDDEN|
|code|RESOURCE_NOT_FOUND|
|code|DUPLICATE_RESOURCE|
|code|INVALID_STATE_TRANSITION|
|code|RATE_LIMIT_EXCEEDED|
|code|INTERNAL_SERVER_ERROR|

<h2 id="tocS_ResponseMetadata7">ResponseMetadata7</h2>

<a id="schemaresponsemetadata7"></a>
<a id="schema_ResponseMetadata7"></a>
<a id="tocSresponsemetadata7"></a>
<a id="tocsresponsemetadata7"></a>

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440999",
  "timestamp": "2026-01-28T10:30:00Z"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|requestId|string(uuid)|true|none||Unique request identifier for tracing|
|timestamp|string(date-time)|true|none||Server timestamp of response|

<h2 id="tocS_ErrorResponse5">ErrorResponse5</h2>

<a id="schemaerrorresponse5"></a>
<a id="schema_ErrorResponse5"></a>
<a id="tocSerrorresponse5"></a>
<a id="tocserrorresponse5"></a>

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Request validation failed",
    "fields": {
      "standardId": "Must be a valid UUID",
      "progress": "Must be between 0 and 100"
    }
  },
  "meta": {
    "requestId": "550e8400-e29b-41d4-a716-446655440999",
    "timestamp": "2026-01-28T10:30:00Z"
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||Always false for error responses|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object¦null|false|none||Field-specific validation errors|
|»» **additionalProperties**|string|false|none||none|
|meta|[ResponseMetadata](#schemaresponsemetadata)|true|none||none|

#### Enum

|Name|Value|
|---|---|
|code|INVALID_REQUEST|
|code|VALIDATION_ERROR|
|code|UNAUTHORIZED|
|code|FORBIDDEN|
|code|RESOURCE_NOT_FOUND|
|code|DUPLICATE_RESOURCE|
|code|INVALID_STATE_TRANSITION|
|code|RATE_LIMIT_EXCEEDED|
|code|INTERNAL_SERVER_ERROR|

<h2 id="tocS_ResponseMetadata">ResponseMetadata</h2>

<a id="schemaresponsemetadata"></a>
<a id="schema_ResponseMetadata"></a>
<a id="tocSresponsemetadata"></a>
<a id="tocsresponsemetadata"></a>

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440999",
  "timestamp": "2026-01-28T10:30:00Z"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|requestId|string(uuid)|true|none||Unique request identifier for tracing|
|timestamp|string(date-time)|true|none||Server timestamp of response|

<h2 id="tocS_ErrorResponse8">ErrorResponse8</h2>

<a id="schemaerrorresponse8"></a>
<a id="schema_ErrorResponse8"></a>
<a id="tocSerrorresponse8"></a>
<a id="tocserrorresponse8"></a>

```json
{
  "error": {
    "code": "VALIDATION_FAILED",
    "message": "Business validation failed",
    "fields": {
      "academicYearId": "Must be a valid UUID",
      "selectedFeeHeads[0].feeHeadId": "Mandatory fee head cannot be deselected"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object¦null|false|none||Field-specific error details|
|»» **additionalProperties**|string|false|none||none|

<h2 id="tocS_ErrorResponse86">ErrorResponse86</h2>

<a id="schemaerrorresponse86"></a>
<a id="schema_ErrorResponse86"></a>
<a id="tocSerrorresponse86"></a>
<a id="tocserrorresponse86"></a>

```json
{
  "error": {
    "code": "VALIDATION_FAILED",
    "message": "Business validation failed",
    "fields": {
      "academicYearId": "Must be a valid UUID",
      "selectedFeeHeads[0].feeHeadId": "Mandatory fee head cannot be deselected"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object¦null|false|none||Field-specific error details|
|»» **additionalProperties**|string|false|none||none|

<h2 id="tocS_StandardError7">StandardError7</h2>

<a id="schemastandarderror7"></a>
<a id="schema_StandardError7"></a>
<a id="tocSstandarderror7"></a>
<a id="tocsstandarderror7"></a>

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields failed validation",
    "fields": {
      "boardId": "boardId must be a valid UUID v4",
      "studentId": "No student found for the provided divisionId"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||none|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error description|
|» fields|object¦null|false|none||Field-level validation errors (key = field name, value = reason)|
|»» **additionalProperties**|string|false|none||none|

<h2 id="tocS_ErrorResponse48">ErrorResponse48</h2>

<a id="schemaerrorresponse48"></a>
<a id="schema_ErrorResponse48"></a>
<a id="tocSerrorresponse48"></a>
<a id="tocserrorresponse48"></a>

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid input data provided",
    "fields": {
      "amount": "Amount must be greater than 0",
      "dueDate": "Due date must be in the future"
    }
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|true|none||Always false for error responses|
|error|object|true|none||none|
|» code|string|true|none||Machine-readable error code|
|» message|string|true|none||Human-readable error message|
|» fields|object|false|none||Field-specific error messages (for validation errors)|
|»» **additionalProperties**|string|false|none||none|

<h2 id="tocS_UpdateEmployeeResponse">UpdateEmployeeResponse</h2>

<a id="schemaupdateemployeeresponse"></a>
<a id="schema_UpdateEmployeeResponse"></a>
<a id="tocSupdateemployeeresponse"></a>
<a id="tocsupdateemployeeresponse"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||Updated employee guid|

