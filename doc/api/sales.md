# Sales API

All routes require `Authorization: Bearer <token>`.

## Routes

| Method | Route | Success status |
|---|---|---|
| `POST` | `/api/sales` | `201` |
| `GET` | `/api/sales/{id}` | `200` |
| `GET` | `/api/sales` | `200` |
| `PUT` | `/api/sales/{id}` | `200` |
| `DELETE` | `/api/sales/{id}` | `200` |
| `DELETE` | `/api/sales/{saleId}/items/{itemId}` | `200` |

Successful responses use `ApiResponse` or `ApiResponseWithData<T>`. DELETE endpoints return an envelope with `200`, although the target contract documents `204`.

## Creation and replacement

The request uses flat fields, not nested `customer` and `branch` objects.

```json
{
  "saleNumber": "SALE-001",
  "saleDate": "2026-07-31T12:00:00Z",
  "customerId": "00000000-0000-0000-0000-000000000001",
  "customerName": "Customer",
  "branchId": "00000000-0000-0000-0000-000000000002",
  "branchName": "Branch",
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000003",
      "productName": "Product",
      "quantity": 4,
      "unitPrice": 10.00
    }
  ]
}
```

Dates must be UTC. Create/update requests reject unknown JSON properties. Do not send status, discounts, totals, cancellation, or audit timestamps as the source of truth; those values belong to the server.

In `PUT`, `items[].id` can identify an existing item. Omitted existing items are cancelled. A cancelled item is not reactivated by `PUT`, and the product of an existing item cannot be changed.

## Listing

Accepted parameters:

| Parameter | Rule |
|---|---|
| `_page` | one-based page; default 1 |
| `_size` | 1 to 100; default 10 |
| `_order` | allowed fields and `asc`/`desc` direction |
| `saleNumber` | text filter |
| `saleDateFrom`, `saleDateTo` | UTC instants ending in `Z` |
| `customerId`, `branchId` | canonical UUID |
| `customerName`, `branchName` | text filter |
| `status` | `Active` or `Cancelled` |

Unknown, repeated, empty, or non-whitelisted parameters return `400`. Default ordering is sale date descending and ID ascending. User-provided ordering also receives a deterministic ID tie-breaker.

### Example

```text
GET /api/sales?_page=1&_size=20&status=Active&_order=saleDate desc,id asc
```

The list returns summaries without the complete item collection; the ID query returns sale details.

## Cancellation

- Cancelling a sale or item is logical and idempotent.
- The last active item cannot be cancelled.
- Cancelled items remain for history and no longer contribute to current totals.
- Cancelling a sale preserves its historical total.

## Response and errors

The sale body is inside the envelope's `data` field and includes server-calculated values. Expected errors are `400` for an invalid request/query, `401` for authentication, `404` for a missing resource, `409` for a duplicate number, `422` for a domain rule, and `500` for an unexpected failure.
