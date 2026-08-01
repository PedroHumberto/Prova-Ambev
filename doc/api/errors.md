# HTTP Errors

The system uses RFC Problem Details. Model-binding validation errors may include field-level `errors`.

## Mapping

| HTTP | Usage |
|---|---|
| `400` | validation, invalid JSON, unknown query, or invalid parameter |
| `401` | missing, invalid, or rejected authentication |
| `404` | resource not found |
| `409` | duplicate email or sale number |
| `422` | Sales domain rule failure |
| `500` | unexpected error |

### Example

```json
{
  "type": "https://httpstatuses.com/422",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "A sale must have at least one active item.",
  "instance": "/api/sales/00000000-0000-0000-0000-000000000000"
}
```

The format does not use the `error` field described by the original template. Clients should handle `status`, `title`, `detail`, and, when present, `errors` without depending on a specific message.
