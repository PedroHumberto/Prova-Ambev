# Project Structure

```text
.
|-- backend/
|   |-- src/
|   |   |-- Ambev.DeveloperEvaluation.Application/
|   |   |-- Ambev.DeveloperEvaluation.Common/
|   |   |-- Ambev.DeveloperEvaluation.Contracts/
|   |   |-- Ambev.DeveloperEvaluation.Domain/
|   |   |-- Ambev.DeveloperEvaluation.IoC/
|   |   |-- Ambev.DeveloperEvaluation.ORM/
|   |   `-- Ambev.DeveloperEvaluation.WebApi/
|   `-- tests/
|       |-- Ambev.DeveloperEvaluation.Unit/
|       |-- Ambev.DeveloperEvaluation.Integration/
|       `-- Ambev.DeveloperEvaluation.Functional/
|-- frontend/
|-- doc/
|-- .github/workflows/
|-- global.json
`-- README.md
```

Layer and test boundaries are described in the [system overview](system-overview.md) and [test strategy](testing/test-strategy.md).
