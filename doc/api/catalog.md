# Catalog API

All endpoints require a JWT. The catalog is a projection of historical names and IDs found in Sales; it is not a CRUD system for products, customers, or branches.

## Endpoints

| Method | Route |
|---|---|
| `GET` | `/api/catalog/customers` |
| `GET` | `/api/catalog/branches` |
| `GET` | `/api/catalog/products` |

## Query string

- `search`: optional; case-insensitive contains search.
- `limit`: optional; constrained by the catalog contract.
- Unknown, repeated, or empty parameters are rejected.

### Example

```text
GET /api/catalog/products?search=cola&limit=20
Authorization: Bearer <token>
```

### Success `200`

```json
{
  "success": true,
  "message": "Products retrieved successfully",
  "data": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "name": "Cola"
    }
  ]
}
```
