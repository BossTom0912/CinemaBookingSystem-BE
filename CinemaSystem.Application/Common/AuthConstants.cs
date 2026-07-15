namespace CinemaSystem.Application.Common;

public static class AuthConstants
{
    public static class Claims
    {
        public const string UserId = "userId";
    }

    public static class Roles
    {
        public const string Customer = "CUSTOMER";
        public const string Staff = "STAFF";
        public const string Manager = "MANAGER";
        public const string Admin = "ADMIN";

        public static string Normalize(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return string.Empty;
            }

            return role.Trim().ToUpperInvariant() switch
            {
                Customer or "ROLE_CUSTOMER" => Customer,
                Staff or "ROLE_STAFF" => Staff,
                Manager or "ROLE_MANAGER" => Manager,
                Admin or "ROLE_ADMIN" => Admin,
                var normalizedRole => normalizedRole
            };
        }
    }

    public static class RoleIds
    {
        public const string Customer = "ROLE_CUSTOMER";
        public const string Staff = "ROLE_STAFF";
        public const string Manager = "ROLE_MANAGER";
        public const string Admin = "ROLE_ADMIN";
    }

    public static class Policies
    {
        public const string CanViewMoviesAndShowtimes = "CanViewMoviesAndShowtimes";
        public const string CanRegisterOrLogin = "CanRegisterOrLogin";
        public const string CanBookTicket = "CanBookTicket";
        public const string CanSelectSeat = "CanSelectSeat";
        public const string CanBuyFoodAndBeverageInCheckout = "CanBuyFoodAndBeverageInCheckout";
        public const string CanApplyVoucher = "CanApplyVoucher";
        public const string CanPayOnline = "CanPayOnline";
        public const string CanViewBookingHistory = "CanViewBookingHistory";
        public const string CanReviewAndFeedback = "CanReviewAndFeedback";
        public const string CanScanTicket = "CanScanTicket";
        public const string CanManageMovie = "CanManageMovie";
        public const string CanManageCinemaRoomSeat = "CanManageCinemaRoomSeat";
        public const string CanManageShowtime = "CanManageShowtime";
        public const string CanManageFoodAndBeverage = "CanManageFoodAndBeverage";
        public const string CanManageVoucher = "CanManageVoucher";
        public const string CanCancelShowtimeAndRefund = "CanCancelShowtimeAndRefund";
        public const string CanViewBranchDashboard = "CanViewBranchDashboard";
        public const string CanViewStaffShiftReport = "CanViewStaffShiftReport";
        public const string CanViewSystemDashboard = "CanViewSystemDashboard";
        public const string CanManageUserAndRole = "CanManageUserAndRole";
        public const string CanManageSystem = "CanManageSystem";
    }

    public static class UserStatus
    {
        public const string PendingVerification = "PENDING_VERIFICATION";
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Banned = "BANNED";
    }
}
