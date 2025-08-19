# AddressBook
# AddressBook
## AddressBook — Clean Architecture Web API (.NET 9)

A layered Address Book Web API with Contacts and Groups, using EF Core (SQLite), OpenIddict-based OAuth/OpenID Connect server scaffolding, Swagger, and unit tests.

### Features
- Contacts and Groups CRUD-like endpoints (create/update via upsert, get by id, paged list)
- Many-to-many relation between contacts and groups
- Pagination with X-Pagination response header
- EF Core 9 + SQLite with automatic migrations on startup
- OpenIddict server scaffold (token endpoint, client credentials and authorization code flows)
- JWT Bearer authentication pipeline (requires Authority/validation wiring)
- Swagger/OpenAPI UI in development
- Unit tests with xUnit and Moq

---

## Solution layout
- `AddressBook.Api/` — ASP.NET Core Web API, DI, EF/OpenIddict setup, controllers, Swagger
- `AddressBook.Application/` — DTOs, interfaces, and services (business logic)
- `AddressBook.Domain/` — entities (`Contact`, `Group`) and enums (`UpsertStatus`)
- `AddressBook.Infrastructure/` — EF Core DbContext and repositories
- `AddressBook.Tests/` — xUnit tests for controllers and services

## Domain model
- `Contact`: `Id`, `FirstName`, `LastName`, `Email`, `PhoneNumber`, `Groups`
- `Group`: `Id`, `Name`, `Contacts`
- Relationship: many-to-many (`ContactGroups` join table configured in `AddressBookDbContext`)
- `UpsertStatus`: `None | Created | Updated`

---

## Getting started
### Prerequisites
- .NET SDK 9.0+

### Run locally
```bash
dotnet restore
dotnet run -p AddressBook.Api
```

Default URLs (see `AddressBook.Api/Properties/launchSettings.json`):
- HTTP: http://localhost:5134
- HTTPS: https://localhost:7255

Swagger UI: https://localhost:44397/swagger

Database: SQLite file `addressbook.db` created under `AddressBook.Api/`.

Connection string: taken from `ConnectionStrings:DefaultConnection` if present, otherwise defaults to `Data Source=addressbook.db`.

---

## API endpoints

### Contacts (`AddressBook.Api/Controllers/ContactsController.cs`)
- POST `/api/contacts` — Upsert contact (create or update)
  - 201 Created when new contact; 200 OK when existing contact updated
- GET `/api/contacts/{id:guid}` — Get contact by id
  - 200 OK or 404 NotFound
- GET `/api/contacts?page=1&pageSize=10` — Paged contact list
  - Returns `X-Pagination` header: `totalCount`, `pageSize`, `currentPage`, `totalPages`
    -returns 404 when empty 

### Groups (`AddressBook.Api/Controllers/GroupsController.cs`)
- Controller is decorated with `[Authorize(Policy = "WriteScope")]` or `[Authorize(Policy = "ReadScope")]'
- POST `/api/groups` — Upsert group
- GET `/api/groups/{id:guid}` — Get group by id
- GET `/api/groups?page=1&pageSize=10` — Paged group list with `X-Pagination`

### Sample payloads
Create/Update contact
```json
{
  "id": null,
  "firstName": "Ada",
  "lastName": "Lovelace",
  "phoneNumber": "123456789",
  "email": "ada@example.com",
  "groupIds": ["11111111-1111-1111-1111-111111111111"]
}
```

Create/Update group
```json
{
  "id": null,
  "name": "Friends",
  "contactIds": []
}
```

---

## Authentication and OAuth (OpenIddict)
Auth is scaffolded in `AddressBook.Api/Program.cs`.

Configured:
- Token endpoint: `/connect/token`
- Flows: Client Credentials, Authorization Code
- Development signing/encryption certs + ephemeral keys (dev only)
- Seeding on startup:
  - Scope `addressbook.api`
  - Client `addressbook.client` with secret `secret` and client credentials grant permission

### Client Credentials quick test (after wiring validation)
Get token
```bash
curl -X POST https://localhost:7255/connect/token \
 -H "Content-Type: application/x-www-form-urlencoded" \
 -d "grant_type=client_credentials&client_id=addressbook.client&client_secret=secret&scope=addressbook.api"
```

Call a protected endpoint
```bash
curl https://localhost:44397/api/groups \
 -H "Authorization: Bearer <access_token>"
```

---

## Swagger/OpenAPI
- Enabled in development via `AddEndpointsApiExplorer()` and `AddSwaggerGen()`; UI at `/swagger`.
- Recommended: Add OAuth2 security definition and requirement so you can authorize in Swagger UI.

---

## Caching
Current status: No caching implemented.

Options to consider:
- Response caching for list endpoints (`AddResponseCaching`, `UseResponseCaching`, `[ResponseCache]`)
- HTTP caching headers/ETags for GETs
- IMemoryCache for in-process caching; IDistributedCache/Redis for production
- EF optimizations: compiled queries, `AsNoTracking` for read-only queries, appropriate indexes

---

## Common API use cases
- Manage contacts: upsert, get contact by id, paginated list
- Manage groups: upsert, get group by id, paginated list
- Associate contacts and groups via IDs in DTOs
- Secure endpoints using scope-based policy once JWT validation/policy are configured

---

## Testing
- Framework: xUnit + Moq (see `AddressBook.Tests/`)
- Coverage: controller status codes and error paths, pagination headers, service upsert flows and mapping

Run tests:
```bash
dotnet test
```

## Future improvements
- Complete OpenIddict validation wiring; implement scope/role-based authorization
- Add Authorization Code + PKCE, refresh tokens, and user management if needed
- Filtering/sorting/search on list endpoints; API versioning
- Standardize pagination (return 200 on empty), add PATCH and ETags for concurrency
- Introduce caching (response, ETag, Redis) and performance tuning
- Auditing, soft deletes, timestamps, concurrency tokens
- Observability: structured logging, correlation IDs, health checks, OpenTelemetry traces/metrics
- DX/Ops: CI/CD, containerization, IaC
- Extensibility: domain events/outbox, background jobs, consider GraphQL/gRPC if appropriate
