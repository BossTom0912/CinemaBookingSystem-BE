# Project Documentation

This directory is the single home for project documentation. `README.md` and
`AGENTS.md` remain at the repository root because GitHub, GitLab, and coding
agents discover them there automatically.

## Requirements

- [`requirements/srs-group-2.docx`](requirements/srs-group-2.docx): current
  Software Requirements Specification.
- [`requirements/business-rules.docx`](requirements/business-rules.docx):
  authoritative business rules.
- [`requirements/movie-theater-srs-v1.2.docx`](requirements/movie-theater-srs-v1.2.docx):
  legacy/reference SRS.

## Architecture

- [`architecture/backend-system-design-clean-architecture.docx`](architecture/backend-system-design-clean-architecture.docx)
- [`architecture/conceptual-erd-explanation.docx`](architecture/conceptual-erd-explanation.docx)
- [`architecture/database-deep-dive-vi.md`](architecture/database-deep-dive-vi.md)
- [`architecture/clean-architecture-notes.md`](architecture/clean-architecture-notes.md)
- [`architecture/context-diagram-en.md`](architecture/context-diagram-en.md)
- [`architecture/context-diagram-vi.md`](architecture/context-diagram-vi.md)
- [`architecture/implemented-features-and-class-flow-vi.md`](architecture/implemented-features-and-class-flow-vi.md):
  current implementation inventory, role/login explanation, use-case status,
  and Controller-to-Service-to-database flow map for the team.
- [`architecture/api-role-business-flow-guide-vi.md`](architecture/api-role-business-flow-guide-vi.md):
  complete current API inventory, role matrix, business rules, request pipeline,
  and end-to-end class flow guide in Vietnamese.

## API Contracts

- [`api/api-contract-backend.docx`](api/api-contract-backend.docx)
- [`api/api-contract-movie-showtime.docx`](api/api-contract-movie-showtime.docx)
- [`api/admin-account-provisioning-vi.md`](api/admin-account-provisioning-vi.md):
  SQL deployment, API contract, error handling and FE flow for the
  data-driven Admin account-provisioning feature.
- [`api/examples/create-order.json`](api/examples/create-order.json)
- [`api/examples/seat-map.json`](api/examples/seat-map.json)

## Database

- [`database/postgresql-staging-test-guide.md`](database/postgresql-staging-test-guide.md):
  required clone, migration, and smoke-test procedure before deploying this
  PostgreSQL branch. It explicitly protects the production database.
- [`database/cinema-booking-schema.sql`](database/cinema-booking-schema.sql):
  legacy SQL Server reset script. Do not run it against PostgreSQL, staging,
  or production.
- For PostgreSQL deployments, apply the reviewed EF migrations to a staging
  clone first; do not use the legacy reset script as a deployment mechanism.
- Development fixtures are kept as rerunnable `.txt` scripts rather than schema
  files: `dev-seed-admin-manager-staff.txt`,
  `dev-seed-paid-ticket-ready-to-scan.txt`, and
  `dev-seed-10-movies-booking-payment-qr.txt`.
- [`database/dev-seed-voucher-compensation-flow.txt`](database/dev-seed-voucher-compensation-flow.txt):
  three rerunnable future showtimes (2D, IMAX and VIP) for the complete
  Customer booking/payment -> Admin cancellation -> compensation voucher test.
- [`reports/SCRUM-198-ticket-scan-db-changes.md`](reports/SCRUM-198-ticket-scan-db-changes.md):
  team handoff and deployment notes for ticket-scan actor auditing.
- [`reports/SCRUM-193-customer-assisted-refund-db-changes.md`](reports/SCRUM-193-customer-assisted-refund-db-changes.md):
  team handoff for the customer-assisted/manual refund schema.

## Testing

- [`testing/auth-manual-test-cases.md`](testing/auth-manual-test-cases.md)
- [`testing/otp-confirmation-bug.md`](testing/otp-confirmation-bug.md)
- [`testing/sepay-webhook-test-notes.md`](testing/sepay-webhook-test-notes.md)
- [`testing/manual-refund-api-test-guide.md`](testing/manual-refund-api-test-guide.md)

## Consolidated Reports and SQL Handoff

Use [`reports/README.md`](reports/README.md) as the single entry point for the
current `main_local` status, historical reports, database handoff notes,
development fixtures, and the canonical SQL link. Detailed reports remain
under `reports` for traceability and are not authoritative specifications.
