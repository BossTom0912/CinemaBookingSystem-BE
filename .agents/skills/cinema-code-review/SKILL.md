---
name: cinema-code-review
description: Review CinemaSystem ASP.NET Core backend code for Clean Architecture, EF Core, SQL Server, authorization scope, SePay payment, seat locking, API consistency, and project-specific business rules. Use this skill when asked to review code, review git diff, inspect a branch, check hardcoded authorization, evaluate merge safety, or find backend bugs.
---

# CinemaSystem Code Review Skill

## Purpose

Use this skill to review CinemaSystem backend code safely and project-aware.

This skill is for review only.

Do not modify files unless the user explicitly asks to fix the issues.

## Required Context

Before reviewing, read:

- `AGENTS.md`
- `docs/ai/code-review.md`

When the change is feature-level, also inspect:

- `docs/README.md`
- Related files under `docs/requirements/`
- Related files under `docs/architecture/`
- Related database schema under `docs/database/`

Do not guess documented business rules.

## Review Scope

Focus on:

1. Clean Architecture violations.
2. Controllers containing business logic.
3. Direct `DbContext` usage from API layer.
4. Incorrect role/policy usage.
5. Role claim mismatch.
6. Staff/Manager cinema scope violations.
7. Customer ownership violations.
8. Hardcoded role/status/string logic.
9. Hardcoded secrets or credentials.
10. EF Core query correctness.
11. N+1 query risks.
12. Missing transaction boundaries.
13. Seat-locking consistency.
14. SePay payment idempotency.
15. SePay webhook validation.
16. API response consistency.
17. DTO/request/response safety.
18. Missing validation.
19. Missing tests.
20. Unrelated changes outside requested scope.

## Project-Specific Rules

### Architecture

- `CinemaSystem` is API layer only.
- Controllers must stay thin.
- Application coordinates use cases.
- Infrastructure implements EF Core, SQL Server, JWT, SMTP, hashing, SePay, and external services.
- Contracts contain DTOs and API response models.
- Domain must not depend on infrastructure or HTTP concerns.

### Authorization

- Public registration may only create Customer accounts.
- Staff, Manager, and Admin must not be created through public registration.
- Customer can only access their own data.
- Staff can only operate within assigned cinema.
- Manager can only manage assigned cinema.
- Admin can access global system data when allowed.

### Role Consistency

Before judging or changing authorization, verify what role value the JWT actually emits.

Possible values may include:

- `Customer`, `Staff`, `Manager`, `Admin`
- `CUSTOMER`, `STAFF`, `MANAGER`, `ADMIN`
- `ROLE_CUSTOMER`, `ROLE_STAFF`, `ROLE_MANAGER`, `ROLE_ADMIN`

Policies must match JWT role claim values.

### Cinema Scope

Staff and Manager scope must be resolved from server-side profile data.

Do not trust request body `cinemaId` as permission proof.

### Security

Never approve code that:

- Hardcodes real secrets.
- Logs passwords, OTPs, JWTs, refresh tokens, SMTP passwords, payment secrets, or connection strings.
- Returns OTP in API response.
- Returns password hash.
- Stores plain text password.
- Accepts weak or missing JWT/payment secrets silently.

### SePay Payment

Payment processing must be safe.

Check:

- Webhook secret/signature validation.
- Idempotency.
- Duplicate webhook handling.
- Amount validation.
- Transaction reference validation.
- Safe status transition.
- No sensitive payment logging.

### Seat Locking

Check:

- Server-side seat availability.
- Lock expiration.
- Lock ownership.
- Booked seat protection.
- Concurrent request safety.
- UTC time handling.
- Safe state transition.

## Review Procedure

When asked to review current changes:

1. Inspect `git status`.
2. Inspect `git diff --stat`.
3. Inspect the relevant diff.
4. Inspect related existing files.
5. Inspect related docs if behavior is feature-level.
6. Produce a review report only.

When asked to review against a branch:

1. Identify current branch.
2. Compare against the requested target branch.
3. Review only meaningful changed files.
4. Avoid commenting on unrelated pre-existing code unless the change makes it worse.

When asked to review specific files:

1. Inspect those files.
2. Inspect dependencies used by those files.
3. Review the requested scope only.

## Output Format

Always use this exact structure:

```md
### Summary

Write a short merge-safety summary.

### Critical Issues

- File:
- Problem:
- Impact:
- Suggested Fix:

### Major Issues

- File:
- Problem:
- Impact:
- Suggested Fix:

### Minor Issues

- File:
- Problem:
- Suggested Fix:

### Missing Tests

- Test:

### Final Verdict

APPROVE / APPROVE WITH MINOR COMMENTS / REQUEST CHANGES
```

If there are no issues in a section, write:

```md
No issues found.
```

## Verdict Rules

Use `APPROVE` only when the code is safe to merge and no important issue remains.

Use `APPROVE WITH MINOR COMMENTS` when the code is safe but has minor cleanup or low-risk missing tests.

Use `REQUEST CHANGES` when there is any critical or major issue.

## Important Constraint

Do not praise vaguely.

Do not say code is good without explaining why.

Do not rewrite code during review.

Do not create files during review.

Do not run destructive commands.

Do not modify database schema unless explicitly requested.
