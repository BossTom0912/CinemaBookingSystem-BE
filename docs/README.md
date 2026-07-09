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
- [`api/examples/create-order.json`](api/examples/create-order.json)
- [`api/examples/seat-map.json`](api/examples/seat-map.json)

## Database

- [`database/cinema-booking-schema.sql`](database/cinema-booking-schema.sql):
  complete local database creation script.
- [`database/SCRUM-198-ticket-scan-db-changes.md`](database/SCRUM-198-ticket-scan-db-changes.md):
  team handoff and deployment notes for ticket-scan actor auditing.
- [`database/SCRUM-198-ticket-scan-patch.sql`](database/SCRUM-198-ticket-scan-patch.sql):
  idempotent patch for existing databases.
- [`database/SCRUM-193-customer-assisted-refund-db-changes.md`](database/SCRUM-193-customer-assisted-refund-db-changes.md):
  team handoff for the customer-assisted/manual refund schema.
- [`database/SCRUM-193-customer-assisted-refund-patch.sql`](database/SCRUM-193-customer-assisted-refund-patch.sql):
  idempotent patch for refund claim, customer request, and manual processing tables.

## Testing

- [`testing/auth-manual-test-cases.md`](testing/auth-manual-test-cases.md)
- [`testing/otp-confirmation-bug.md`](testing/otp-confirmation-bug.md)
- [`testing/sepay-webhook-test-notes.md`](testing/sepay-webhook-test-notes.md)
- [`testing/manual-refund-api-test-guide.md`](testing/manual-refund-api-test-guide.md)

## Historical Reports

Files under [`reports`](reports) describe completed implementation work. They
are retained for traceability but are not authoritative specifications.

- [`reports/customer-flow-movie-view-booking.md`](reports/customer-flow-movie-view-booking.md)
- [`reports/forgot-password-implementation.md`](reports/forgot-password-implementation.md)
- [`reports/scrum-157-checkout-implementation-plan.md`](reports/scrum-157-checkout-implementation-plan.md)
- [`reports/sprint-1-auth-implementation.md`](reports/sprint-1-auth-implementation.md)
- [`reports/main-manager-admin-integration-test-2026-06-28.md`](reports/main-manager-admin-integration-test-2026-06-28.md):
  merge-conflict decisions, integrated feature/database scope, and build/test
  evidence for `main` + `MangerAndAdmin_1`.
- [`reports/SCRUM-193-customer-assisted-refund.md`](reports/SCRUM-193-customer-assisted-refund.md)
- [`reports/SCRUM-198-ticket-scan.md`](reports/SCRUM-198-ticket-scan.md)
- [`reports/MangerAndAdmin_1-admin-merge-report.md`](reports/MangerAndAdmin_1-admin-merge-report.md)
