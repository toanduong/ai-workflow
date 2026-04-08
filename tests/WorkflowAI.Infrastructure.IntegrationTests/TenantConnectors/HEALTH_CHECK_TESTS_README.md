# Apollo API Health Check Tests

This directory contains real integration tests that call the actual Apollo APIs and record their health status to the database.

## Test Files

### 1. `ApolloApiHealthCheckTests.cs`
**Purpose**: Unit-style tests for health check recording mechanism
**Trait**: `[Trait("Category", "AzureDb")]`
**Requirements**: Azure DB credentials only
**Tests** (7 total):
- Record successful health check (200 status, 245ms)
- Record failed health check (404 status)
- Record timeout/network error (null status code)
- Retrieve all health checks for connector
- Audit trail showing sequential check history
- Long error messages truncated to 1000 chars
- Calculate success rate and average duration metrics

**Run**:
```bash
PGHOST=... PGUSER=... PGPASSWORD=... PGPORT=... PGDATABASE=... \
dotnet test --filter "FullyQualifiedName~ApolloApiHealthCheckTests"
```

---

### 2. `ApolloApiHealthCheckRealTests.cs` ⭐ **NEW**
**Purpose**: Real end-to-end health checks that call actual Apollo APIs
**Trait**: `[Trait("Category", "AzureDb")]`
**Requirements**: 
- Azure DB credentials (PGHOST, PGUSER, PGPASSWORD, PGPORT, PGDATABASE)
- Valid Apollo API key (APOLLO_API_KEY)

**Tests** (5 total):
1. **Health check the auth/health endpoint**
   - Calls: `GET /v1/auth/health`
   - Records: Success/failure, status code, duration
   
2. **Health check the search people endpoint**
   - Calls: `POST /v1/mixed_people/search` with test body `{"q":"test"}`
   - Records: Success/failure, status code, duration

3. **Health check the get contact endpoint**
   - Calls: `GET /v1/contacts/test-invalid-id`
   - Records: Success/failure (likely 404), status code, duration

4. **Batch health check all Apollo APIs**
   - Calls all 3 endpoints above sequentially
   - Records results for each
   - Logs summary table

5. **Success rate and metrics calculation**
   - Runs health check 3 times
   - Calculates success rate and average duration
   - Demonstrates metrics computation from health checks

**API Credentials**:
Each test applies the API key via `x-api-key` header:
```csharp
request.Headers.Add("x-api-key", apiKey);
```

**Skip Behavior**:
If `APOLLO_API_KEY` env var is not set, tests throw `SkipTestException` and are skipped gracefully.

**Run**:
```bash
PGHOST=workflowai.postgres.database.azure.com \
PGUSER=postgres \
PGPASSWORD='...' \
PGPORT=5432 \
PGDATABASE=postgres \
APOLLO_API_KEY='sk-...' \
dotnet test --filter "FullyQualifiedName~ApolloApiHealthCheckRealTests"
```

**Console Output Example**:
```
✓ Health Check API: Success=True, Status=200, Duration=145ms
✓ Search People API: Success=True, Status=200, Duration=320ms
✓ Get Contact API: Success=False, Status=404, Duration=98ms

=== Batch Health Check Results ===
✓ Health Check: ✓ Success | Status=200 | Duration=142ms
✓ Search People: ✓ Success | Status=200 | Duration=315ms
✓ Get Contact: ✗ Failed | Status=404 | Duration=102ms

=== Health Check Metrics (last 3 checks) ===
Success Rate: 100% (3/3)
Avg Duration: 148ms
```

---

## Database Schema

Health check results are stored in `TenantConnectorApiHealthChecks` table:

```sql
CREATE TABLE TenantConnectorApiHealthChecks (
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL,
    TenantConnectorId UUID NOT NULL,
    TenantConnectorApiId UUID NOT NULL,
    ApiName VARCHAR NOT NULL,
    HttpMethod VARCHAR(10) NOT NULL,
    ResolvedUrl VARCHAR NOT NULL,
    IsSuccess BOOLEAN NOT NULL,
    StatusCode INT,                    -- NULL for timeouts/network errors
    DurationMs INT NOT NULL,
    FailureReason VARCHAR(1000),       -- Truncated to 1000 chars
    CreatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (TenantConnectorApiId) REFERENCES TenantConnectorApis(Id),
    FOREIGN KEY (TenantConnectorId) REFERENCES TenantConnectors(Id),
    FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
```

---

## API Endpoints Tested

All endpoints are for Apollo.io (`api.apollo.io`):

| Endpoint | Method | URL | Purpose |
|----------|--------|-----|---------|
| Health Check | GET | `/v1/auth/health` | Verify API key and auth |
| Search People | POST | `/v1/mixed_people/search` | Search contacts (requires `{"q":"search term"}`) |
| Get Contact | GET | `/v1/contacts/{id}` | Retrieve single contact (often returns 404 for test IDs) |

---

## Key Features

✅ **Real API Calls**: Calls actual Apollo APIs with real credentials
✅ **Error Handling**: Catches HTTP errors, timeouts, and network failures
✅ **Timeout Protection**: 30-second timeout per request
✅ **Audit Trail**: Records every health check result for historical analysis
✅ **Graceful Skip**: Skips tests if API key not configured (doesn't fail CI)
✅ **Metrics**: Calculates success rate, average duration, trend analysis
✅ **Truncation**: Error messages truncated to 1000 chars to prevent DB bloat

---

## No Cleanup

All health check records are **persisted to the database** for inspection and debugging.
To clean up: manually delete rows from `TenantConnectorApiHealthChecks` table.

This allows:
- Post-test inspection of API health patterns
- Historical audit trail of API availability
- Performance trend analysis
- Debugging failed API calls

---

## Integration with Connector Generation

These tests complement `GenerateConnectorAssetsCommandHandler`:
1. Handler **generates** API definitions from Claude
2. **This test** verifies those APIs are actually callable
3. Results are recorded for monitoring connector health

The generated APIs for Apollo are:
- `searchPeople` — Search for contacts
- `matchPerson` — Find matching person record
- `searchOrgs` — Search for organizations
- `enrollSequence` — Enroll contact in email sequence
- `getContact` — Get contact by ID
