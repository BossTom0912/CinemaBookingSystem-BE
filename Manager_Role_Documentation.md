# Manager Role Documentation - CinemaSystem Backend

## Overview
This document provides comprehensive information about the Manager role implementation in the CinemaSystem backend application (branch: Tom/integration-main-manager-admin-test).

---

## 1. Manager Role Definition

### Role Constants
**Location:** `CinemaSystem.Application/Common/AuthConstants.cs`

```csharp
public const string Manager = "MANAGER";
public const string Manager = "ROLE_MANAGER"; // RoleIds
```

The Manager role is defined as one of four core roles in the system:
- Customer
- Staff
- Manager
- Admin

---

## 2. Authorization Policies for Manager

### Policy Configuration
**Location:** `CinemaSystem/Program.cs`

Manager has access to the following authorization policies:

1. **CanManageMovie** - Manager, Admin
2. **CanManageCinemaRoomSeat** - Manager, Admin
3. **CanManageShowtime** - Manager, Admin
4. **CanManageFoodAndBeverage** - Staff, Manager, Admin
5. **CanManageVoucher** - Manager, Admin
6. **CanCancelShowtimeAndRefund** - Manager, Admin
7. **CanViewBranchDashboard** - Manager, Admin

### Policies NOT Available to Manager
- **CanViewSystemDashboard** - Admin only
- **CanManageUserAndRole** - Admin only
- **CanManageSystem** - Admin only

---

## 3. Cinema Scope Authorization

### Service
**Location:** `CinemaSystem.Infrastructure/Auth/CinemaScopeAuthorizationService.cs`

**Interface:** `CinemaSystem.Application/Interfaces/ICinemaScopeAuthorizationService.cs`

### Key Concept
Managers and Staff are **scoped to a specific cinema**. This is enforced through the `StaffProfile` entity which links a user to a cinema.

### How It Works
1. Manager users must have an active `StaffProfile` record
2. The `StaffProfile` contains a `CinemaId` field that defines their scope
3. When a Manager accesses cinema resources (rooms, seats, showtimes), the system checks:
   - If user is Admin: Full access to all cinemas
   - If user is Manager/Staff: Only access to their assigned cinema

### Authorization Methods
- `GetUserCinemaScopeAsync()` - Gets the cinema scope for the current user
- `AuthorizeCinemaAsync()` - Validates access to a specific cinema
- `AuthorizeRoomAsync()` - Validates access to a room (checks cinema ownership)
- `AuthorizeSeatAsync()` - Validates access to a seat (checks cinema ownership)
- `AuthorizeShowtimeAsync()` - Validates access to a showtime (checks cinema ownership)

---

## 4. Manager-Specific Controllers

### 4.1 ManagerDashboardController
**Location:** `CinemaSystem/Controllers/ManagerDashboardController.cs`

**Route:** `api/manager/dashboard`

**Policy:** `CanViewBranchDashboard`

**Functionality:**
- GET dashboard statistics for manager's cinema (or all cinemas if Admin)
- Filters by date range and optionally by movie
- Returns revenue, tickets, occupancy metrics

**Service:** `IManagerDashboardService`
**Implementation:** `CinemaSystem.Infrastructure/Dashboard/ManagerDashboardService.cs`

**Response Model:** `CinemaSystem.Contracts/Dashboard/ManagerDashboardResponse.cs`

**Dashboard Metrics:**
- GrossRevenue
- RefundedAmount
- PendingRefundAmount
- ManualRefundAmount
- NetRevenue
- GrossTicketsSold
- RefundedTickets
- NetTicketsSold
- SellableSeatCapacity
- OccupiedSeats
- OccupancyRate (percentage)

### 4.2 ManagerRefundsController
**Location:** `CinemaSystem/Controllers/ManagerRefundsController.cs`

**Route:** `api/manager/refunds`

**Policy:** `CanCancelShowtimeAndRefund`

**Functionality:**
- GET refunds for manager's cinema scope
- View refund requests and status

**Service:** `IRefundService`

### 4.3 ShowtimeCancellationsController
**Location:** `CinemaSystem/Controllers/ShowtimeCancellationsController.cs`

**Route:** `api/manager/showtimes`

**Policy:** `CanCancelShowtimeAndRefund`

**Functionality:**
- POST `{showtimeId}/cancel` - Cancel a showtime and trigger refunds
- Must be within manager's cinema scope

**Service:** `IShowtimeCancellationService`

---

## 5. Shared Controllers with Manager Access

### 5.1 MoviesController
**Location:** `CinemaSystem/Controllers/MoviesController.cs`

**Manager Endpoints:**
- POST `/api/movies` - Create movie (Admin, Manager)
- PUT `/api/movies/{movieId}` - Update movie (Admin, Manager)
- DELETE `/api/movies/{movieId}` - Delete movie (Admin, Manager)

### 5.2 ShowtimesController
**Location:** `CinemaSystem/Controllers/ShowtimesController.cs`

**Manager Endpoints:**
- POST `/api/showtimes` - Create showtime (Admin, Manager)
- PUT `/api/showtimes/{showtimeId}` - Update showtime (Admin, Manager)
- Cinema scope is enforced via room authorization

### 5.3 RoomsController
**Location:** `CinemaSystem/Controllers/RoomsController.cs`

**Manager Endpoints:**
- GET `/api/cinemas/rooms` - List rooms (Admin, Manager, Staff)
- GET `/api/cinemas/rooms/{roomId}` - Get room details (Admin, Manager, Staff)
- POST `/api/cinemas/{cinemaId}/rooms` - Create room (Admin, Manager)
- PUT `/api/cinemas/rooms/{roomId}` - Update room (Admin, Manager)
- DELETE `/api/cinemas/rooms/{roomId}` - Delete room (Admin, Manager)
- POST `/api/cinemas/rooms/{roomId}/generate-seats` - Generate seats (Admin, Manager)

**Cinema Scope:** Enforced for Manager users

### 5.4 SeatsController
**Location:** `CinemaSystem/Controllers/SeatsController.cs`

**Manager Endpoints:**
- GET `/api/seats` - List seats (Manager, Admin)
- GET `/api/seats/{seatId}` - Get seat details (Manager, Admin)
- POST `/api/seats` - Create seat (Manager, Admin)
- PUT `/api/seats/{seatId}` - Update seat (Manager, Admin)
- DELETE `/api/seats/{seatId}` - Delete seat (Manager, Admin)

**Cinema Scope:** Enforced via room ownership

---

## 6. Data Models

### 6.1 StaffProfile Entity
**Location:** `CinemaSystem.Domain/Entities/StaffProfile.cs`

```
- StaffProfileId (string, PK)
- UserId (string, FK to User)
- CinemaId (string, FK to Cinema)
- Position (string)
- HireDate (DateOnly?)
- EmploymentStatus (string)
- DateOfBirth (DateOnly?)
```

**Key Field:** `CinemaId` - Defines the cinema scope for the Manager/Staff user

**Employment Status Values:**
- ACTIVE
- INACTIVE

**Note:** Only ACTIVE staff profiles grant cinema scope access

### 6.2 Manager Dashboard Models
**Location:** `CinemaSystem.Contracts/Dashboard/`

- `ManagerDashboardQueryRequest.cs` - Query parameters (From, To, MovieId)
- `ManagerDashboardResponse.cs` - Response with metrics

---

## 7. Business Logic Summary

### Manager Capabilities
1. **Movie Management:** Create, update, delete movies
2. **Showtime Management:** Create, update, cancel showtimes within their cinema
3. **Room Management:** Create, update, delete rooms in their cinema
4. **Seat Management:** Create, update, delete seats in their cinema's rooms
5. **Dashboard Analytics:** View cinema-specific performance metrics
6. **Refund Management:** View and process refunds for their cinema
7. **Showtime Cancellation:** Cancel showtimes and trigger automatic refunds

### Scope Restrictions
- Managers can ONLY manage resources (rooms, seats, showtimes) within their assigned cinema
- Admin users bypass cinema scope and can manage all cinemas
- Staff users have similar scope restrictions but fewer management permissions

### Security Features
- Cinema scope is validated on every protected endpoint
- Manager must have an ACTIVE StaffProfile with a valid CinemaId
- Authorization failures return 403 Forbidden with clear error messages
- All manager actions are auditable (userId tracked in cancellations, etc.)

---

## 8. File Structure Summary

### Application Layer
- `CinemaSystem.Application/Interfaces/IManagerDashboardService.cs`
- `CinemaSystem.Application/Interfaces/ICinemaScopeAuthorizationService.cs`
- `CinemaSystem.Application/Common/AuthConstants.cs`

### Infrastructure Layer
- `CinemaSystem.Infrastructure/Dashboard/ManagerDashboardService.cs`
- `CinemaSystem.Infrastructure/Auth/CinemaScopeAuthorizationService.cs`

### API Layer (Controllers)
- `CinemaSystem/Controllers/ManagerDashboardController.cs`
- `CinemaSystem/Controllers/ManagerRefundsController.cs`
- `CinemaSystem/Controllers/ShowtimeCancellationsController.cs`
- `CinemaSystem/Controllers/MoviesController.cs` (shared)
- `CinemaSystem/Controllers/ShowtimesController.cs` (shared)
- `CinemaSystem/Controllers/RoomsController.cs` (shared)
- `CinemaSystem/Controllers/SeatsController.cs` (shared)

### Contracts (DTOs)
- `CinemaSystem.Contracts/Dashboard/ManagerDashboardResponse.cs`
- `CinemaSystem.Contracts/Dashboard/ManagerDashboardQueryRequest.cs`
- `CinemaSystem.Contracts/Refunds/RefundResponse.cs`
- `CinemaSystem.Contracts/Showtimes/CancelShowtimeRequest.cs`

### Domain Layer
- `CinemaSystem.Domain/Entities/StaffProfile.cs`
- `CinemaSystem.Domain/Entities/Role.cs`
- `CinemaSystem.Domain/Entities/User.cs`

---

## 9. Testing

### Test Files Covering Manager Role
**Location:** `CinemaSystem.Tests/`

- `ManagerDashboardApiIntegrationTests.cs`
- `ManagerCinemaScopeApiIntegrationTests.cs`
- `ShowtimeCancellationApiIntegrationTests.cs`
- `RoomShowtimeApiIntegrationTests.cs`
- `SeatCrudApiIntegrationTests.cs`

---

## 10. API Endpoint Summary

### Manager-Only Endpoints
| Method | Endpoint | Policy | Description |
|--------|----------|--------|-------------|
| GET | /api/manager/dashboard | CanViewBranchDashboard | Get cinema dashboard |
| GET | /api/manager/refunds | CanCancelShowtimeAndRefund | List refunds |
| POST | /api/manager/showtimes/{id}/cancel | CanCancelShowtimeAndRefund | Cancel showtime |

### Shared Manager Endpoints (Manager + Admin)
| Method | Endpoint | Roles | Description |
|--------|----------|-------|-------------|
| POST | /api/movies | Manager, Admin | Create movie |
| PUT | /api/movies/{id} | Manager, Admin | Update movie |
| DELETE | /api/movies/{id} | Manager, Admin | Delete movie |
| POST | /api/showtimes | Manager, Admin | Create showtime |
| PUT | /api/showtimes/{id} | Manager, Admin | Update showtime |
| POST | /api/cinemas/{id}/rooms | Manager, Admin | Create room |
| PUT | /api/cinemas/rooms/{id} | Manager, Admin | Update room |
| DELETE | /api/cinemas/rooms/{id} | Manager, Admin | Delete room |
| POST | /api/cinemas/rooms/{id}/generate-seats | Manager, Admin | Generate seats |
| POST | /api/seats | Manager, Admin | Create seat |
| PUT | /api/seats/{id} | Manager, Admin | Update seat |
| DELETE | /api/seats/{id} | Manager, Admin | Delete seat |

---

## 11. Configuration

Manager role requires no special configuration beyond standard JWT authentication and role-based authorization already configured in `Program.cs`.

### Required Database Setup
- Manager user must exist in `USER` table with role = 'ROLE_MANAGER'
- Manager must have an active record in `STAFF_PROFILE` table with:
  - Valid `CinemaId`
  - `EmploymentStatus` = 'ACTIVE'

---

## Document Information
- **Branch:** Tom/integration-main-manager-admin-test
- **Generated:** 2026-07-02
- **Author:** Codex AI Agent
- **Version:** 1.0

