# Users API

Current endpoints use UUIDs and response envelopes. There is no user-listing or user-update operation.

## Current endpoints

| Method | Route | Authentication | Result |
|---|---|---|---|
| `POST` | `/api/Users` | Currently public | `201` with the created user |
| `GET` | `/api/Users/{id}` | Currently public | `200` with the user |
| `DELETE` | `/api/Users/{id}` | Currently public | `200` without data |

Public access to retrieval and deletion is a known risk, not a security recommendation.

## Creation

The actual contract includes user fields, phone, password, status, and role. The password is stored as BCrypt and must never appear in responses. The server currently accepts status and role from the request; this must be treated as a mass-assignment risk.

```json
{
  "name": "Example User",
  "password": "Password1!",
  "phone": "+5511999999999",
  "email": "user@example.com",
  "status": 1,
  "role": 1
}
```

## Response

```json
{
  "success": true,
  "message": "User created successfully",
  "data": {
    "id": "00000000-0000-0000-0000-000000000000",
    "name": "Example User",
    "email": "user@example.com",
    "phone": "+5511999999999",
    "status": "Active",
    "role": "Customer"
  }
}
```

Responses do not include the password.

## Errors

- `400`: invalid UUID or data.
- `404`: user not found.
- `409`: duplicate email.
- `500`: unexpected failure.
