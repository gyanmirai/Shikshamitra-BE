# HR Employee Personal Info API (deprecated)

This file previously contained an auto-generated Slate-style contract that **does not match** the current backend.

**Use instead:**

- **Canonical contract:** [docs/modules/hr/contracts/curated/employees-api.md](./modules/hr/contracts/curated/employees-api.md) (employee personal-info section).
- **Live surface:** JSON `POST/GET/PUT` under `/api/employees/personal-info` and `/api/employees/{employeeId}/personal-info` — see [PersonalInfoController.cs](../src/ShikshaMitra.HttpApi/Controllers/HR/Employees/PersonalInfoController.cs).
- **Generated OpenAPI:** build the host and use the emitted OpenAPI artifacts under `src/ShikshaMitra.HttpApi.Host/obj/Debug/net9.0/EndpointInfo/` (paths vary by configuration), or open Swagger UI when running `ShikshaMitra.HttpApi.Host`.

The old document described multipart uploads, `get-personal-info`, and response/enumeration shapes that are **not** implemented in this repository.
