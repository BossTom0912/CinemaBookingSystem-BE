namespace CinemaSystem.Domain.Constants;

public static class FbConstants
{
    public static class ItemStatus
    {
        public const string Available = "AVAILABLE";
        public const string Unavailable = "UNAVAILABLE";
        public const string Inactive = "INACTIVE";
    }

    public static class FulfillmentStatus
    {
        public const string NotRequired = "NOT_REQUIRED";
        public const string Pending = "PENDING";
        public const string Preparing = "PREPARING";
        public const string Fulfilled = "FULFILLED";
        public const string Cancelled = "CANCELLED";
    }

    public static class Channel
    {
        public const string Counter = "COUNTER";
        public const string Online = "ONLINE";
    }

    public static class ErrorCodes
    {
        public const string InvalidPrice = "INVALID_PRICE";
        public const string NotFound = "NOT_FOUND";
        public const string ForbiddenCinemaBranch = "FORBIDDEN_CINEMA_BRANCH";
        public const string ItemUnavailable = "ITEM_UNAVAILABLE";
        public const string InsufficientStock = "INSUFFICIENT_STOCK";
        public const string EmptyOrder = "EMPTY_ORDER";
        public const string AlreadyFulfilled = "ALREADY_FULFILLED";
        public const string BookingCancelled = "BOOKING_CANCELLED";
        public const string NoFbItems = "NO_FB_ITEMS";
        public const string FoodSafetyRestockDenied = "FOOD_SAFETY_RESTOCK_DENIED";
        public const string WrongCinemaBranch = "WRONG_CINEMA_BRANCH";
        public const string InternalError = "INTERNAL_ERROR";
    }

    public static class Messages
    {
        public const string ItemCreatedSuccess = "F&B item created successfully.";
        public const string ItemUpdatedSuccess = "F&B item updated successfully.";
        public const string ItemDeactivatedSuccess = "F&B item deactivated successfully.";
        public const string InventoryUpdatedSuccess = "Cinema inventory updated successfully.";
        public const string OrderPlacedSuccess = "Counter F&B order placed and fulfilled successfully.";
        public const string OrderCompleted = "Order completed.";
        public const string CounterOrderProcessingFailed = "Unable to process the counter F&B order. Please try again.";
        public const string FulfillmentCompleted = "F&B items successfully handed over to customer.";
        public const string RestockSuccess = "Restocked F&B items successfully.";

        public const string PriceNonNegative = "Price must be non-negative.";
        public const string ItemNotFound = "F&B item not found.";
        public const string CinemaBranchNotFound = "Cinema branch not found.";
        public const string BookingNotFound = "Booking not found.";
        public const string EmptyOrderError = "Order must contain at least one F&B item.";
        public const string ForbiddenBranchInventory = "Unauthorized to adjust inventory for a cinema branch other than your assigned location.";
        public const string ForbiddenBranchOrder = "Staff can only process orders at their assigned cinema branch.";
        public const string NoFbItemsError = "This booking does not contain any F&B items.";
        public const string BookingCancelledError = "Cannot fulfill F&B items for a cancelled booking.";
        public const string FoodSafetyRestockDenied = "Fulfilled F&B items cannot be restocked due to food safety regulations.";
    }
}
