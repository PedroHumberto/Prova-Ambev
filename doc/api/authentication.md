# Authentication API

## `POST /api/Auth`

Public endpoint. Receives an email and password and returns an envelope containing a JWT for an active user.

### Request

```json
{
  "email": "demo.admin@ambev.local",
  "password": "DemoPassword1!"
}
```

### Success `200`

```json
{
  "success": true,
  "message": "User authenticated successfully",
  "data": {
    "token": "eyJ...",
    "email": "demo.admin@ambev.local",
    "name": "Demo Administrator",
    "role": "Admin"
  }
}
```

The token expires after eight hours and contains identity, name, and role claims. Send it as `Authorization: Bearer <token>` to protected endpoints.

### Errors

- `400`: invalid body or validation failure.
- `401`: invalid credentials or inactive user.
- `500`: unexpected failure.

See the [error format](errors.md).

## Operational security

The JWT secret must be supplied externally in non-local environments. The key in `appsettings.json` is a development default only and must not be promoted.
