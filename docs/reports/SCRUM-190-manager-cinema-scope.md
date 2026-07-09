# SCRUM-190 - Manager Cinema Scope

## Task

Implement cinema-level access control for Manager accounts.

Scope rule:

- One Manager manages exactly one cinema.
- Manager cinema scope is read from `STAFF_PROFILE.cinemaId`.
- Manager can access only resources that belong to their assigned cinema.
- Admin bypasses cinema scope and can access all cinemas.
- Manager or Staff without an active `STAFF_PROFILE` is rejected.

## Code Changes

Added shared cinema-scope authorization:

- `CinemaSystem.Application/Common/CinemaScopeAuthorizationResult.cs`
- `CinemaSystem.Application/Interfaces/ICinemaScopeAuthorizationService.cs`
- `CinemaSystem.Infrastructure/Auth/CinemaScopeAuthorizationService.cs`

The service resolves target cinema by:

- direct `cinemaId`
- `roomId -> ROOM.cinemaId`
- `seatId -> SEAT -> ROOM.cinemaId`
- `showtimeId -> SHOWTIME -> ROOM.cinemaId`

Applied scope checks to:

- `RoomsController`
  - `GET /api/rooms/rooms`: Manager/Staff only receive rooms in their cinema.
  - `GET /api/rooms/rooms/{roomId}`: forbidden if room is outside scope.
  - `POST /api/rooms/cinemas/{cinemaId}/rooms`: forbidden if cinema is outside scope.
  - `PUT /api/rooms/rooms/{roomId}`: forbidden if room is outside scope.
  - `DELETE /api/rooms/rooms/{roomId}`: forbidden if room is outside scope.
  - `POST /api/rooms/{roomId}/generate-seats`: forbidden if room is outside scope.
- `SeatsController`
  - `POST /api/seats`: checks request `roomId`.
  - `PUT /api/seats/{seatId}`: checks seat's room cinema.
  - `DELETE /api/seats/{seatId}`: checks seat's room cinema.
  - `GET /api/seats/room/{roomId}`: checks room cinema.
  - `GET /api/seats`: filters the paged query by the authenticated Manager's cinema.
  - `GET /api/seats/{seatId}`: checks the seat's cinema before returning details.
- `ShowtimesController`
  - `POST /api/showtimes`: checks request `roomId`.
  - `PUT /api/showtimes/{showtimeId}`: checks both current showtime cinema and target request room cinema.
  - `DELETE /api/showtimes/{showtimeId}`: checks showtime cinema.

Updated `RoomService.GetRoomsAsync` to accept an optional cinema scope filter.

## Error Behavior

- Resource does not exist: keeps existing `404` style response.
- Resource exists but belongs to another cinema: returns `403 Forbidden`.
- Error code for out-of-scope access: `CINEMA_SCOPE_FORBIDDEN`.
- Active staff profile missing: returns `403` with `STAFF_PROFILE_SCOPE_NOT_FOUND`.

## Tests Added / Updated

Added `ManagerCinemaScopeApiIntegrationTests`:

- Manager assigned to cinema A lists rooms and only sees cinema A rooms.
- Manager assigned to cinema A cannot create a room in cinema B.
- Admin can create a room in cinema B.
- Manager assigned to cinema A cannot create a showtime in a cinema B room.
- Manager assigned to cinema A cannot update a cinema A showtime to a cinema B room.
- Manager assigned to cinema A cannot delete a cinema B showtime.

Scope-gap regression coverage added on 2026-07-05:

- Manager assigned to cinema A lists seats and receives only cinema A seats.
- Manager assigned to cinema A cannot read a cinema B seat by ID.
- Admin still bypasses cinema scope and can list seats from both cinemas.

Updated existing integration tests to seed active `StaffProfile` rows for Manager/Staff tokens.

Updated controller mock tests to include an allow-all cinema scope mock where the test target is controller response mapping, not authorization.

## Test Results

Commands run from repository root:

```powershell
dotnet build CinemaSystem.sln
dotnet test CinemaSystem.sln --no-build
```

Results:

- Build: passed.
- Tests: passed.
- Test count: 245 passed, 0 failed, 0 skipped.

Build warnings:

- Existing nullable warnings in test files remained; no new build errors.

## Scope-Gap Fix Verification - 2026-07-05

- Targeted `ManagerCinemaScopeApiIntegrationTests`: 9 passed.
- Full solution: 247 passed, 0 failed, 0 skipped.
- Build: 0 warnings, 0 errors.
