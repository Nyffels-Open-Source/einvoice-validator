# EInvoicing.Validation.Api

Local Dockerized validation API for UBL e-invoices. It wraps the KoSIT validator and the official Peppol BIS Billing 3 validator configuration instead of implementing Peppol business rules in application code.

## Run

```bash
docker compose up --build
```

The service listens on `http://localhost:<VALIDATION_PORT>` (default `8080`).

OpenAPI is exposed at:

```text
http://localhost:<VALIDATION_PORT>/openapi/v1.json
```

Interactive Scalar API documentation is exposed at:

```text
http://localhost:<VALIDATION_PORT>/scalar/v1
```

## Validate XML

Maximum request body size is **10 MB** by default (configurable via `VALIDATION_MAX_REQUEST_SIZE_BYTES`).

```bash
curl -X POST http://localhost:${VALIDATION_PORT:-8080}/validate \
  -H "Content-Type: application/xml" \
  --data-binary @tests/Fixtures/invalid-missing-buyer-endpoint.xml
```

Optional profile header:

```bash
-H "X-Validation-Profile: peppol-bis3"
```

If omitted, the API defaults to `peppol-bis3`.

## Artefacts

Artefacts are stored under:

```text
/data/artefacts
```

With Docker Compose this is mounted to:

```text
./data
```

On first run, the service warms the local cache by downloading:

- KoSIT standalone validator release
- KoSIT Peppol BIS Billing validator configuration, which packages the Peppol validation artefacts

Later validation requests reuse the cached local artefacts. Normal `/validate` execution does not call an external validation service. If the warmup fails (e.g. no network on first start), call `POST /artefacts/update` manually to retry.

Manual update:

```bash
curl -X POST http://localhost:${VALIDATION_PORT:-8080}/artefacts/update \
  -H "X-Admin-Api-Key: <your-key>"
```

The update endpoint checks the latest upstream release and downloads it only when newer than the cached version.

### Protecting the update endpoint

Set `VALIDATION_ADMIN_API_KEY` (environment variable) or `Validation.AdminApiKey` (appsettings) to require the `X-Admin-Api-Key` header on `POST /artefacts/update`. When unset the endpoint is open, which is acceptable for isolated/local deployments but not for internet-facing ones.

```yaml
# docker-compose.yml
environment:
  VALIDATION_ADMIN_API_KEY: changeme
```


## Docker Compose example

This example includes the configurable port and all supported runtime options:

```yaml
services:
  einvoicing-validator:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: einvoicing-validator
    restart: unless-stopped
    ports:
      - "${VALIDATION_PORT:-8080}:${VALIDATION_PORT:-8080}"
    volumes:
      - ./data:/data
    environment:
      VALIDATION_PORT: ${VALIDATION_PORT:-8080}
      ASPNETCORE_URLS: http://+:${VALIDATION_PORT:-8080}
      VALIDATION_ARTEFACTS_PATH: /data/artefacts
      VALIDATION_MAX_REQUEST_SIZE_BYTES: 10485760
      VALIDATION_ADMIN_API_KEY: changeme
```

If `ASPNETCORE_URLS` is not set, the API also supports `VALIDATION_PORT` directly and binds to that port.

## Configuration

| Variable | Default | Description |
|---|---|---|
| `VALIDATION_PORT` | `8080` | Port used by the API when `ASPNETCORE_URLS` is not set |
| `VALIDATION_ARTEFACTS_PATH` | `/data/artefacts` | Directory where validator artefacts are stored |
| `VALIDATION_MAX_REQUEST_SIZE_BYTES` | `10485760` (10 MB) | Maximum request body size for `POST /validate` |
| `VALIDATION_ADMIN_API_KEY` | *(unset)* | API key required for `POST /artefacts/update`. Open when unset. |

## Endpoints

- `POST /validate`
- `GET /profiles`
- `GET /health`
- `POST /artefacts/update`
- `GET /openapi/v1.json`
- `GET /scalar/v1`

## Tests

```bash
dotnet test
```

The automated tests use fake artefact and validator implementations, so they do not require Java, Docker, or network access.

## UBL.be

The `ubl-be` profile is present in the API contract and returned by `/profiles`, but it is disabled. Requests that explicitly select it return `501` with `PROFILE-NOT-IMPLEMENTED`.
