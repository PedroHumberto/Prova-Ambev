# Target Contract Differences

Sales functional ADRs describe a target contract. Use the current code for integration until a coordinated change is implemented.

| Topic | Target contract | Current API |
|---|---|---|
| Customer and branch | nested objects | flat `customerId`, `customerName`, `branchId`, `branchName` fields |
| Success | direct payload | `ApiResponse`/`ApiResponseWithData` envelope |
| DELETE | `204` without a body | `200` with an envelope |
| Filters | broader contract | whitelist implemented for the parameters described in [Sales](sales.md) |
| Events | versioned contracts | outbox and dispatcher implemented; consumers are external |

This matrix prevents the ADR from being treated as a literal description of the current HTTP API. Any future convergence must update DTOs, validators, mappings, controllers, Swagger, frontend, and tests in the same change.
