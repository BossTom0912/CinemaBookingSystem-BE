# Hardcode Audit - 2026-07-03

## Scope

- Production projects: API, Application, Domain, Infrastructure, and Contracts.
- Primary focus: showtime cancellation and refund workflows.
- Baseline branch: `Tom/remove-hardcodes` at `01b8e9c`.

## Sources reviewed

- `docs/requirements/srs-group-2.docx`
- `docs/requirements/business-rules.docx`
- `docs/architecture/backend-system-design-clean-architecture.docx`
- `docs/architecture/conceptual-erd-explanation.docx`
- `docs/database/cinema-booking-schema.sql`

The refund decisions remain aligned with BR-38 through BR-41, BR-60, BR-75,
BR-79, BR-82, and BR-84 through BR-92.

## Changes made

1. Moved refund error-code values into `DomainConstants.RefundErrorCode`.
   `BookingConstants.RefundErrorCodes` remains as an Application compatibility
   facade and now references Domain constants.
2. Moved the staff position and the default administrative cancellation reason
   into Domain constants.
3. Centralized the custom JWT claim name as `AuthConstants.Claims.UserId` and
   replaced repeated `"userId"` literals in production authentication consumers.
4. Moved refund and cancellation email subjects/bodies out of Infrastructure
   services into `EmailTemplatesSettings`.
5. Added the new email template keys to
   `CinemaSystem/appsettings.Development.example.json`.
6. Removed the `"string"`/`"user"` sentinel check and arbitrary active-Admin
   fallback from legacy showtime cancellation. Cancellation now requires the
   authenticated actor ID, preventing audit records from being attributed to an
   unrelated administrator.

## Literals intentionally retained

### EF Core database mapping

`CinemaDbContext` still contains table names, column names, index names, SQL
filters, and database default values such as `ONLINE`, `CREATED`, `ACTIVE`, and
`PENDING`. These values are schema metadata generated from the database-first
model. Replacing them with application constants would couple scaffolded mapping
to application policy and make future re-scaffolding harder. They should remain
literal unless the project moves to hand-maintained Fluent API configurations.

### API routes and protocol identifiers

Controller route templates, Swagger security identifiers, media types, and
external-provider field names are protocol metadata. They must be compile-time
constants or attributes and are not runtime business configuration.

### Validation thresholds and HTTP semantics

Zero/one boundary checks, `HttpStatusCode`, string length attributes, decimal
precision, and pagination arithmetic express validation or protocol semantics.
They should not be moved to Domain unless the business rule requires them to be
changed independently at runtime.

### User-facing API messages and log templates

Response and structured-log messages remain close to the branch that produces
them. Moving every unique sentence into Domain would turn Domain into a message
catalog without reducing behavior drift. If localization is introduced, move
these messages to resource files (`.resx`) rather than constants.

### Gemini moderation prompt

The prompt explicitly lists `APPROVED`, `REJECTED`, and `FLAGGED` because those
exact tokens are the external AI response grammar. The service validates the
returned status through `ReviewConstants`; the prompt examples remain readable
protocol instructions.

## Recommended placement rule

- Domain constants: statuses, actions, entity types, ID prefixes, invariant
  business codes, and fixed business reasons.
- Options/appsettings: URLs, time windows, limits, provider settings, file paths,
  email templates, and other environment-dependent values.
- Contracts: request validation bounds and wire-format constraints.
- Infrastructure mapping: SQL/table/column/index/default literals required to
  match the database schema.
- Resource files: localized response, notification, and UI-facing text.

## Verification

- `CinemaSystem/appsettings.Development.example.json` parsed successfully.
- `dotnet build CinemaSystem.sln --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test CinemaSystem.sln --no-build`: passed, 231/231 tests.
- `git diff --check`: passed; only line-ending conversion notices were reported.
