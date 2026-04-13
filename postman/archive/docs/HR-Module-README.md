# HR Module API - Postman Collection

## 📋 Overview

This Postman collection provides comprehensive API testing coverage for the **ShikshaMitra HR Module**, including:

- **Leave Management** (Leave Types & Policies)
- **Payroll Management** (Earning & Deduction Components)
- **Employee Management** (Documents, Payroll Details, Additional Details)

## 🚀 Quick Start

### 1. Import Collection & Environment

1. Open Postman
2. Click **Import** button
3. Import both files:
   - `HR-Module.postman_collection.json`
   - `ShikshaMitra-Consolidated.postman_environment.json`
4. Select the **"ShikshaMitra - All Modules"** environment from the environment dropdown

### 2. Configure Environment Variables

Update these variables in your environment:

| Variable | Description | Example Value |
|----------|-------------|---------------|
| `base_url` | Local development URL | `https://localhost:44300` |
| `staging_url` | Staging server URL | `https://staging-gyanmirai-be.azurewebsites.net` |
| `tenant_id` | Tenant identifier | `3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e` |
| `username` | Login username | `admin` |
| `password` | Login password | `Temp@123` |
| `employee_id` | Test employee ID | `3a1e19bb-4fe8-be24-a3d5-6c64963783b2` |

### 3. Authenticate

1. Navigate to **Authentication → Login**
2. Click **Send**
3. The `access_token` will be automatically saved to your environment

## 📖 API Endpoints

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/connect/token` | Login and get access token |

### Leave Management

#### Leave Types

| Method | Endpoint | Description | Permission Required |
|--------|----------|-------------|---------------------|
| GET | `/api/attendance/leave-types` | Get all leave types (paginated) | View |
| GET | `/api/attendance/leave-types/active` | Get active leave types | View |
| GET | `/api/attendance/leave-types/{id}` | Get leave type by ID | View |
| POST | `/api/attendance/leave-types` | Create leave type | Add |
| PUT | `/api/attendance/leave-types/{id}` | Update leave type | Add |
| DELETE | `/api/attendance/leave-types/{id}` | Delete leave type | Delete |

#### Leave Policies

| Method | Endpoint | Description | Permission Required |
|--------|----------|-------------|---------------------|
| GET | `/api/attendance/leave-policies` | Get all leave policies | View |
| GET | `/api/attendance/leave-policies/active` | Get active policies | View |
| GET | `/api/attendance/leave-policies/{id}` | Get policy by ID | View |
| POST | `/api/attendance/leave-policies` | Create policy | Add |
| PUT | `/api/attendance/leave-policies/{id}` | Update policy | Add |
| DELETE | `/api/attendance/leave-policies/{id}` | Delete policy | Delete |

### Payroll Management

#### Earning Components

| Method | Endpoint | Description | Permission Required |
|--------|----------|-------------|---------------------|
| GET | `/api/payroll/earning-components` | Get all earning components | View |
| GET | `/api/payroll/earning-components/active` | Get active components | View |
| GET | `/api/payroll/earning-components/{id}` | Get component by ID | View |
| POST | `/api/payroll/earning-components` | Create component | Add |
| PUT | `/api/payroll/earning-components/{id}` | Update component | Add |
| DELETE | `/api/payroll/earning-components/{id}` | Delete component | Delete |
| POST | `/api/payroll/earning-components/{id}/activate` | Activate component | Add |
| POST | `/api/payroll/earning-components/{id}/deactivate` | Deactivate component | Add |

#### Deduction Components

| Method | Endpoint | Description | Permission Required |
|--------|----------|-------------|---------------------|
| GET | `/api/payroll/deduction-components` | Get all deduction components | View |
| GET | `/api/payroll/deduction-components/active` | Get active components | View |
| GET | `/api/payroll/deduction-components/{id}` | Get component by ID | View |
| POST | `/api/payroll/deduction-components` | Create component | Add |
| PUT | `/api/payroll/deduction-components/{id}` | Update component | Add |
| DELETE | `/api/payroll/deduction-components/{id}` | Delete component | Delete |
| POST | `/api/payroll/deduction-components/{id}/activate` | Activate component | Add |
| POST | `/api/payroll/deduction-components/{id}/deactivate` | Deactivate component | Add |

### Employee Management

#### Employee Documents

| Method | Endpoint | Description | Permission Required |
|--------|----------|-------------|---------------------|
| GET | `/api/employee/{employeeId}/documents` | Get all documents | View |
| GET | `/api/employee/{employeeId}/documents/{documentId}` | Get document by ID | View |
| PUT | `/api/employee/{employeeId}/documents` | Upload documents (multipart) | Add |
| PUT | `/api/employee/{employeeId}/documents/{documentId}` | Update document (multipart) | Add |

#### Employee Payroll

| Method | Endpoint | Description | Permission Required |
|--------|----------|-------------|---------------------|
| GET | `/api/employees/{employeeId}/payroll` | Get payroll details | View |
| PUT | `/api/employees/{employeeId}/payroll` | Update payroll details | Add |
| PUT | `/api/employee/{employeeId}/payroll-structure` | Update payroll structure (legacy) | Add |

#### Employee Additional Details

| Method | Endpoint | Description | Permission Required |
|--------|----------|-------------|---------------------|
| GET | `/api/app/employee/{employeeId}/additional-details` | Get additional details | View |
| PUT | `/api/app/employee/{employeeId}/additional-details` | Update additional details | Add |

## 📝 Sample Requests

### Create Leave Type

```json
POST /api/attendance/leave-types
{
  "name": "Sick Leave",
  "shortName": "SL",
  "description": "Leave for medical reasons",
  "isPaid": true,
  "isActive": true
}
```

### Create Earning Component

```json
POST /api/payroll/earning-components
{
  "name": "Basic Salary",
  "description": "Monthly basic salary",
  "componentType": 0,
  "dependOn": 0,
  "amount": "50000",
  "visibleInPayslip": true,
  "displayOrder": 1
}
```

### Update Employee Payroll Details

```json
PUT /api/employees/{employeeId}/payroll
{
  "payrollDetails": {
    "bankName": "HDFC Bank",
    "branchName": "Mumbai Main",
    "accountHolderName": "John Doe",
    "accountNumber": "12345678901234",
    "ifscCode": "HDFC0001234",
    "pfRule": "Standard",
    "epfNumber": "MH/MUM/1234567/000/0000001",
    "uanNumber": "123456789012",
    "punchId": "EMP001",
    "basicSalary": 50000,
    "tdsPercentage": 10,
    "tdsAmount": 5000
  },
  "earnings": [
    {
      "componentName": "Basic Salary",
      "type": 0,
      "amount": 50000
    },
    {
      "componentName": "HRA",
      "type": 0,
      "amount": 20000
    }
  ],
  "deductions": [
    {
      "componentName": "PF",
      "type": 1,
      "amount": 6000
    },
    {
      "componentName": "TDS",
      "type": 1,
      "amount": 5000
    }
  ]
}
```

### Upload Employee Documents

```
PUT /api/employee/{employeeId}/documents
Content-Type: multipart/form-data

files: [Select files from file system]
```

### Update Employee Additional Details

```json
PUT /api/app/employee/{employeeId}/additional-details
{
  "bplCardNo": "BPL123456",
  "personWithDisability": false,
  "secondLanguage": "Hindi",
  "libraryMember": true,
  "transportMember": true
}
```

## 🔐 Authorization

All endpoints require authentication. The collection uses Bearer token authentication automatically using the `{{access_token}}` variable.

### Required Headers

```
Authorization: Bearer {{access_token}}
__tenant: {{tenant_id}}
Content-Type: application/json (for JSON requests)
```

## 🧪 Testing Workflow

### 1. Leave Management Flow

1. **Login** to get access token
2. **Create Leave Type** → Saves `leave_type_id`
3. **Get All Leave Types** → Verify creation
4. **Create Leave Policy** using the `leave_type_id`
5. **Update Leave Policy** → Test updates
6. **Get Leave Policy by ID** → Verify update
7. **Delete Leave Policy** → Cleanup

### 2. Payroll Management Flow

1. **Login** to get access token
2. **Create Earning Component** → Saves `earning_component_id`
3. **Create Deduction Component** → Saves `deduction_component_id`
4. **Get Active Components** → Verify creation
5. **Deactivate Component** → Test activation/deactivation
6. **Activate Component** → Re-enable
7. **Update Components** → Test modifications
8. **Delete Components** → Cleanup

### 3. Employee Management Flow

1. **Login** to get access token
2. **Get Employee Additional Details** → View current data
3. **Update Additional Details** → Modify employee info
4. **Get Payroll Details** → View current payroll
5. **Update Payroll Details** → Modify payroll configuration
6. **Upload Documents** → Add employee documents
7. **Get All Documents** → Verify upload
8. **Update Document** → Replace existing document

## 📊 Auto-Saved Variables

The collection automatically saves these variables from responses:

- `access_token` - From login response
- `leave_type_id` - From create leave type
- `leave_policy_id` - From create leave policy
- `earning_component_id` - From create earning component
- `deduction_component_id` - From create deduction component

## ❌ Common Error Responses

### 400 Bad Request - Validation Error

```json
{
  "error": {
    "code": "Volo.Abp.Validation.AbpValidationException",
    "message": "Request validation failed",
    "validationErrors": [
      {
        "message": "The Name field is required.",
        "members": ["Name"]
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

**Solution:** Run the Login request to get a new access token.

### 403 Forbidden

```json
{
  "error": {
    "code": "403",
    "message": "Authorization failed! Given policy has not granted."
  }
}
```

**Solution:** User doesn't have required permissions. Login with admin account.

### 404 Not Found

```json
{
  "error": {
    "code": "404",
    "message": "Entity not found"
  }
}
```

**Solution:** Verify the ID in the request URL is correct.

## 🔧 Environment Switching

### Local Development
```
base_url = https://localhost:44300
```

### Staging
```
base_url = https://staging-gyanmirai-be.azurewebsites.net
```

To switch environments:
1. Duplicate the environment
2. Change `base_url` value
3. Optionally enable `staging_url` variable
4. Select the new environment

## 🎯 Permission Matrix

| Feature | View | Add | Delete |
|---------|------|-----|--------|
| Leave Types | ✅ | ✅ | ✅ |
| Leave Policies | ✅ | ✅ | ✅ |
| Earning Components | ✅ | ✅ | ✅ |
| Deduction Components | ✅ | ✅ | ✅ |
| Employee Documents | ✅ | ✅ | ❌ |
| Employee Payroll | ✅ | ✅ | ❌ |
| Employee Additional Details | ✅ | ✅ | ❌ |

## 📚 Additional Resources

- [ABP Framework Documentation](https://docs.abp.io/)
- [API Reference Documentation](./API-REFERENCE.md)
- [ShikshaMitra Postman Collection](./ShikshaMitra.postman_collection.json)

## 🐛 Troubleshooting

### Issue: Token Expired
**Solution:** Run the Login request again to get a fresh token.

### Issue: 415 Unsupported Media Type (Document Upload)
**Solution:** Ensure `Content-Type` is set to `multipart/form-data` in the request headers.

### Issue: Empty File Error
**Solution:** Make sure files are properly selected in the form-data body before sending the request.

### Issue: Environment Variables Not Working
**Solution:** 
1. Check environment is selected in dropdown
2. Verify variable names match collection usage
3. Re-import environment file if needed

## 📝 Notes

- All IDs are GUIDs
- Pagination uses `SkipCount` and `MaxResultCount` query parameters
- File uploads use multipart/form-data
- All timestamps are in UTC
- Soft delete is not implemented (hard delete only)

## ✅ Collection Features

- ✅ Bearer token authentication
- ✅ Auto-save response IDs
- ✅ Environment variables support
- ✅ Pre-configured sample data
- ✅ Comprehensive test coverage
- ✅ Multi-environment support
- ✅ Error response examples

---

**Version:** 1.0.0  
**Last Updated:** 2025-01-15  
**Module:** HR Module  
**Collection ID:** `hr-module-api-collection`
