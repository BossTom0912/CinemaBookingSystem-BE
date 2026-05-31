1. Guest

Guest → System:

Movie, cinema, and showtime browsing request
Customer account registration information
Login credentials
Email verification OTP
System → Guest:

Public movie, cinema, and showtime information
Registration result
Login result
Email verification result
Error messages for invalid registration, login, or verification
Note: Guest can only browse public information, register, log in, and verify email. Guest cannot book tickets.

2. Customer

Customer → System:

Profile update request
Movie, showtime, and seat map request
Temporary seat lock request
Booking information
F&B selection
Voucher code / reward point usage request
Booking payment request
Booking, ticket, and reward point history request
Movie review/rating submission
System → Customer:

Customer profile information
Movie list, showtime list, and real-time seat map
Seat lock status
Booking price summary
Voucher/reward point application result
Booking status
Payment status
E-ticket / QR code
Booking, ticket, and reward point history
Showtime cancellation / refund notification
Review/rating submission result
Note: Customer is the main actor for online ticket booking.

3. Staff

Staff → System:

QR ticket scan request
Manual ticket code validation request
Ticket check-in request
Counter ticket sale request
F&B order handling confirmation, if applicable
System → Staff:

Ticket validation result: valid, used, cancelled, invalid
Ticket, booking, showtime, room, and seat information
Check-in result
Counter sale result
F&B order information, if applicable
Note: Staff should not manage movies, rooms, seats, or showtimes. Staff mainly handles ticket validation, check-in, counter sales, and operational support.

4. Manager

Manager → System:

Branch/cinema dashboard request
Revenue report request
Booking statistics request
Payment/refund monitoring request
Showtime cancellation request
Showtime operation management request within assigned cinema scope
System → Manager:

Branch/cinema dashboard
Revenue report
Booking, payment, and refund statistics
Showtime cancellation result
Automatic/manual refund status
Cinema operation data within assigned scope
Note: Manager manages cinema operations and reports within assigned cinema or branch scope.

5. Admin

Admin → System:

User and role management request
Account ban/unban request
Movie management request
Cinema, room, and seat management request
Showtime management request
Voucher/promotion management request
F&B menu management request
Payment provider configuration request
System dashboard and audit log request
System → Admin:

User, role, and account status information
User/role update result
Movie, cinema, room, seat, and showtime management result
Voucher/F&B management result
System-wide reports
Audit logs
System configuration result
Note: Admin has the highest privilege and manages system-wide master data, access control, and configuration.

6. Payment Gateway

Examples: VNPAY / MoMo

System → Payment Gateway:

Payment transaction request
Booking payment information
Payment amount
System transaction code
Refund request
Payment/refund reconciliation information
Payment Gateway → System:

Payment callback result
Payment status: success, failed, cancelled, expired
Provider transaction code
Refund callback result
Provider refund code
Payment/refund error information
Note: The system does not process money directly. It sends payment/refund requests to the gateway and receives callback results.

7. Email / Notification Service

System → Email/Notification Service:

Email verification OTP
Password reset OTP/link
Booking confirmation email
E-ticket / QR code email
Showtime cancellation notification
Refund notification
Account/booking status notification
Email/Notification Service → System:

Email/notification delivery result
Delivery success/failure status
Delivery error information
Note: Guest and Customer do not interact directly with the Email Service. The system requests the service to send emails or notifications.