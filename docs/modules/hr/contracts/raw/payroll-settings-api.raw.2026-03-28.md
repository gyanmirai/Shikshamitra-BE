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

# Payroll Management/Payroll Setting/Payroll configuration

## POST Save day calculation settings

POST /api/payroll/day-calculation

Stores the selected day calculation method. - Working days: no extra fields required - Total days:
  - Total month days OR
  - Manual days (1–31)

> Body Parameters

```json
{
  "dayCalculationType": "working-days"
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|body|body|[DayCalculationRequest](#schemadaycalculationrequest)| yes |none|

> Response Examples

> 200 Response

```json
{
  "success": true,
  "message": "Attendance configuration saved successfully"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Settings saved successfully|[SuccessResponse](#schemasuccessresponse)|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Validation error|[ErrorResponse](#schemaerrorresponse)|

# Payroll Management/Payroll Setting/Earning Components

## GET Get earning components (paginated)

GET /api/payroll/earning-components

Retrieves a paginated list of earning components.

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|page|query|integer| no |Page number|
|pageSize|query|integer| no |Number of records per page|
|search|query|string| no |none|

> Response Examples

> 200 Response

```json
{
  "totalCount": 0,
  "currentPage": 1,
  "pageSize": 10,
  "totalPages": 0,
  "items": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "earningComponentName": "Basic Salary",
      "description": "Monthly fixed salary",
      "earningType": "FIXED",
      "calculationType": "AMOUNT",
      "amount": 25000,
      "percentage": 40,
      "selectedEarningComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "visibleInPayslip": true,
      "customComponent": {
        "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
        "componentName": "string",
        "componentOf": "GROSS",
        "description": "string",
        "visibleInPayslip": true
      }
    }
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Earning components retrieved successfully|[EarningComponentPaginatedResponse](#schemaearningcomponentpaginatedresponse)|

<a id="opIdcreateEarningComponent"></a>

## POST Create earning component

POST /api/payroll/earning-components

> Body Parameters

```json
{
  "earningComponentName": "Basic Salary",
  "description": "Monthly fixed salary",
  "earningType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 25000,
  "percentage": 40,
  "selectedEarningComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|body|body|[PayrollEarningComponentCreateRequest](#schemapayrollearningcomponentcreaterequest)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|201|[Created](https://tools.ietf.org/html/rfc7231#section-6.3.2)|Earning component created successfully|None|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Validation error|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|

<a id="opIddeleteEarningComponent"></a>

## DELETE Delete earning component

DELETE /api/payroll/earning-components/{id}

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|204|[No Content](https://tools.ietf.org/html/rfc7231#section-6.3.5)|No Content|None|

<a id="opIdupdateEarningComponent"></a>

## PUT Update earning component

PUT /api/payroll/earning-components/{id}

> Body Parameters

```json
{
  "earningComponentName": "Basic Salary",
  "description": "Updated description",
  "earningType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 28000,
  "percentage": 45,
  "selectedEarningComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|
|body|body|[PayrollEarningComponentUpdateRequest](#schemapayrollearningcomponentupdaterequest)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Earning component updated successfully|None|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Validation error|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Earning component not found|None|

## GET Get earning component by ID

GET /api/payroll/earning-components/{id}

Retrieves a single earning component using its unique ID.

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "earningComponentName": "Basic Salary",
  "description": "Monthly fixed salary",
  "earningType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 25000,
  "percentage": 40,
  "selectedEarningComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true,
  "customComponent": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "componentName": "string",
    "componentOf": "GROSS",
    "description": "string",
    "visibleInPayslip": true
  }
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Earning component retrieved successfully|[EarningComponent](#schemaearningcomponent)|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Earning component not found|None|

# Payroll Management/Payroll Setting/Deduction Components

<a id="opIddeleteDeductionComponent"></a>

## DELETE Delete deduction component

DELETE /api/payroll/deduction-components/{id}

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|204|[No Content](https://tools.ietf.org/html/rfc7231#section-6.3.5)|No Content|None|

<a id="opIdupdateDeductionComponent"></a>

## PUT Update deduction component

PUT /api/payroll/deduction-components/{id}

> Body Parameters

```json
{
  "deductionComponentName": "PF Deduction",
  "description": "Provident Fund deduction",
  "deductionType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 1800,
  "percentage": 12,
  "selectedDeductionComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|
|body|body|[PayrollDeductionComponentUpdateRequest](#schemapayrolldeductioncomponentupdaterequest)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Deduction component updated successfully|None|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Validation error|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Deduction component not found|None|

## GET Get deduction component by ID

GET /api/payroll/deduction-components/{id}

Retrieves a deduction component by its ID.

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |none|

> Response Examples

> 200 Response

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "deductionComponentName": "PF Deduction",
  "description": "Provident Fund deduction",
  "deductionType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 1800,
  "percentage": 12,
  "selectedDeductionComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true,
  "customComponent": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "componentName": "string",
    "componentOf": "GROSS",
    "description": "string",
    "visibleInPayslip": true
  }
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Deduction component retrieved successfully|[DeductionComponent](#schemadeductioncomponent)|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|Deduction component not found|None|

<a id="opIdcreateDeductionComponent"></a>

## POST Create deduction component

POST /api/payroll/deduction-components

> Body Parameters

```json
{
  "deductionComponentName": "PF Deduction",
  "description": "Provident Fund deduction",
  "deductionType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 1800,
  "percentage": 12,
  "selectedDeductionComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|body|body|[PayrollDeductionComponentCreateRequest](#schemapayrolldeductioncomponentcreaterequest)| yes |none|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|201|[Created](https://tools.ietf.org/html/rfc7231#section-6.3.2)|Deduction component created successfully|None|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Validation error|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|

## GET Get deduction components (paginated)

GET /api/payroll/deduction-components

Retrieves a paginated list of deduction components.

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|page|query|integer| no |Page number|
|pageSize|query|integer| no |Number of records per page|
|search|query|string| no |none|

> Response Examples

> 200 Response

```json
{
  "totalCount": 0,
  "currentPage": 1,
  "pageSize": 10,
  "totalPages": 0,
  "items": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "deductionComponentName": "PF Deduction",
      "description": "Provident Fund deduction",
      "deductionType": "FIXED",
      "calculationType": "AMOUNT",
      "amount": 1800,
      "percentage": 12,
      "selectedDeductionComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "visibleInPayslip": true,
      "customComponent": {
        "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
        "componentName": "string",
        "componentOf": "GROSS",
        "description": "string",
        "visibleInPayslip": true
      }
    }
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|Deduction components retrieved successfully|[DeductionComponentPaginatedResponse](#schemadeductioncomponentpaginatedresponse)|

# Payroll Management/Payroll Setting/PF Rules

<a id="opIdcreatePfRule"></a>

## POST Create PF rule

POST /api/payroll/pf-rules

Creates a new Provident Fund (PF) rule

> Body Parameters

```json
{
  "name": "string",
  "code": "string",
  "employeePercentage": 100,
  "employerPercentage": 100
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|body|body|[PfRuleCreateRequest](#schemapfrulecreaterequest)| yes |none|

> Response Examples

> 201 Response

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "createdAt": "2019-08-24T14:15:22Z"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|201|[Created](https://tools.ietf.org/html/rfc7231#section-6.3.2)|PF rule created successfully|[PfRuleCreateResponse](#schemapfrulecreateresponse)|
|400|[Bad Request](https://tools.ietf.org/html/rfc7231#section-6.5.1)|Validation error|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|403|[Forbidden](https://tools.ietf.org/html/rfc7231#section-6.5.3)|Payroll Admin only|None|

<a id="opIdgetPfRules"></a>

## GET Get PF rules

GET /api/payroll/pf-rules

Retrieves paginated list of PF rules

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|page|query|integer| no |none|
|pageSize|query|integer| no |none|
|search|query|string| no |none|

> Response Examples

> 200 Response

```json
{
  "totalCount": 0,
  "currentPage": 1,
  "pageSize": 0,
  "totalPages": 0,
  "items": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "name": "string",
      "code": "string",
      "employeePercentage": 0,
      "employerPercentage": 0,
      "createdAt": "2019-08-24T14:15:22Z",
      "updatedAt": "2019-08-24T14:15:22Z"
    }
  ]
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|PF rules retrieved successfully|[PaginatedPfRuleResponse](#schemapaginatedpfruleresponse)|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|403|[Forbidden](https://tools.ietf.org/html/rfc7231#section-6.5.3)|Forbidden|None|

<a id="opIdupdatePfRule"></a>

## PUT Update PF rule

PUT /api/payroll/pf-rules/{id}

Updates an existing PF rule

> Body Parameters

```json
{
  "name": "string",
  "code": "string",
  "employeePercentage": 100,
  "employerPercentage": 100
}
```

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |PF rule ID|
|body|body|[PfRuleCreateRequest](#schemapfrulecreaterequest)| yes |none|

> Response Examples

> 200 Response

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "updatedAt": "2019-08-24T14:15:22Z"
}
```

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|200|[OK](https://tools.ietf.org/html/rfc7231#section-6.3.1)|PF rule updated successfully|[PfRuleUpdateResponse](#schemapfruleupdateresponse)|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|403|[Forbidden](https://tools.ietf.org/html/rfc7231#section-6.5.3)|Payroll Admin only|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|PF rule not found|None|

<a id="opIddeletePfRule"></a>

## DELETE Delete PF rule

DELETE /api/payroll/pf-rules/{id}

Deletes a PF rule

### Params

|Name|Location|Type|Required|Description|
|---|---|---|---|---|
|id|path|string(uuid)| yes |PF rule ID|

### Responses

|HTTP Status Code |Meaning|Description|Data schema|
|---|---|---|---|
|204|[No Content](https://tools.ietf.org/html/rfc7231#section-6.3.5)|No Content|None|
|401|[Unauthorized](https://tools.ietf.org/html/rfc7235#section-3.1)|Unauthorized|None|
|403|[Forbidden](https://tools.ietf.org/html/rfc7231#section-6.5.3)|Payroll Admin only|None|
|404|[Not Found](https://tools.ietf.org/html/rfc7231#section-6.5.4)|PF rule not found|None|

# Data Schema

<h2 id="tocS_EarningComponent">EarningComponent</h2>

<a id="schemaearningcomponent"></a>
<a id="schema_EarningComponent"></a>
<a id="tocSearningcomponent"></a>
<a id="tocsearningcomponent"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "earningComponentName": "Basic Salary",
  "description": "Monthly fixed salary",
  "earningType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 25000,
  "percentage": 40,
  "selectedEarningComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true,
  "customComponent": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "componentName": "string",
    "componentOf": "GROSS",
    "description": "string",
    "visibleInPayslip": true
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||Earning component ID|
|earningComponentName|string|true|none||Earning component name|
|description|string¦null|false|none||Optional description|
|earningType|[EarningTypeEnum](#schemaearningtypeenum)|true|none||FIXED or VARIABLE|
|calculationType|[CalculationTypeEnum](#schemacalculationtypeenum)|true|none||AMOUNT or PERCENTAGE|
|amount|number¦null|false|none||Fixed amount (used when calculationType = AMOUNT)|
|percentage|number¦null|false|none||Percentage value (used when calculationType = PERCENTAGE)|
|selectedEarningComponentId|string(uuid)¦null|false|none||Component on which percentage is calculated|
|visibleInPayslip|boolean|true|none||Whether visible in payslip|
|customComponent|[CustomComponent](#schemacustomcomponent)|false|none||none|

<h2 id="tocS_DeductionComponent">DeductionComponent</h2>

<a id="schemadeductioncomponent"></a>
<a id="schema_DeductionComponent"></a>
<a id="tocSdeductioncomponent"></a>
<a id="tocsdeductioncomponent"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "deductionComponentName": "PF Deduction",
  "description": "Provident Fund deduction",
  "deductionType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 1800,
  "percentage": 12,
  "selectedDeductionComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true,
  "customComponent": {
    "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
    "componentName": "string",
    "componentOf": "GROSS",
    "description": "string",
    "visibleInPayslip": true
  }
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||Deduction component ID|
|deductionComponentName|string|true|none||Deduction component name|
|description|string¦null|false|none||Optional description|
|deductionType|[DeductionTypeEnum](#schemadeductiontypeenum)|true|none||FIXED or VARIABLE|
|calculationType|[CalculationTypeEnum](#schemacalculationtypeenum)|true|none||AMOUNT or PERCENTAGE|
|amount|number¦null|false|none||Fixed amount (used when calculationType = AMOUNT)|
|percentage|number¦null|false|none||Percentage value (used when calculationType = PERCENTAGE)|
|selectedDeductionComponentId|string(uuid)¦null|false|none||Component on which percentage is calculated|
|visibleInPayslip|boolean|true|none||Whether visible in payslip|
|customComponent|[CustomComponent](#schemacustomcomponent)|false|none||none|

<h2 id="tocS_PfRuleCreateRequest">PfRuleCreateRequest</h2>

<a id="schemapfrulecreaterequest"></a>
<a id="schema_PfRuleCreateRequest"></a>
<a id="tocSpfrulecreaterequest"></a>
<a id="tocspfrulecreaterequest"></a>

```json
{
  "name": "string",
  "code": "string",
  "employeePercentage": 100,
  "employerPercentage": 100
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|name|string|true|none||Human-readable PF rule name|
|code|string|true|none||Unique PF rule code (uppercase)|
|employeePercentage|number|true|none||Employee contribution percentage|
|employerPercentage|number|true|none||Employer contribution percentage|

<h2 id="tocS_PfRuleCreateResponse">PfRuleCreateResponse</h2>

<a id="schemapfrulecreateresponse"></a>
<a id="schema_PfRuleCreateResponse"></a>
<a id="tocSpfrulecreateresponse"></a>
<a id="tocspfrulecreateresponse"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "createdAt": "2019-08-24T14:15:22Z"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||Newly created PF rule ID|
|createdAt|string(date-time)|true|none||Creation timestamp|

<h2 id="tocS_PfRuleUpdateResponse">PfRuleUpdateResponse</h2>

<a id="schemapfruleupdateresponse"></a>
<a id="schema_PfRuleUpdateResponse"></a>
<a id="tocSpfruleupdateresponse"></a>
<a id="tocspfruleupdateresponse"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "updatedAt": "2019-08-24T14:15:22Z"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||PF rule ID|
|updatedAt|string(date-time)|true|none||Last update timestamp|

<h2 id="tocS_PfRuleResponse">PfRuleResponse</h2>

<a id="schemapfruleresponse"></a>
<a id="schema_PfRuleResponse"></a>
<a id="tocSpfruleresponse"></a>
<a id="tocspfruleresponse"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "name": "string",
  "code": "string",
  "employeePercentage": 0,
  "employerPercentage": 0,
  "createdAt": "2019-08-24T14:15:22Z",
  "updatedAt": "2019-08-24T14:15:22Z"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||none|
|name|string|true|none||none|
|code|string|true|none||none|
|employeePercentage|number|true|none||none|
|employerPercentage|number|true|none||none|
|createdAt|string(date-time)|true|none||none|
|updatedAt|string(date-time)¦null|false|none||none|

<h2 id="tocS_PaginatedPfRuleResponse">PaginatedPfRuleResponse</h2>

<a id="schemapaginatedpfruleresponse"></a>
<a id="schema_PaginatedPfRuleResponse"></a>
<a id="tocSpaginatedpfruleresponse"></a>
<a id="tocspaginatedpfruleresponse"></a>

```json
{
  "totalCount": 0,
  "currentPage": 1,
  "pageSize": 0,
  "totalPages": 0,
  "items": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "name": "string",
      "code": "string",
      "employeePercentage": 0,
      "employerPercentage": 0,
      "createdAt": "2019-08-24T14:15:22Z",
      "updatedAt": "2019-08-24T14:15:22Z"
    }
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|totalCount|integer|true|none||none|
|currentPage|integer|true|none||none|
|pageSize|integer|true|none||none|
|totalPages|integer|true|none||none|
|items|[[PfRuleResponse](#schemapfruleresponse)]|true|none||none|

<h2 id="tocS_DeductionComponentPaginatedResponse">DeductionComponentPaginatedResponse</h2>

<a id="schemadeductioncomponentpaginatedresponse"></a>
<a id="schema_DeductionComponentPaginatedResponse"></a>
<a id="tocSdeductioncomponentpaginatedresponse"></a>
<a id="tocsdeductioncomponentpaginatedresponse"></a>

```json
{
  "totalCount": 0,
  "currentPage": 1,
  "pageSize": 10,
  "totalPages": 0,
  "items": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "deductionComponentName": "PF Deduction",
      "description": "Provident Fund deduction",
      "deductionType": "FIXED",
      "calculationType": "AMOUNT",
      "amount": 1800,
      "percentage": 12,
      "selectedDeductionComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "visibleInPayslip": true,
      "customComponent": {
        "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
        "componentName": "string",
        "componentOf": "GROSS",
        "description": "string",
        "visibleInPayslip": true
      }
    }
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|totalCount|integer|true|none||Total deduction components|
|currentPage|integer|true|none||Current page number|
|pageSize|integer|true|none||Page size used|
|totalPages|integer|true|none||Computed total pages|
|items|[[DeductionComponent](#schemadeductioncomponent)]|true|none||List of deduction components|

<h2 id="tocS_CustomComponent">CustomComponent</h2>

<a id="schemacustomcomponent"></a>
<a id="schema_CustomComponent"></a>
<a id="tocScustomcomponent"></a>
<a id="tocscustomcomponent"></a>

```json
{
  "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
  "componentName": "string",
  "componentOf": "GROSS",
  "description": "string",
  "visibleInPayslip": true
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|id|string(uuid)|true|none||Dependent component ID|
|componentName|string|true|none||Dependent component name|
|componentOf|string|true|none||Dependency base|
|description|string¦null|false|none||none|
|visibleInPayslip|boolean|true|none||none|

#### Enum

|Name|Value|
|---|---|
|componentOf|GROSS|
|componentOf|BASIC|
|componentOf|CUSTOM_COMPONENT|

<h2 id="tocS_ErrorResponse">ErrorResponse</h2>

<a id="schemaerrorresponse"></a>
<a id="schema_ErrorResponse"></a>
<a id="tocSerrorresponse"></a>
<a id="tocserrorresponse"></a>

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields failed validation",
    "fields": {
      "boardId": "Must be a valid UUID",
      "studentId": "Must be a valid UUID"
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

<h2 id="tocS_EarningTypeEnum">EarningTypeEnum</h2>

<a id="schemaearningtypeenum"></a>
<a id="schema_EarningTypeEnum"></a>
<a id="tocSearningtypeenum"></a>
<a id="tocsearningtypeenum"></a>

```json
"FIXED"

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|string|false|none||none|

#### Enum

|Name|Value|
|---|---|
|*anonymous*|FIXED|
|*anonymous*|VARIABLE|

<h2 id="tocS_CalculationTypeEnum">CalculationTypeEnum</h2>

<a id="schemacalculationtypeenum"></a>
<a id="schema_CalculationTypeEnum"></a>
<a id="tocScalculationtypeenum"></a>
<a id="tocscalculationtypeenum"></a>

```json
"AMOUNT"

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|string|false|none||none|

#### Enum

|Name|Value|
|---|---|
|*anonymous*|AMOUNT|
|*anonymous*|PERCENTAGE|

<h2 id="tocS_PayrollEarningComponentCreateRequest">PayrollEarningComponentCreateRequest</h2>

<a id="schemapayrollearningcomponentcreaterequest"></a>
<a id="schema_PayrollEarningComponentCreateRequest"></a>
<a id="tocSpayrollearningcomponentcreaterequest"></a>
<a id="tocspayrollearningcomponentcreaterequest"></a>

```json
{
  "earningComponentName": "Basic Salary",
  "description": "Monthly fixed salary",
  "earningType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 25000,
  "percentage": 40,
  "selectedEarningComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|earningComponentName|string|true|none||Name of the earning component|
|description|string¦null|false|none||Optional description|
|earningType|[EarningTypeEnum](#schemaearningtypeenum)|true|none||FIXED or VARIABLE. - FIXED → value is mandatory - VARIABLE → value is optional|
|calculationType|[CalculationTypeEnum](#schemacalculationtypeenum)|true|none||Determines how the earning is calculated: - AMOUNT → fixed amount - PERCENTAGE → percentage of another component|
|amount|number¦null|false|none||Used when calculationType is AMOUNT. Represents fixed earning amount.|
|percentage|number¦null|false|none||Used when calculationType is PERCENTAGE. Must be between 0 and 100.|
|selectedEarningComponentId|string(uuid)¦null|false|none||Required when calculationType is PERCENTAGE. Percentage will be calculated based on this component.|
|visibleInPayslip|boolean|true|none||Whether component is shown on payslip|

<h2 id="tocS_PayrollEarningComponentUpdateRequest">PayrollEarningComponentUpdateRequest</h2>

<a id="schemapayrollearningcomponentupdaterequest"></a>
<a id="schema_PayrollEarningComponentUpdateRequest"></a>
<a id="tocSpayrollearningcomponentupdaterequest"></a>
<a id="tocspayrollearningcomponentupdaterequest"></a>

```json
{
  "earningComponentName": "Basic Salary",
  "description": "Updated description",
  "earningType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 28000,
  "percentage": 45,
  "selectedEarningComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|earningComponentName|string|true|none||Name of the earning component|
|description|string¦null|false|none||Optional description|
|earningType|[EarningTypeEnum](#schemaearningtypeenum)|true|none||FIXED or VARIABLE. - FIXED → value is mandatory - VARIABLE → value is optional|
|calculationType|[CalculationTypeEnum](#schemacalculationtypeenum)|true|none||Determines how the earning is calculated: - AMOUNT → fixed amount - PERCENTAGE → percentage of another component|
|amount|number¦null|false|none||Used when calculationType is AMOUNT. Represents fixed earning amount.|
|percentage|number¦null|false|none||Used when calculationType is PERCENTAGE. Must be between 0 and 100.|
|selectedEarningComponentId|string(uuid)¦null|false|none||Required when calculationType is PERCENTAGE. Percentage will be calculated based on this component.|
|visibleInPayslip|boolean|true|none||Whether component is shown on payslip|

<h2 id="tocS_EarningComponentPaginatedResponse">EarningComponentPaginatedResponse</h2>

<a id="schemaearningcomponentpaginatedresponse"></a>
<a id="schema_EarningComponentPaginatedResponse"></a>
<a id="tocSearningcomponentpaginatedresponse"></a>
<a id="tocsearningcomponentpaginatedresponse"></a>

```json
{
  "totalCount": 0,
  "currentPage": 1,
  "pageSize": 10,
  "totalPages": 0,
  "items": [
    {
      "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
      "earningComponentName": "Basic Salary",
      "description": "Monthly fixed salary",
      "earningType": "FIXED",
      "calculationType": "AMOUNT",
      "amount": 25000,
      "percentage": 40,
      "selectedEarningComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "visibleInPayslip": true,
      "customComponent": {
        "id": "497f6eca-6276-4993-bfeb-53cbbbba6f08",
        "componentName": "string",
        "componentOf": "GROSS",
        "description": "string",
        "visibleInPayslip": true
      }
    }
  ]
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|totalCount|integer|true|none||Total earning components|
|currentPage|integer|true|none||Current page number|
|pageSize|integer|true|none||Page size used|
|totalPages|integer|true|none||Computed total pages|
|items|[[EarningComponent](#schemaearningcomponent)]|true|none||List of earning components|

<h2 id="tocS_DeductionTypeEnum">DeductionTypeEnum</h2>

<a id="schemadeductiontypeenum"></a>
<a id="schema_DeductionTypeEnum"></a>
<a id="tocSdeductiontypeenum"></a>
<a id="tocsdeductiontypeenum"></a>

```json
"FIXED"

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|string|false|none||none|

#### Enum

|Name|Value|
|---|---|
|*anonymous*|FIXED|
|*anonymous*|VARIABLE|

<h2 id="tocS_PayrollDeductionComponentCreateRequest">PayrollDeductionComponentCreateRequest</h2>

<a id="schemapayrolldeductioncomponentcreaterequest"></a>
<a id="schema_PayrollDeductionComponentCreateRequest"></a>
<a id="tocSpayrolldeductioncomponentcreaterequest"></a>
<a id="tocspayrolldeductioncomponentcreaterequest"></a>

```json
{
  "deductionComponentName": "PF Deduction",
  "description": "Provident Fund deduction",
  "deductionType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 1800,
  "percentage": 12,
  "selectedDeductionComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|deductionComponentName|string|true|none||Name of the deduction component|
|description|string¦null|false|none||Optional description|
|deductionType|[DeductionTypeEnum](#schemadeductiontypeenum)|true|none||FIXED or VARIABLE. - FIXED → value is mandatory - VARIABLE → value is optional|
|calculationType|[CalculationTypeEnum](#schemacalculationtypeenum)|true|none||Determines how the deduction is calculated: - AMOUNT → fixed amount - PERCENTAGE → percentage of another component|
|amount|number¦null|false|none||Used when calculationType is AMOUNT. Represents fixed deduction amount.|
|percentage|number¦null|false|none||Used when calculationType is PERCENTAGE. Must be between 0 and 100.|
|selectedDeductionComponentId|string(uuid)¦null|false|none||Required when calculationType is PERCENTAGE. Percentage will be calculated based on this component.|
|visibleInPayslip|boolean|true|none||Whether component is shown on payslip|

<h2 id="tocS_PayrollDeductionComponentUpdateRequest">PayrollDeductionComponentUpdateRequest</h2>

<a id="schemapayrolldeductioncomponentupdaterequest"></a>
<a id="schema_PayrollDeductionComponentUpdateRequest"></a>
<a id="tocSpayrolldeductioncomponentupdaterequest"></a>
<a id="tocspayrolldeductioncomponentupdaterequest"></a>

```json
{
  "deductionComponentName": "PF Deduction",
  "description": "Provident Fund deduction",
  "deductionType": "FIXED",
  "calculationType": "AMOUNT",
  "amount": 1800,
  "percentage": 12,
  "selectedDeductionComponentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "visibleInPayslip": true
}

```

### Attribute

*None*

<h2 id="tocS_DayCalculationRequest">DayCalculationRequest</h2>

<a id="schemadaycalculationrequest"></a>
<a id="schema_DayCalculationRequest"></a>
<a id="tocSdaycalculationrequest"></a>
<a id="tocsdaycalculationrequest"></a>

```json
{
  "dayCalculationType": "working-days",
  "totalDayType": "manual-days",
  "manualDays": 30
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|dayCalculationType|string|true|none||Determines the main day calculation method|
|totalDayType|string|false|none||Required when dayCalculationType is total-days. Determines how total days are calculated.|
|manualDays|integer|false|none||Required only when totalDayType is manual-days.|

oneOf

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|object|false|none||none|
|» dayCalculationType|string|true|none||none|

xor

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|object|false|none||none|
|» dayCalculationType|string|true|none||none|
|» totalDayType|string|true|none||none|

xor

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|*anonymous*|object|false|none||none|
|» dayCalculationType|string|true|none||none|
|» totalDayType|string|true|none||none|

#### Enum

|Name|Value|
|---|---|
|dayCalculationType|working-days|
|dayCalculationType|total-days|
|totalDayType|total-month-days|
|totalDayType|manual-days|
|dayCalculationType|working-days|
|dayCalculationType|total-days|
|totalDayType|total-month-days|
|dayCalculationType|total-days|
|totalDayType|manual-days|

<h2 id="tocS_SuccessResponse">SuccessResponse</h2>

<a id="schemasuccessresponse"></a>
<a id="schema_SuccessResponse"></a>
<a id="tocSsuccessresponse"></a>
<a id="tocssuccessresponse"></a>

```json
{
  "success": true,
  "message": "Attendance configuration saved successfully"
}

```

### Attribute

|Name|Type|Required|Restrictions|Title|Description|
|---|---|---|---|---|---|
|success|boolean|false|none||none|
|message|string|false|none||none|

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

<h2 id="tocS_ErrorResponse2">ErrorResponse2</h2>

<a id="schemaerrorresponse2"></a>
<a id="schema_ErrorResponse2"></a>
<a id="tocSerrorresponse2"></a>
<a id="tocserrorresponse2"></a>

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

<h2 id="tocS_ErrorResponse3">ErrorResponse3</h2>

<a id="schemaerrorresponse3"></a>
<a id="schema_ErrorResponse3"></a>
<a id="tocSerrorresponse3"></a>
<a id="tocserrorresponse3"></a>

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

<h2 id="tocS_ErrorResponse8">ErrorResponse8</h2>

<a id="schemaerrorresponse8"></a>
<a id="schema_ErrorResponse8"></a>
<a id="tocSerrorresponse8"></a>
<a id="tocserrorresponse8"></a>

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

<h2 id="tocS_ErrorResponse89">ErrorResponse89</h2>

<a id="schemaerrorresponse89"></a>
<a id="schema_ErrorResponse89"></a>
<a id="tocSerrorresponse89"></a>
<a id="tocserrorresponse89"></a>

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

<h2 id="tocS_ErrorResponse810">ErrorResponse810</h2>

<a id="schemaerrorresponse810"></a>
<a id="schema_ErrorResponse810"></a>
<a id="tocSerrorresponse810"></a>
<a id="tocserrorresponse810"></a>

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

<h2 id="tocS_StandardError11">StandardError11</h2>

<a id="schemastandarderror11"></a>
<a id="schema_StandardError11"></a>
<a id="tocSstandarderror11"></a>
<a id="tocsstandarderror11"></a>

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

<h2 id="tocS_ErrorResponse412">ErrorResponse412</h2>

<a id="schemaerrorresponse412"></a>
<a id="schema_ErrorResponse412"></a>
<a id="tocSerrorresponse412"></a>
<a id="tocserrorresponse412"></a>

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

