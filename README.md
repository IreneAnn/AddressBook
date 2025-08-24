# AddressBook

![Swagger](.github/images/swagger.png?raw=true "Swagger")
## AddressBook — Clean Architecture Web API (.NET 9)

A layered Address Book Web API with Contacts and Groups, using SQLite with Dapper for application data access, OpenIddict-based OAuth/OpenID Connect server scaffolding (EF Core used for OpenIddict/migrations), Swagger, and unit tests.

### Features
- Contacts and Groups CRUD-like endpoints (create/update via upsert, get by id, paged list)
- Many-to-many relation between contacts and groups
- Pagination with X-Pagination response header
- SQLite with Dapper repositories for application data (Contacts/Groups)
- EF Core is used for OpenIddict stores and schema migrations; runtime data access for Contacts/Groups uses Dapper repositories
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
- `AddressBook.Infrastructure/` — Dapper context and repositories for app data; EF Core DbContext is retained for OpenIddict and migrations
- `AddressBook.Tests/` — xUnit tests for controllers and services

## Data access: Dapper vs EF Core
- Runtime data access for Contacts/Groups uses Dapper repositories for lean, explicit SQL and control over queries.
- EF Core remains for OpenIddict stores and for schema migrations (including Contacts/Groups tables). Repositories do not use EF at runtime.
- Repositories: `AddressBook.Infrastructure/Repositories/ContactRepository.cs`, `AddressBook.Infrastructure/Repositories/GroupRepository.cs`
- Context and setup: `AddressBook.Infrastructure/DapperContext.cs`, `AddressBook.Infrastructure/DapperTypeHandlers.cs`
- Schema: `AddressBook.Infrastructure/AddressBookDbContext.cs` and `AddressBook.Infrastructure/Migrations/`

---

## Domain model
- `Contact`: `Id`, `FirstName`, `LastName`, `Email`, `PhoneNumber`, `Groups`
- `Group`: `Id`, `Name`, `Contacts`
- Relationship: many-to-many via `ContactGroups` join table
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

- HTTP: http://localhost:8080

Swagger UI:  http://localhost:8080/swagger

Database: SQLite file `addressbook.db` created under `AddressBook.Api/`.

Connection string: taken from `ConnectionStrings:DefaultConnection` if present, otherwise defaults to `Data Source=addressbook.db`.

---
## Docker

![Docker Build Push](.github/images/docker_build_push.jpg?raw=true "Docker Build Push")

Build image (local):
```bash
docker build -t irene22/address-book-api:latest .
```

Run container (maps HTTP 8080):
```bash
docker run -p 8080:8080 --name address-book-api irene22/address-book-api:latest
```

Push image (already pushed by maintainer):
```bash
docker push irene22/address-book-api:latest
```

Pull from Docker Hub:
```bash
docker pull irene22/address-book-api:latest
```

Docker Hub: https://hub.docker.com/r/irene22/address-book-api/tags

Note: The image is already pushed to Docker Hub. Users can pull and run directly using the commands above.

---
## CI/CD (GitHub Actions)
- Workflow file: `.github/workflows/docker-publish.yml`
- Trigger: runs on every `push` to any branch.
- Steps:
  - Login to Docker Hub using `DOCKER_USERNAME` and `DOCKER_PASSWORD` secrets.
  - Build image: `docker build -t irene22/address-book-api:latest .`
  - Push image: `docker push irene22/address-book-api:latest`

To enable:
- Add repo secrets `DOCKER_USERNAME` and `DOCKER_PASSWORD` in GitHub > Settings > Secrets and variables > Actions.

---
## Swagger/OpenAPI
- Enabled in development via `AddEndpointsApiExplorer()` and `AddSwaggerGen()`; UI available at `/swagger` on your chosen base URL.
- OAuth2 Client Credentials is configured (security scheme `oauth2`) with scopes `addressbook.read` and `addressbook.write`.
- In Swagger UI, click "**Authorize**":
  - Use the OAuth2 flow (
	- **ClientId**: `addressbook.client`, 
	- **ClientSecret**: `secret`,
	- select scopes -
    - **Write scope** :'addressbook.read addressbook.write'
    - **Read scope** : 'addressbook.read'


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
  - Note: Cache key is per page/pageSize. Results are shared across clients that request the same page and size; consider including client_id/subject in the key or using distributed cache if needed.

### Groups (`AddressBook.Api/Controllers/GroupsController.cs`)
- Secured via [Authorize] policies: `WriteScope` for POST, `ReadScope` for GET
- POST `/api/groups` — Upsert group
- GET `/api/groups/{id:guid}` — Get group by id
- GET `/api/groups?page=1&pageSize=10` — Paged group list with `X-Pagination`
  - Returns `X-Pagination` header: `Total`, `pageSize`, `currentPage`, `totalPages`

### Sample payloads
Create/Update contact
```json
  {
    "id": "a7712c75-40ba-4f0f-bcb1-f7648c8df90f",
    "firstName": "Thomas",
    "lastName": "Alexander",
    "phoneNumber": "02214275254",
    "email": "thomasalexander@gmail.com",
    "groupIds": [
      "1957b7c4-f352-416f-a574-071a489c98cc"
    ]
  }
```

Create/Update group
```json
 {
    "id": "1957b7c4-f352-416f-a574-071a489c98cc",
    "name": "Family",
    "contactIds": [
      "a7712c75-40ba-4f0f-bcb1-f7648c8df90f"
    ]
  }
```

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
curl -X POST http://localhost:8080/connect/token \
 -H "Content-Type: application/x-www-form-urlencoded" \
 -d "grant_type=client_credentials&client_id=addressbook.client&client_secret=secret&scope=addressbook.read%20addressbook.write"
```

Call a protected endpoint
```bash
curl http://localhost:8080/api/groups \
 -H "Authorization: Bearer <access_token>"
```

Note:
- The app currently sets the token issuer and JWT validation authority to `http://localhost:8080/`.

---

## Caching
Current status: In-memory caching implemented for list endpoints (TTL 60s).
- `ContactService.GetContactListAsync()`: key is `contacts_{page}_{pageSize}`
- `GroupService.GetGroupListAsync()`: key is `groups_{page}_{pageSize}`
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
- Issuer/Authority mismatch: JWT Bearer `Authority` is `http://localhost:8080/`.. Align issuer/authority and token `iss` in `AddressBook.Api/Program.cs` and `AuthorizationController.cs`.
- AcceptAnonymousClients vs ClientId: `AcceptAnonymousClients()` is enabled, but `AuthorizationController.Exchange()` requires `request.ClientId` (throws if null). Remove anonymous clients or handle missing client IDs.
- Dev-only settings: `RequireHttpsMetadata = false`, development certs, and `DisableAccessTokenEncryption()` are for local/dev only.
- Secrets in code: Client secret appears in `SeedOpenIddictClients()` and Swagger UI OAuth config. Move to configuration/user-secrets.
- Cache scope: Cache keys are per `page`/`pageSize` and shared across clients for list endpoints. Consider including client_id/tenant in keys or using distributed cache.
- No cache invalidation on writes; consider eviction strategy or cache busting.
- Minimal DTO validation; add data annotations or FluentValidation and consistent ProblemDetails responses.
- Read performance: with Dapper, queries are non-tracking by default. Consider selecting only needed columns and projecting directly to DTOs.
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
- Dapper (app data): `AddressBook.Infrastructure/DapperContext.cs`, `AddressBook.Infrastructure/Repositories/*`
- EF Core (OpenIddict + schema/migrations): `AddressBook.Infrastructure/AddressBookDbContext.cs`
- DTOs: `AddressBook.Application/DTO/*`

AddressBook
Create a basic .Net Core restful API which implements an address book of contacts where a contact has the following details:

•	First name
•	Last name
•	Phone number
•	Email

Also include the ability to add a contact to a “group”. Where a contact can belong to multiple groups; each group just has a name.

The app should implement the following:

•	An endpoint/API call to add/update a new group
•	An endpoint/API call to add/update a new contact
•	An endpoint/API call to get a list of groups and an individual group
•	An endpoint/API call to get a list of contacts and an individual contact
•	Support pagination
•	The endpoints should be secured by OAuth client credential flow

You can use SQLite as the db, please provide this also when you submit your code along with relevant user credentials to access the calls.
