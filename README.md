# Secure Human Loop CAPTCHA Automation

This repository contains a reference implementation of a secure human-in-the-loop automation service. The system ingests jobs, runs Playwright-based browser automation, detects CAPTCHAs, escalates to human operators, and resumes processing once manual input is received.

## Solution layout
```
SecureHumanLoopCaptcha/
├── SecureHumanLoopCaptcha.sln
├── src/
│   ├── Shared/           # Entity models, DbContext, DTOs, messaging contracts
│   ├── Api/              # ASP.NET Core 8 minimal API and SignalR hub
│   └── Worker/           # Background worker running Playwright automation
├── infra/                # Dockerfiles and docker-compose for local development
└── ui/                   # Placeholder for future React dashboard
```

### Key services
- **API** (`src/Api`):
  - Provides the `/intake`, `/records/{id}`, `/jobs/awaiting`, `/jobs/{id}/claim`, and `/jobs/{id}/human-action` endpoints.
  - Persists encrypted payloads and audit actions to PostgreSQL via EF Core.
  - Publishes job messages to Redis and pushes status updates to SignalR clients.
- **Worker** (`src/Worker`):
  - Subscribes to the Redis job queue and executes Playwright automation.
  - Detects CAPTCHA challenges, stores screenshots/HTML snapshots, and waits for operators to respond.
  - Applies operator input, completes the automation run, and records audit events.

### Messaging channels
| Channel | Purpose |
| --- | --- |
| `automation-jobs` | New jobs available for the automation worker |
| `automation-awaiting-human` | Records that require human attention |
| `automation-human-actions` | Human-provided CAPTCHA or form responses |
| `automation-status-updates` | Status changes for real-time dashboards |

## Local development with Docker
1. Ensure Docker and Docker Compose are installed.
2. Navigate to the `infra` directory and run:
   ```bash
   docker compose up --build
   ```
3. The API will be exposed at `https://localhost:8080` (proxied through the ASP.NET HTTPS redirect). Update the OIDC configuration and encryption secrets as needed before deploying beyond local development.

Screenshots and HTML snapshots from the worker are written to the `worker-data` Docker volume by default.

## Security considerations
- Payloads and human inputs are encrypted at rest using AES before being stored in PostgreSQL.
- SignalR hub and REST endpoints are protected with JWT bearer authentication; configure your OIDC authority and audience in environment variables or configuration files.
- Every state transition is recorded in the `RecordAction` audit log for traceability.

## Next steps
- Implement the React-based operator UI inside the `ui/` folder.
- Configure Azure AD (or your preferred identity provider) for production authentication.
- Extend the worker automation flow to handle target-specific form input strategies.
- Add OpenTelemetry instrumentation for distributed tracing across services.
