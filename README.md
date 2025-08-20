# AddressBook
## AddressBook — Clean Architecture Web API (.NET 9)

A layered Address Book Web API with Contacts and Groups, using EF Core (SQLite), OpenIddict-based OAuth/OpenID Connect server scaffolding, Swagger, and unit tests.

### Features
- Contacts and Groups CRUD-like endpoints (create/update via upsert, get by id, paged list)
- Many-to-many relation between contacts and groups
- Pagination with X-Pagination response header
- EF Core 9 + SQLite with automatic migrations on startup
- OpenIddict server configured (token endpoint; client credentials flow implemented)
- JWT Bearer authentication configured (Authority/Audience validation; see Auth section)
- Swagger/OpenAPI UI in development with OAuth2 (client credentials) security scheme
- In-memory caching for list endpoints (60s TTL per page/pageSize)
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

- HTTPS: https://localhost:7255

Swagger UI:  https://localhost:7255/swagger

Database: SQLite file `addressbook.db` created under `AddressBook.Api/`.

Connection string: taken from `ConnectionStrings:DefaultConnection` if present, otherwise defaults to `Data Source=addressbook.db`.

---

## API endpoints

### Contacts (`AddressBook.Api/Controllers/ContactsController.cs`)
- Secured via [Authorize] policies: `WriteScope` for POST, `ReadScope` for GET
- POST `/api/contacts` — Upsert contact (create or update)
  - 201 Created when new contact; 200 OK when existing contact updated
- GET `/api/contacts/{id:guid}` — Get contact by id
  - 200 OK or 404 NotFound
- GET `/api/contacts?page=1&pageSize=10` — Paged contact list
  - Returns `X-Pagination` header: `Total`, `pageSize`, `currentPage`, `totalPages`
  - Returns 200 OK with empty array when no data
  - Note: Cache key includes the user's identity. With client-credentials tokens `User?.Identity?.Name` is typically null, so keys may be shared across clients (same key). Consider including client_id/subject in the key or switch to distributed cache.

### Groups (`AddressBook.Api/Controllers/GroupsController.cs`)
- Secured via [Authorize] policies: `WriteScope` for POST, `ReadScope` for GET
- POST `/api/groups` — Upsert group
- GET `/api/groups/{id:guid}` — Get group by id
- GET `/api/groups?page=1&pageSize=10` — Paged group list with `X-Pagination`
  - Returns `X-Pagination` header: `TotalCount`, `pageSize`, `currentPage`, `totalPages`

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
- Flow: Client Credentials (Authorization Code planned)
- Development signing/encryption certs + ephemeral keys (dev only)
- Seeding on startup:
  - Scopes `addressbook.read` and `addressbook.write`
  - Client `addressbook.client` with secret `secret` and client credentials grant (permissions include both scopes)

### Client Credentials quick test
Get token (replace base URL if using IIS Express)
```bash
curl -X POST https://localhost:7255/connect/token \
 -H "Content-Type: application/x-www-form-urlencoded" \
 -d "grant_type=client_credentials&client_id=addressbook.client&client_secret=secret&scope=addressbook.read%20addressbook.write"
```

Call a protected endpoint
```bash
curl https://localhost:7255/api/groups \
 -H "Authorization: Bearer <access_token>"
```

Note:
- The app currently sets the token issuer and JWT validation authority to `https://localhost:7255/`.

---

## Swagger/OpenAPI
- Enabled in development via `AddEndpointsApiExplorer()` and `AddSwaggerGen()`; UI available at `/swagger` on your chosen base URL.
- OAuth2 Client Credentials is configured (security scheme `oauth2`) with scopes `addressbook.read` and `addressbook.write`.
- In Swagger UI, click "Authorize":
  - Use the OAuth2 flow (
	- ClientId: `addressbook.client`, 
	- ClientSecret: `secret`,
	- select scopes - 
	- check addressbook.read and uncheck addressbook.write for get endpoints and 
	- addressbook.write and uncheck addressbook.read for write endpoints) — dev only;
  

---

## Caching
Current status: In-memory caching implemented for list endpoints (TTL 60s).
- `ContactsController.GetContactList()`: key is `contacts_{User?.Identity?.Name}_{page}_{pageSize}`
- `GroupsController.GetGroupList()`: key is `GroupList_{page}_{pageSize}`
Notes:
- Cache is per-process. Use `IDistributedCache`/Redis for multi-instance deployments.

---

## Common API use cases
- Manage contacts: upsert, get contact by id, paginated list
- Manage groups: upsert, get group by id, paginated list
- Associate contacts and groups via IDs in DTOs
- Secure endpoints using scope-based policies; ensure tokens include `addressbook.read` (GET) or `addressbook.write` (POST)

---

## Testing
- Framework: xUnit + Moq (see `AddressBook.Tests/`)
- Coverage: controller status codes and error paths, pagination headers, service upsert flows and mapping

Run tests:
```bash
dotnet test
```

## Known Issues and Considerations
- Issuer/Authority mismatch: JWT Bearer `Authority` is `https://localhost:7255/`.. Align issuer/authority and token `iss` in `AddressBook.Api/Program.cs` and `AuthorizationController.cs`.
- AcceptAnonymousClients vs ClientId: `AcceptAnonymousClients()` is enabled, but `AuthorizationController.Exchange()` requires `request.ClientId` (throws if null). Remove anonymous clients or handle missing client IDs.
- Dev-only settings: `RequireHttpsMetadata = false`, development certs, and `DisableAccessTokenEncryption()` are for local/dev only.
- Secrets in code: Client secret appears in `SeedOpenIddictClients()` and Swagger UI OAuth config. Move to configuration/user-secrets.
- Cache key collisions: `ContactsController` cache key uses `User?.Identity?.Name`; with client-credentials this is null and may cause cross-client cache sharing.
- No cache invalidation on writes; consider eviction strategy or cache busting.
- Minimal DTO validation; add data annotations or FluentValidation and consistent ProblemDetails responses.
- Read performance: consider `AsNoTracking()` and projection for read-only queries.
- Security hardening: add CORS, rate limiting, audit logging.
- Observability: health checks, correlation IDs, OpenTelemetry for traces/metrics.

## Troubleshooting
- 401/403: Ensure token has the right scopes (`addressbook.read` for GET, `addressbook.write` for POST) and `aud` = `addressbook.api`.
- Issuer/metadata issues: Make the running URL match `Authority` and the token `iss` claim; adjust `AuthorizationController` issuer if needed.
- SQLite DB issues: Confirm working directory and connection string (defaults to `Data Source=addressbook.db`).

## File References
- Auth/DI/Swagger: `AddressBook.Api/Program.cs`
- Token issuance: `AddressBook.Api/Controllers/AuthorizationController.cs`
- Controllers: `AddressBook.Api/Controllers/*`
- EF Core: `AddressBook.Infrastructure/AddressBookDbContext.cs`, `AddressBook.Infrastructure/Repositories/*`
- DTOs: `AddressBook.Application/DTO/*`
