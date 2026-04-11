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

- HTTP Authentication, scheme: bearer

- HTTP Authentication, scheme: bearer

# Employee /Setting Documnet Config

## PUT Update Document Configuration

PUT /api/employees/documents/{id}

> Body Parameters

```json
{
  "title": "Student Certificate",
  "documentSize": 5,
  "documentTypes": [
    0,
    3,
    4
  ]
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|
|body|body|[DocumentRequest](#schemadocumentrequest)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|204|[No Content](https://tools.ietf.org/html/rfc7231#section-6.3.5)|Document updated successfully|None|

## DELETE Delete Document Configuration

DELETE /api/employees/documents/{id}

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|204|[No Content](https://tools.ietf.org/html/rfc7231#section-6.3.5)|Document deleted successfully|None|

## GET Get Document By ID

GET /api/employees/documents/{id}

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Student Certificate",
  "documentSize": 5,
  "documentTypes": [
    0
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Document fetched successfully|[DocumentItem](#schemadocumentitem)|

## GET Get All Document Configurations

GET /api/employees/documents

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|page|query|integer| no |none|
|pageSize|query|integer| no |none|
|academicYearId|query|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "Student Certificate",
      "documentSize": 5,
      "documentTypes": [
        0
      ]
    }
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Document list fetched successfully|Inline|

### Responses Data Schema

HTTP Status Code **200**

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|» items|[[DocumentItem](#schemadocumentitem)]|false|none||none|
|»» id|string(uuid)|false|none||none|
|»» title|string|false|none||none|
|»» documentSize|integer|false|none||none|
|»» documentTypes|[integer]|false|none||none|

## POST Create Document Configuration

POST /api/employees/documents

> Body Parameters

```json
{
  "title": "Student Certificate",
  "documentSize": 5,
  "documentTypes": [
    0,
    3,
    4
  ]
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|academicYearId|query|string(uuid)| yes |none|
|body|body|[DocumentRequest](#schemadocumentrequest)| yes |none|

> Response Examples

> 201 Response

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|201|[Created](https://tools.ietf.org/html/rfc7231#section-6.3.2)|Document created successfully|Inline|

### Responses Data Schema

HTTP Status Code **201**

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|» id|string(uuid)|false|none||none|

# Employee /Employee Form/Personal Info

## GET Get employee personal information

GET /api/employees/{employeeId}/get-personal-info

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "firstName": "string",
  "middleName": "string",
  "lastName": "string",
  "shortName": "string",
  "dob": "2019-08-24T14:15:22Z",
  "gender": 0,
  "religion": "string",
  "casteType": 0,
  "familyCaste": "string",
  "maritalStatus": 0,
  "bloodGroup": 1,
  "email": "user@example.com",
  "mobileNo": "string",
  "secondaryMobileNo": "string",
  "dateOfJoining": "2019-08-24T14:15:22Z",
  "isTeaching": true,
  "department": "Academic",
  "roleId": "7382d58e-652a-4905-b7c9-bcca1e0e5391",
  "shiftId": "4458e38b-30c8-4cab-b4da-8eefc165e6bf",
  "address1": "string",
  "address2": "string",
  "address3": "string",
  "country": {
    "name": "string",
    "isoCode": "string"
  },
  "state": {
    "name": "string",
    "isoCode": "string"
  },
  "city": "string",
  "pinCode": "string"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Personal information|[PersonalInfo](#schemapersonalinfo)|

## GET Get employee by ID

GET /api/employees/{employeeId}

> Body Parameters

```json
{}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|object| no |none|

> Response Examples

> 200 Response

```json
{
  "roleName": "string",
  "fullName": "string",
  "thumbnailPicture": "http://example.com",
  "employeeIdentifier": "string",
  "dob": "2019-08-24T14:15:22Z",
  "gender": 0,
  "doj": "2019-08-24T14:15:22Z",
  "email": "user@example.com",
  "mobileNo": "string",
  "isTeaching": true,
  "shift": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "name": "Morning Shift",
    "startTime": "08:30:00+05:30",
    "endTime": "14:30:00+05:30"
  },
  "status": 0,
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "roleColor": "string"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Employee details|[EmployeeDetails](#schemaemployeedetails)|

## POST Create employee personal information

POST /api/employees/personal-info

> Body Parameters

```yaml
profilePicture: ""
firstName: ""
middleName: ""
lastName: ""
shortName: ""
dob: ""
familyCast: ""
castType: 0
gender: 0
religion: 0
maritalStatus: 0
bloodGroup: 0
mobileNo: ""
secondaryMobileNo: ""
email: ""
department: ""
dateOfJoining: ""
isTeaching: ""
role: ""
addressLine1: ""
addressLine2: ""
addressLine3: ""
country: ""
state: ""
city: ""
pincode: ""
shift: ""

```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|academicYearId|query|string(uuid)| yes |none|
|body|body|object| yes |none|
|» profilePicture|body|string(binary)| yes |Profile photo file (jpg/png). Max size: 3MB (validated in backend/frontend).|
|» firstName|body|string| yes |none|
|» middleName|body|string¦null| no |none|
|» lastName|body|string| yes |none|
|» shortName|body|string| no |none|
|» dob|body|string(date-time)| yes |Date of birth (must be a valid past date)|
|» familyCast|body|string| yes |Employee family/sub-caste name|
|» castType|body|integer| yes |none|
|» gender|body|integer| yes |Gender value|
|» religion|body|integer| yes |none|
|» maritalStatus|body|integer| yes |none|
|» bloodGroup|body|integer| no |none|
|» mobileNo|body|string| yes |Primary Indian mobile number. Must be valid 10-digit Indian number (+91XXXXXXXXXX).|
|» secondaryMobileNo|body|string¦null| no |Secondary mobile number. Must be valid and different from primary (validated in backend).|
|» email|body|string(email)| yes |none|
|» department|body|string| no |none|
|» dateOfJoining|body|string(date-time)| yes |Date of joining (required & valid)|
|» isTeaching|body|boolean| yes |Employee group|
|» role|body|string(uuid)| yes |Employee role|
|» addressLine1|body|string| yes |Building / House No.|
|» addressLine2|body|string| yes |Area / Locality / Road|
|» addressLine3|body|string¦null| no |none|
|» country|body|object| yes |Employee's country of residence. Contains the full country name and its ISO code (ISO 3166-1 alpha-2/alpha-3) used for standardization, reporting, and integration purposes.|
|»» name|body|string| yes |none|
|»» isoCode|body|string| yes |none|
|» state|body|object| yes |Employee's state or province within the selected country. Includes the state name and its standardized ISO/state code for accurate address mapping and administrative usage.|
|»» name|body|string| yes |none|
|»» isoCode|body|string| yes |none|
|» city|body|object| yes |none|
|»» name|body|string| yes |none|
|» pincode|body|string| no |6-digit pincode|
|» shift|body|string| yes |Shift ID or name|

#### Enum

|Name|Value|
|---|---|
|» castType|0|
|» castType|1|
|» castType|2|
|» castType|3|
|» gender|0|
|» gender|1 |
|» religion|0|
|» religion|1|
|» religion|2|
|» religion|3|
|» religion|4|
|» religion|5|
|» religion|6|
|» religion|7|
|» religion|8|
|» maritalStatus|0|
|» maritalStatus|1|
|» maritalStatus|2|
|» maritalStatus|3|
|» bloodGroup|0|
|» bloodGroup|1|
|» bloodGroup|2|
|» bloodGroup|3|
|» bloodGroup|4|
|» bloodGroup|5|
|» bloodGroup|6|
|» bloodGroup|7|

> Response Examples

> 201 Response

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|201|[Created](https://tools.ietf.org/html/rfc7231#section-6.3.2)|Personal information created successfully|[EmployeeResponse](#schemaemployeeresponse)|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Validation error|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|

## PUT Update employee personal information

PUT /api/employees/{employeeId}/personal-info

> Body Parameters

```yaml
profilePicture: ""
firstName: ""
middleName: ""
lastName: ""
shortName: ""
dob: ""
familyCast: ""
castType: 0
gender: 0
religion: 0
maritalStatus: 0
bloodGroup: 0
mobileNo: ""
secondaryMobileNo: ""
email: ""
department: ""
dateOfJoining: ""
isTeaching: ""
role: ""
addressLine1: ""
addressLine2: ""
addressLine3: ""
country: ""
state: ""
city: ""
pincode: ""
shift: ""

```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|object| yes |none|
|» profilePicture|body|string(binary)| no |Profile photo file (jpg/png). Optional during update. Max size 3MB (validated in backend).|
|» firstName|body|string| yes |none|
|» middleName|body|string¦null| no |none|
|» lastName|body|string| yes |none|
|» shortName|body|string| no |none|
|» dob|body|string(date-time)| yes |Date of birth (must be valid past date)|
|» familyCast|body|string| yes |none|
|» castType|body|integer| yes |Category (General / OBC / SC / ST)|
|» gender|body|integer| yes |none|
|» religion|body|integer| yes |none|
|» maritalStatus|body|integer| yes |none|
|» bloodGroup|body|integer| no |none|
|» mobileNo|body|string| yes |Valid Indian mobile number|
|» secondaryMobileNo|body|string¦null| no |Must be different from primary (backend validation)|
|» email|body|string(email)| yes |none|
|» department|body|string| no |none|
|» dateOfJoining|body|string(date-time)| yes |none|
|» isTeaching|body|boolean| yes |none|
|» role|body|string(uuid)| yes |none|
|» addressLine1|body|string| yes |none|
|» addressLine2|body|string| yes |none|
|» addressLine3|body|string¦null| no |none|
|» country|body|object| yes |Employee's country of residence. Contains the full country name and its ISO code (ISO 3166-1 alpha-2/alpha-3) used for standardization, reporting, and integration purposes.|
|»» name|body|string| yes |none|
|» state|body|object| yes |Employee's state or province within the selected country. Includes the state name and its standardized ISO/state code for accurate address mapping and administrative usage.|
|»» name|body|string| yes |none|
|» city|body|object| yes |none|
|»» name|body|string| yes |none|
|» pincode|body|string| no |none|
|» shift|body|string(uuid)| yes |Shift identifier|

#### Enum

|Name|Value|
|---|---|
|» castType|0|
|» castType|1|
|» castType|2|
|» castType|3|
|» gender|0|
|» gender|1|
|» religion|0|
|» religion|1|
|» religion|2|
|» religion|3|
|» religion|4|
|» religion|5|
|» religion|6|
|» religion|7|
|» religion|8|
|» maritalStatus|1|
|» maritalStatus|2|
|» maritalStatus|3|
|» maritalStatus|4|
|» bloodGroup|0|
|» bloodGroup|1|
|» bloodGroup|2|
|» bloodGroup|3|
|» bloodGroup|4|
|» bloodGroup|5|
|» bloodGroup|6|
|» bloodGroup|7|

> Response Examples

> 200 Response

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Personal information updated successfully|[UpdateEmployeeResponse](#schemaupdateemployeeresponse)|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Validation error|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Employee not found|None|

# Employee /Employee Form/Employee Family Info

## PUT Create or update employee family information

PUT /api/employees/{employeeId}/family-info

Create or update family details (father, mother, spouse) for an employee

> Body Parameters

```json
{
  "fatherDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  },
  "motherDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  },
  "spouseDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  }
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |Employee unique identifier|
|body|body|[EmployeeFamilyInfoRequest](#schemaemployeefamilyinforequest)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|204|[No Content](https://tools.ietf.org/html/rfc7231#section-6.3.5)|Family information saved successfully|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Employee not found|None|
|500|[Internal Server Error](https://tools.ietf.org/html/rfc7231#section-6.6.1)|Internal server error|None|

## GET Get employee family information

GET /api/employees/{employeeId}/family-info

Fetch family details (father, mother, spouse) for an employee

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |Employee unique identifier|

> Response Examples

> 200 Response

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "fatherDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  },
  "motherDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  },
  "spouseDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  }
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Family information fetched successfully|[EmployeeFamilyInfoResponse](#schemaemployeefamilyinforesponse)|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Employee family information not found|None|
|500|[Internal Server Error](https://tools.ietf.org/html/rfc7231#section-6.6.1)|Internal server error|None|

# Employee /Employee Form/Additional Info

## PUT Update employee additional information

PUT /api/employees/{employeeId}/update-additional-info

> Body Parameters

```json
{
  "bplCardNo": "string",
  "personWithDisability": true,
  "secondLanguage": "string",
  "isLibraryMember": true,
  "isTransportMember": true
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|[AdditionalInfo](#schemaadditionalinfo)| no |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Updated|None|

## GET Get employee additional information

GET /api/employees/{employeeId}/get-additional-info

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "bplCardNo": "string",
  "personWithDisability": true,
  "secondLanguage": "string",
  "isLibraryMember": true,
  "isTransportMember": true
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Additional info|[AdditionalInfo](#schemaadditionalinfo)|

# Employee /Employee Form/Employee Documents

## PUT Update employee document

PUT /api/employees/{employeeId}/documents/{documentId}

> Body Parameters

```yaml
documentNameId: ""
file: ""
fileName: ""

```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|documentId|path|string(uuid)| yes |none|
|body|body|object| yes |none|
|» documentNameId|body|string(uuid)| yes |none|
|» file|body|string(binary)| yes |none|
|» fileName|body|string| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Updated|None|

## GET Fetch all employee documents

GET /api/employees/{employeeId}/documents

Fetches all documents uploaded for a specific employee

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |Unique identifier of the employee|
|page|query|integer| no |none|
|pageSize|query|integer| no |none|
|search|query|string| no |none|

> Response Examples

> 200 Response

```json
{
  "items": [
    {
      "documentId": "4704590c-004e-410d-adf7-acb7ca0a7052",
      "documentNameId": "1b75b846-0301-44b4-8643-69de3873e58a",
      "documentName": "Jpg",
      "fileName": "string",
      "contentType": "string",
      "size": 0,
      "uploadedAt": "2019-08-24T14:15:22Z",
      "updatedAt": "2019-08-24T14:15:22Z"
    }
  ],
  "currentPage": 0,
  "totalPages": 0,
  "pageSize": 0,
  "totalCount": 0
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Documents fetched successfully|Inline|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Employee not found|None|
|500|[Internal Server Error](https://tools.ietf.org/html/rfc7231#section-6.6.1)|Internal server error|None|

### Responses Data Schema

HTTP Status Code **200**

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|» items|[[EmployeeDocument](#schemaemployeedocument)]|false|none||none|
|»» documentId|string(uuid)|false|none||Unique document identifier|
|»» documentNameId|string(uuid)|false|none||Document type identifier|
|»» documentName|string|false|none||Document type name|
|»» fileName|string|false|none||File name|
|»» contentType|string|false|none||MIME type|
|»» size|number|false|none||File size in bytes|
|»» uploadedAt|string(date-time)|false|none||Upload timestamp|
|»» updatedAt|string(date-time)|false|none||Last updated timestamp|
|» currentPage|integer|false|none||none|
|» totalPages|integer|false|none||none|
|» pageSize|integer|false|none||none|
|» totalCount|integer|false|none||none|

#### Enum

|Name|Value|
|---|---|
|documentName|Jpg|
|documentName|Jpeg|
|documentName|Png|
|documentName|Docx|
|documentName|Pdf|
|documentName|Xlsx|

## POST Upload employee document

POST /api/employees/{employeeId}/documents

> Body Parameters

```yaml
documentNameId: ""
file: ""
fileName: ""

```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|object| yes |none|
|» documentNameId|body|string(uuid)| yes |none|
|» file|body|string(binary)| yes |none|
|» fileName|body|string| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|201|[Created](https://tools.ietf.org/html/rfc7231#section-6.3.2)|Uploaded|None|

# Employee /Employee Form/Work Experience

## PUT Update employee work experience

PUT /api/employees/{employeeId}/work-experience

> Body Parameters

```json
[
  {
    "organizationName": "string",
    "designation": "string",
    "department": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "startDate": "2019-08-24T14:15:22Z",
    "endDate": "2019-08-24T14:15:22Z"
  }
]
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|[WorkExperience](#schemaworkexperience)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Updated successfully|None|

## GET Get employee work experience

GET /api/employees/{employeeId}/get-work-experience

Returns paginated work experience details of an employee

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |Employee unique identifier|
|page|query|integer| no |Page number (0-based)|
|pageSize|query|integer| no |Number of records per page|

> Response Examples

> 200 Response

```json
{
  "totalCount": 25,
  "pageSize": 10,
  "totalPages": 3,
  "currentPage": 0,
  "items": [
    {
      "organizationName": "string",
      "designation": "string",
      "department": "string",
      "country": {
        "name": "string",
        "isoCode": "string"
      },
      "state": {
        "name": "string",
        "isoCode": "string"
      },
      "city": "string",
      "startDate": "2019-08-24T14:15:22Z",
      "endDate": "2019-08-24T14:15:22Z"
    }
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Paginated work experience list|[WorkExperiencePage](#schemaworkexperiencepage)|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Invalid request parameters|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Employee not found|None|
|500|[Internal Server Error](https://tools.ietf.org/html/rfc7231#section-6.6.1)|Internal server error|None|

# Employee /Employee Form/Employee Payroll Info

## GET Get employee payroll information

GET /api/employees/{employeeId}/get-payroll

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "bankDetails": {
    "bankName": "string",
    "branchName": "string",
    "accountHolderName": "string",
    "accountNumber": "stringstr",
    "ifscCode": "string"
  },
  "salaryDetails": {
    "pfRuleId": "9fb87f69-f3f6-4409-b5d8-0dad048fe1dd",
    "epfNumber": "string",
    "uanNumber": "string",
    "punchId": "string",
    "totalWorkingHourPerMonth": 0
  },
  "earningComponents": [
    {
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true
    }
  ],
  "deductionComponents": [
    {
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true
    }
  ],
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Payroll info|Inline|

### Responses Data Schema

HTTP Status Code **200**

|Name|Type|Required|Restrictions|Title|description|
|---|---|---|---|---|---|
|» bankDetails|[BankDetails](#schemabankdetails)|true|none||none|
|»» bankName|string¦null|false|none||none|
|»» branchName|string¦null|false|none||none|
|»» accountHolderName|string¦null|false|none||none|
|»» accountNumber|string¦null|false|none||none|
|»» ifscCode|string¦null|false|none||none|
|» salaryDetails|[SalaryDetails](#schemasalarydetails)|true|none||none|
|»» pfRuleId|string(uuid)¦null|false|none||none|
|»» epfNumber|string¦null|false|none||none|
|»» uanNumber|string¦null|false|none||none|
|»» punchId|string¦null|false|none||none|
|»» totalWorkingHourPerMonth|number¦null|false|none||none|
|» earningComponents|[[SalaryComponent](#schemasalarycomponent)]|false|none||none|
|»» componentId|string(uuid)¦null|false|none||none|
|»» name|string|true|none||none|
|»» amount|number¦null|false|none||none|
|»» isFixed|boolean|false|none||none|
|» deductionComponents|[[SalaryComponent](#schemasalarycomponent)]|false|none||none|
|»» componentId|string(uuid)¦null|false|none||none|
|»» name|string|true|none||none|
|»» amount|number¦null|false|none||none|
|»» isFixed|boolean|false|none||none|
|» id|string(uuid)|true|none||none|

## PUT Update employee payroll information

PUT /api/employees/{employeeId}/payroll

> Body Parameters

```json
{
  "bankDetails": {
    "bankName": "string",
    "branchName": "string",
    "accountHolderName": "string",
    "accountNumber": "stringstr",
    "ifscCode": "string"
  },
  "salaryDetails": {
    "pfRuleId": "9fb87f69-f3f6-4409-b5d8-0dad048fe1dd",
    "epfNumber": "string",
    "uanNumber": "string",
    "punchId": "string",
    "totalWorkingHourPerMonth": 0
  },
  "earningComponents": [
    {
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true
    }
  ],
  "deductionComponents": [
    {
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true
    }
  ]
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|[Payroll](#schemapayroll)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Payroll information updated successfully|None|

# Employee /Employee Form/Reporting Manager

## PUT Update reporting manager

PUT /api/employees/{employeeId}/reporting-manager

> Body Parameters

```json
{
  "reportingManagerId": "8742977d-492d-4910-b49c-dc37cb75c5c6"
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|object| yes |none|
|» reportingManagerId|body|string(uuid)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Updated|None|

## GET Get reporting manager

GET /api/employees/{employeeId}/reporting-manager

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "employeeId": "b0e78c8f-8c8b-4410-8293-9239e39d9e17",
  "reportingManager": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "employeeIdentifier": "string",
    "fullName": "string",
    "designation": "string"
  }
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Reporting manager details|[ReportingManagerResponse](#schemareportingmanagerresponse)|

## GET Get employees under reporting manager

GET /api/employees/reporting-manager/{managerId}/employees

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|managerId|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "manager": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "employeeIdentifier": "string",
    "fullName": "string",
    "designation": "string"
  },
  "employees": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "employeeIdentifier": "string",
      "fullName": "string",
      "designation": "string"
    }
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Employees under manager|[EmployeesUnderManager](#schemaemployeesundermanager)|

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

# Employee /Employee Status

<a id="opIdchangeEmployeeStatus"></a>

## PUT Change employee status

PUT /api/employees/{employeeId}/status

> Body Parameters

```json
{
  "employeeStatusType": 0,
  "remarks": "string"
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|employeeId|path|string(uuid)| yes |none|
|body|body|object| yes |none|
|» employeeStatusType|body|integer| yes |none|
|» remarks|body|string| no |none|

#### Enum

|Name|Value|
|---|---|
|» employeeStatusType|0|
|» employeeStatusType|1|
|» employeeStatusType|2|
|» employeeStatusType|3|

> Response Examples

> 200 Response

```json
{
  "employeeId": "string",
  "previousStatus": "ACTIVE",
  "currentStatus": "ACTIVE",
  "updatedAt": "string"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Employee status updated successfully|[EmployeeStatusResponse](#schemaemployeestatusresponse)|

# Employee /Employee Screen

## GET Get all employees

GET /api/all-employees

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|status|query|string| no |none|
|page|query|[EmployeeStatusString](#schemaemployeestatusstring)| no |none|
|pageSize|query|integer| no |none|
|isTeaching|query|integer| no |none|
|roleId|query|string(uuid)| no |none|
|academicYearId|query|string(uuid)| yes |none|
|search|query|string| no |none|

#### Enum

|Name|Value|
|---|---|
|page|ACTIVE|
|page|INACTIVE|
|page|LEFT|
|page|RETIRED|

> Response Examples

> 200 Response

```json
{
  "totalCount": 0,
  "currentPage": 0,
  "pageSize": 0,
  "totalPages": 0,
  "items": [
    {
      "roleName": "string",
      "fullName": "string",
      "thumbnailPicture": "http://example.com",
      "employeeIdentifier": "string",
      "dob": "2019-08-24T14:15:22Z",
      "gender": 0,
      "doj": "2019-08-24T14:15:22Z",
      "email": "user@example.com",
      "mobileNo": "string",
      "isTeaching": true,
      "shift": {
        "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
        "name": "Morning Shift",
        "startTime": "08:30:00+05:30",
        "endTime": "14:30:00+05:30"
      },
      "status": 0,
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "roleColor": "string"
    }
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Employees list|[EmployeeListResponse](#schemaemployeelistresponse)|

# Data Schema

<h2 id="tocS_EmployeeFamilyInfoRequest">EmployeeFamilyInfoRequest</h2>

<a id="schemaemployeefamilyinforequest"></a>
<a id="schema_EmployeeFamilyInfoRequest"></a>
<a id="tocSemployeefamilyinforequest"></a>
<a id="tocsemployeefamilyinforequest"></a>

```json
{
  "fatherDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  },
  "motherDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  },
  "spouseDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|fatherDetails|[FamilyMemberDetails](#schemafamilymemberdetails)|false|none||none|
|motherDetails|[FamilyMemberDetails](#schemafamilymemberdetails)|false|none||none|
|spouseDetails|[FamilyMemberDetails](#schemafamilymemberdetails)|false|none||none|

<h2 id="tocS_FamilyMemberDetails">FamilyMemberDetails</h2>

<a id="schemafamilymemberdetails"></a>
<a id="schema_FamilyMemberDetails"></a>
<a id="tocSfamilymemberdetails"></a>
<a id="tocsfamilymemberdetails"></a>

```json
{
  "name": "string",
  "occupation": "string",
  "mobileNo": "stringstri",
  "address1": "string",
  "address2": "string",
  "address3": "string",
  "country": {
    "name": "string",
    "isoCode": "string"
  },
  "state": {
    "name": "string",
    "isoCode": "string"
  },
  "city": "string",
  "pincode": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|name|string¦null|false|none||Name|
|occupation|string¦null|false|none||Occupation|
|mobileNo|string¦null|false|none||Mobile number (Indian format). Accepts +91 or 10-digit number.|
|address1|string¦null|false|none||Address line 1|
|address2|string¦null|false|none||Address line 2|
|address3|string¦null|false|none||Address line 3|
|country|[Country](#schemacountry)|false|none||none|
|state|[State](#schemastate)|false|none||none|
|city|string¦null|false|none||City|
|pincode|string¦null|false|none||PIN code (exactly 6 digits)|

<h2 id="tocS_Shift">Shift</h2>

<a id="schemashift"></a>
<a id="schema_Shift"></a>
<a id="tocSshift"></a>
<a id="tocsshift"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "name": "Morning Shift",
  "startTime": "08:30:00+05:30",
  "endTime": "14:30:00+05:30"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||none|
|name|string|true|none||none|
|startTime|string(date-time)|true|none||none|
|endTime|string(date-time)|true|none||none|

<h2 id="tocS_Country">Country</h2>

<a id="schemacountry"></a>
<a id="schema_Country"></a>
<a id="tocScountry"></a>
<a id="tocscountry"></a>

```json
{
  "name": "string",
  "isoCode": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|name|string|true|none||Country name|
|isoCode|string|false|none||ISO-2 country code|

<h2 id="tocS_State">State</h2>

<a id="schemastate"></a>
<a id="schema_State"></a>
<a id="tocSstate"></a>
<a id="tocsstate"></a>

```json
{
  "name": "string",
  "isoCode": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|name|string|true|none||State name|
|isoCode|string|false|none||ISO-2 state code|

<h2 id="tocS_EmployeeFamilyInfoResponse">EmployeeFamilyInfoResponse</h2>

<a id="schemaemployeefamilyinforesponse"></a>
<a id="schema_EmployeeFamilyInfoResponse"></a>
<a id="tocSemployeefamilyinforesponse"></a>
<a id="tocsemployeefamilyinforesponse"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "fatherDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  },
  "motherDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  },
  "spouseDetails": {
    "name": "string",
    "occupation": "string",
    "mobileNo": "stringstri",
    "address1": "string",
    "address2": "string",
    "address3": "string",
    "country": {
      "name": "string",
      "isoCode": "string"
    },
    "state": {
      "name": "string",
      "isoCode": "string"
    },
    "city": "string",
    "pincode": "string"
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||Family info record ID|
|fatherDetails|[FamilyMemberDetails](#schemafamilymemberdetails)|false|none||none|
|motherDetails|[FamilyMemberDetails](#schemafamilymemberdetails)|false|none||none|
|spouseDetails|[FamilyMemberDetails](#schemafamilymemberdetails)|false|none||none|

<h2 id="tocS_BankDetails">BankDetails</h2>

<a id="schemabankdetails"></a>
<a id="schema_BankDetails"></a>
<a id="tocSbankdetails"></a>
<a id="tocsbankdetails"></a>

```json
{
  "bankName": "string",
  "branchName": "string",
  "accountHolderName": "string",
  "accountNumber": "stringstr",
  "ifscCode": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|bankName|string¦null|false|none||none|
|branchName|string¦null|false|none||none|
|accountHolderName|string¦null|false|none||none|
|accountNumber|string¦null|false|none||none|
|ifscCode|string¦null|false|none||none|

<h2 id="tocS_SalaryDetails">SalaryDetails</h2>

<a id="schemasalarydetails"></a>
<a id="schema_SalaryDetails"></a>
<a id="tocSsalarydetails"></a>
<a id="tocssalarydetails"></a>

```json
{
  "pfRuleId": "9fb87f69-f3f6-4409-b5d8-0dad048fe1dd",
  "epfNumber": "string",
  "uanNumber": "string",
  "punchId": "string",
  "totalWorkingHourPerMonth": 0
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|pfRuleId|string(uuid)¦null|false|none||none|
|epfNumber|string¦null|false|none||none|
|uanNumber|string¦null|false|none||none|
|punchId|string¦null|false|none||none|
|totalWorkingHourPerMonth|number¦null|false|none||none|

<h2 id="tocS_EmployeeListResponse">EmployeeListResponse</h2>

<a id="schemaemployeelistresponse"></a>
<a id="schema_EmployeeListResponse"></a>
<a id="tocSemployeelistresponse"></a>
<a id="tocsemployeelistresponse"></a>

```json
{
  "totalCount": 0,
  "currentPage": 0,
  "pageSize": 0,
  "totalPages": 0,
  "items": [
    {
      "roleName": "string",
      "fullName": "string",
      "thumbnailPicture": "http://example.com",
      "employeeIdentifier": "string",
      "dob": "2019-08-24T14:15:22Z",
      "gender": 0,
      "doj": "2019-08-24T14:15:22Z",
      "email": "user@example.com",
      "mobileNo": "string",
      "isTeaching": true,
      "shift": {
        "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
        "name": "Morning Shift",
        "startTime": "08:30:00+05:30",
        "endTime": "14:30:00+05:30"
      },
      "status": 0,
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "roleColor": "string"
    }
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|totalCount|integer|false|none||none|
|currentPage|integer|false|none||none|
|pageSize|integer|false|none||none|
|totalPages|integer|false|none||none|
|items|[[EmployeeSummary](#schemaemployeesummary)]|false|none||none|

<h2 id="tocS_SalaryComponent">SalaryComponent</h2>

<a id="schemasalarycomponent"></a>
<a id="schema_SalaryComponent"></a>
<a id="tocSsalarycomponent"></a>
<a id="tocssalarycomponent"></a>

```json
{
  "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
  "name": "string",
  "amount": 0,
  "isFixed": true
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|componentId|string(uuid)¦null|false|none||none|
|name|string|true|none||none|
|amount|number¦null|false|none||none|
|isFixed|boolean|false|none||none|

<h2 id="tocS_EmployeeStatusTypeEnum">EmployeeStatusTypeEnum</h2>

<a id="schemaemployeestatustypeenum"></a>
<a id="schema_EmployeeStatusTypeEnum"></a>
<a id="tocSemployeestatustypeenum"></a>
<a id="tocsemployeestatustypeenum"></a>

```json
"ACTIVE"

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|string|false|none||none|

#### Enum

|Name|Value|
|---|---|
|*anonymous*|ACTIVE|
|*anonymous*|INACTIVE|
|*anonymous*|LEFT|
|*anonymous*|RETIRED|

<h2 id="tocS_EmployeeStatusResponse">EmployeeStatusResponse</h2>

<a id="schemaemployeestatusresponse"></a>
<a id="schema_EmployeeStatusResponse"></a>
<a id="tocSemployeestatusresponse"></a>
<a id="tocsemployeestatusresponse"></a>

```json
{
  "employeeId": "string",
  "previousStatus": "ACTIVE",
  "currentStatus": "ACTIVE",
  "updatedAt": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|employeeId|string|false|none||none|
|previousStatus|[EmployeeStatusTypeEnum](#schemaemployeestatustypeenum)|false|none||none|
|currentStatus|[EmployeeStatusTypeEnum](#schemaemployeestatustypeenum)|false|none||none|
|updatedAt|string|false|none||none|

<h2 id="tocS_EmployeeSummary">EmployeeSummary</h2>

<a id="schemaemployeesummary"></a>
<a id="schema_EmployeeSummary"></a>
<a id="tocSemployeesummary"></a>
<a id="tocsemployeesummary"></a>

```json
{
  "roleName": "string",
  "fullName": "string",
  "thumbnailPicture": "http://example.com",
  "employeeIdentifier": "string",
  "dob": "2019-08-24T14:15:22Z",
  "gender": 0,
  "doj": "2019-08-24T14:15:22Z",
  "email": "user@example.com",
  "mobileNo": "string",
  "isTeaching": true,
  "shift": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "name": "Morning Shift",
    "startTime": "08:30:00+05:30",
    "endTime": "14:30:00+05:30"
  },
  "status": 0,
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "roleColor": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|roleName|string|false|none||none|
|fullName|string|false|none||none|
|thumbnailPicture|string(uri)|false|none||none|
|employeeIdentifier|string|false|none||none|
|dob|string(date-time)|false|none||none|
|gender|integer|false|none||none|
|doj|string(date-time)|false|none||none|
|email|string(email)|false|none||none|
|mobileNo|string|false|none||none|
|isTeaching|boolean|false|none||none|
|shift|[Shift](#schemashift)|false|none||none|
|status|integer(uint)|true|none||none|
|id|string(uuid)|true|none||none|
|roleColor|string|true|none||none|

#### Enum

|Name|Value|
|---|---|
|gender|0|
|gender|1|
|status|0|
|status|1|
|status|2|
|status|3|

<h2 id="tocS_EmployeeDetails">EmployeeDetails</h2>

<a id="schemaemployeedetails"></a>
<a id="schema_EmployeeDetails"></a>
<a id="tocSemployeedetails"></a>
<a id="tocsemployeedetails"></a>

```json
{
  "roleName": "string",
  "fullName": "string",
  "thumbnailPicture": "http://example.com",
  "employeeIdentifier": "string",
  "dob": "2019-08-24T14:15:22Z",
  "gender": 0,
  "doj": "2019-08-24T14:15:22Z",
  "email": "user@example.com",
  "mobileNo": "string",
  "isTeaching": true,
  "shift": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "name": "Morning Shift",
    "startTime": "08:30:00+05:30",
    "endTime": "14:30:00+05:30"
  },
  "status": 0,
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "roleColor": "string"
}

```

### Attribute

*None*

<h2 id="tocS_PersonalInfo">PersonalInfo</h2>

<a id="schemapersonalinfo"></a>
<a id="schema_PersonalInfo"></a>
<a id="tocSpersonalinfo"></a>
<a id="tocspersonalinfo"></a>

```json
{
  "firstName": "string",
  "middleName": "string",
  "lastName": "string",
  "shortName": "string",
  "dob": "2019-08-24T14:15:22Z",
  "gender": 0,
  "religion": "string",
  "casteType": 0,
  "familyCaste": "string",
  "maritalStatus": 0,
  "bloodGroup": 1,
  "email": "user@example.com",
  "mobileNo": "string",
  "secondaryMobileNo": "string",
  "dateOfJoining": "2019-08-24T14:15:22Z",
  "isTeaching": true,
  "department": "Academic",
  "roleId": "7382d58e-652a-4905-b7c9-bcca1e0e5391",
  "shiftId": "4458e38b-30c8-4cab-b4da-8eefc165e6bf",
  "address1": "string",
  "address2": "string",
  "address3": "string",
  "country": {
    "name": "string",
    "isoCode": "string"
  },
  "state": {
    "name": "string",
    "isoCode": "string"
  },
  "city": "string",
  "pinCode": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|firstName|string|true|none||none|
|middleName|string|false|none||none|
|lastName|string|true|none||none|
|shortName|string|false|none||none|
|dob|string(date-time)|true|none||Date of birth (must be past date)|
|gender|integer|true|none||0 = Male, 1 = Female|
|religion|string|true|none||none|
|casteType|integer|true|none||0 = General, 1 = OBC, 2 = SC, 3 = ST|
|familyCaste|string|true|none||none|
|maritalStatus|integer|true|none||0 = Single, 1 = Married, 2 = Divorced, 3 = Widowed|
|bloodGroup|integer|false|none||0=A+,1=A-,2=B+,3=B-,5=O+,6=O-,7=AB+,8=AB-|
|email|string(email)|true|none||none|
|mobileNo|string|true|none||Primary mobile number (India)|
|secondaryMobileNo|string|true|none||Secondary mobile number (must be different from primary)|
|dateOfJoining|string(date-time)|true|none||Date of joining|
|isTeaching|boolean|true|none||Teaching or non-teaching staff|
|department|string|true|none||Employee department|
|roleId|string(uuid)|true|none||none|
|shiftId|string(uuid)|false|none||none|
|address1|string|true|none||none|
|address2|string|true|none||none|
|address3|string|false|none||none|
|country|[Country](#schemacountry)|true|none||none|
|state|[State](#schemastate)|true|none||none|
|city|string|true|none||none|
|pinCode|string|false|none||6-digit pincode|

#### Enum

|Name|Value|
|---|---|
|gender|0|
|gender|1|
|casteType|0|
|casteType|1|
|casteType|2|
|casteType|3|
|maritalStatus|0|
|maritalStatus|1|
|maritalStatus|2|
|maritalStatus|3|
|bloodGroup|1|
|bloodGroup|2|
|bloodGroup|3|
|bloodGroup|4|
|bloodGroup|5|
|bloodGroup|6|
|bloodGroup|7|
|bloodGroup|8|
|department|Academic|
|department|Administration|
|department|Examination|
|department|Finance|
|department|Student Affairs|

<h2 id="tocS_AdditionalInfo">AdditionalInfo</h2>

<a id="schemaadditionalinfo"></a>
<a id="schema_AdditionalInfo"></a>
<a id="tocSadditionalinfo"></a>
<a id="tocsadditionalinfo"></a>

```json
{
  "bplCardNo": "string",
  "personWithDisability": true,
  "secondLanguage": "string",
  "isLibraryMember": true,
  "isTransportMember": true
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|bplCardNo|string|false|none||none|
|personWithDisability|boolean|false|none||none|
|secondLanguage|string|false|none||none|
|isLibraryMember|boolean|false|none||none|
|isTransportMember|boolean|false|none||none|

<h2 id="tocS_WorkExperience">WorkExperience</h2>

<a id="schemaworkexperience"></a>
<a id="schema_WorkExperience"></a>
<a id="tocSworkexperience"></a>
<a id="tocsworkexperience"></a>

```json
{
  "organizationName": "string",
  "designation": "string",
  "department": "string",
  "country": {
    "name": "string",
    "isoCode": "string"
  },
  "state": {
    "name": "string",
    "isoCode": "string"
  },
  "city": "string",
  "startDate": "2019-08-24T14:15:22Z",
  "endDate": "2019-08-24T14:15:22Z"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|organizationName|string|true|none||Organization name|
|designation|string|false|none||Designation (optional)|
|department|string|false|none||Department name|
|country|[Country](#schemacountry)|false|none||none|
|state|[State](#schemastate)|false|none||none|
|city|string|false|none||City name|
|startDate|string(date-time)|true|none||Start date (required)|
|endDate|string(date-time)|true|none||End date (required). Must be greater than startDate.|

<h2 id="tocS_Payroll">Payroll</h2>

<a id="schemapayroll"></a>
<a id="schema_Payroll"></a>
<a id="tocSpayroll"></a>
<a id="tocspayroll"></a>

```json
{
  "bankDetails": {
    "bankName": "string",
    "branchName": "string",
    "accountHolderName": "string",
    "accountNumber": "stringstr",
    "ifscCode": "string"
  },
  "salaryDetails": {
    "pfRuleId": "9fb87f69-f3f6-4409-b5d8-0dad048fe1dd",
    "epfNumber": "string",
    "uanNumber": "string",
    "punchId": "string",
    "totalWorkingHourPerMonth": 0
  },
  "earningComponents": [
    {
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true
    }
  ],
  "deductionComponents": [
    {
      "componentId": "616ccd92-d666-4180-8349-8d125b269fac",
      "name": "string",
      "amount": 0,
      "isFixed": true
    }
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|bankDetails|[BankDetails](#schemabankdetails)|true|none||none|
|salaryDetails|[SalaryDetails](#schemasalarydetails)|true|none||none|
|earningComponents|[[SalaryComponent](#schemasalarycomponent)]|false|none||none|
|deductionComponents|[[SalaryComponent](#schemasalarycomponent)]|false|none||none|

<h2 id="tocS_ReportingManagerResponse">ReportingManagerResponse</h2>

<a id="schemareportingmanagerresponse"></a>
<a id="schema_ReportingManagerResponse"></a>
<a id="tocSreportingmanagerresponse"></a>
<a id="tocsreportingmanagerresponse"></a>

```json
{
  "employeeId": "b0e78c8f-8c8b-4410-8293-9239e39d9e17",
  "reportingManager": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "employeeIdentifier": "string",
    "fullName": "string",
    "designation": "string"
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|employeeId|string(uuid)|false|none||none|
|reportingManager|[Manager](#schemamanager)|false|none||none|

<h2 id="tocS_EmployeesUnderManager">EmployeesUnderManager</h2>

<a id="schemaemployeesundermanager"></a>
<a id="schema_EmployeesUnderManager"></a>
<a id="tocSemployeesundermanager"></a>
<a id="tocsemployeesundermanager"></a>

```json
{
  "manager": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "employeeIdentifier": "string",
    "fullName": "string",
    "designation": "string"
  },
  "employees": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "employeeIdentifier": "string",
      "fullName": "string",
      "designation": "string"
    }
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|manager|[Manager](#schemamanager)|false|none||none|
|employees|[[ManagerEmployee](#schemamanageremployee)]|false|none||none|

<h2 id="tocS_Manager">Manager</h2>

<a id="schemamanager"></a>
<a id="schema_Manager"></a>
<a id="tocSmanager"></a>
<a id="tocsmanager"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "employeeIdentifier": "string",
  "fullName": "string",
  "designation": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|false|none||none|
|employeeIdentifier|string|false|none||none|
|fullName|string|false|none||none|
|designation|string|false|none||none|

<h2 id="tocS_ManagerEmployee">ManagerEmployee</h2>

<a id="schemamanageremployee"></a>
<a id="schema_ManagerEmployee"></a>
<a id="tocSmanageremployee"></a>
<a id="tocsmanageremployee"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "employeeIdentifier": "string",
  "fullName": "string",
  "designation": "string"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|false|none||none|
|employeeIdentifier|string|false|none||none|
|fullName|string|false|none||none|
|designation|string|false|none||none|

<h2 id="tocS_DocumentRequest">DocumentRequest</h2>

<a id="schemadocumentrequest"></a>
<a id="schema_DocumentRequest"></a>
<a id="tocSdocumentrequest"></a>
<a id="tocsdocumentrequest"></a>

```json
{
  "title": "Student Certificate",
  "documentSize": 5,
  "documentTypes": [
    0,
    3,
    4
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|title|string|true|none||Document name (alphabets and spaces only)|
|documentSize|integer|true|none||Maximum file size allowed in MB|
|documentTypes|[integer]|true|none||Accepted document formats (number enum)|

<h2 id="tocS_DocumentItem">DocumentItem</h2>

<a id="schemadocumentitem"></a>
<a id="schema_DocumentItem"></a>
<a id="tocSdocumentitem"></a>
<a id="tocsdocumentitem"></a>

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Student Certificate",
  "documentSize": 5,
  "documentTypes": [
    0
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|false|none||none|
|title|string|false|none||none|
|documentSize|integer|false|none||none|
|documentTypes|[integer]|false|none||none|

<h2 id="tocS_EmployeeDocument">EmployeeDocument</h2>

<a id="schemaemployeedocument"></a>
<a id="schema_EmployeeDocument"></a>
<a id="tocSemployeedocument"></a>
<a id="tocsemployeedocument"></a>

```json
{
  "documentId": "4704590c-004e-410d-adf7-acb7ca0a7052",
  "documentNameId": "1b75b846-0301-44b4-8643-69de3873e58a",
  "documentName": "Jpg",
  "fileName": "string",
  "contentType": "string",
  "size": 0,
  "uploadedAt": "2019-08-24T14:15:22Z",
  "updatedAt": "2019-08-24T14:15:22Z"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|documentId|string(uuid)|false|none||Unique document identifier|
|documentNameId|string(uuid)|false|none||Document type identifier|
|documentName|string|false|none||Document type name|
|fileName|string|false|none||File name|
|contentType|string|false|none||MIME type|
|size|number|false|none||File size in bytes|
|uploadedAt|string(date-time)|false|none||Upload timestamp|
|updatedAt|string(date-time)|false|none||Last updated timestamp|

#### Enum

|Name|Value|
|---|---|
|documentName|Jpg|
|documentName|Jpeg|
|documentName|Png|
|documentName|Docx|
|documentName|Pdf|
|documentName|Xlsx|

<h2 id="tocS_WorkExperiencePage">WorkExperiencePage</h2>

<a id="schemaworkexperiencepage"></a>
<a id="schema_WorkExperiencePage"></a>
<a id="tocSworkexperiencepage"></a>
<a id="tocsworkexperiencepage"></a>

```json
{
  "totalCount": 25,
  "pageSize": 10,
  "totalPages": 3,
  "currentPage": 0,
  "items": [
    {
      "organizationName": "string",
      "designation": "string",
      "department": "string",
      "country": {
        "name": "string",
        "isoCode": "string"
      },
      "state": {
        "name": "string",
        "isoCode": "string"
      },
      "city": "string",
      "startDate": "2019-08-24T14:15:22Z",
      "endDate": "2019-08-24T14:15:22Z"
    }
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|totalCount|integer|true|none||Total number of work experience records|
|pageSize|integer|true|none||Number of records per page|
|totalPages|integer|true|none||Total number of pages|
|currentPage|integer|true|none||Current page number (0-based)|
|items|[[WorkExperience](#schemaworkexperience)]|true|none||List of work experience records|

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

<h2 id="tocS_EmployeeResponse">EmployeeResponse</h2>

<a id="schemaemployeeresponse"></a>
<a id="schema_EmployeeResponse"></a>
<a id="tocSemployeeresponse"></a>
<a id="tocsemployeeresponse"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||New generated guid|

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

<h2 id="tocS_EmployeeStatusString">EmployeeStatusString</h2>

<a id="schemaemployeestatusstring"></a>
<a id="schema_EmployeeStatusString"></a>
<a id="tocSemployeestatusstring"></a>
<a id="tocsemployeestatusstring"></a>

```json
"ACTIVE"

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|string|false|none||none|

#### Enum

|Name|Value|
|---|---|
|*anonymous*|ACTIVE|
|*anonymous*|INACTIVE|
|*anonymous*|LEFT|
|*anonymous*|RETIRED|

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

