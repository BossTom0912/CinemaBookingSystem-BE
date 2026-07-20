using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CinemaSystem.Infrastructure.Configuration;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CinemaSystem.Application.Common;
using CinemaSystem.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Use case tạo giao dịch SePay và chốt trạng thái payment/booking/ticket.
/// </summary>
/// <remarks>
/// Được gọi từ PaymentController khi tạo payment và từ PaymentWebhookService
/// khi ngân hàng callback. Class đọc/ghi PAYMENT, BOOKING, BOOKING_SEAT,
/// SHOWTIME_SEAT, TICKET và REFUND qua CinemaDbContext; kết quả quay về caller.
/// </remarks>
public class PaymentService : IPaymentService
{
    // Khai báo biến DbContext để tương tác với cơ sở dữ liệu
    private readonly CinemaDbContext _db;
    // Khai báo biến lưu trữ cấu hình SePay
    private readonly SepaySettings _sepaySettings;
    private readonly BookingSettings _bookingSettings;
    private readonly IRefundClaimIssuer _refundClaimIssuer;
    private readonly IEmailSender _emailSender;
    private readonly RefundSettings _refundSettings;
    private readonly IClock _clock;
    private readonly ILogger<PaymentService> _logger;
    // Biểu thức chính quy (Regex) để trích xuất mã giao dịch (Bắt đầu bằng chữ T và theo sau là 10 ký tự chữ/số)
    private static readonly Regex TransactionCodeRegex = new(
        DomainConstants.PaymentTransactionCode.Pattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Phương thức khởi tạo (Constructor) tiêm các dependency cần thiết
    public PaymentService(
        CinemaDbContext db,
        IOptions<SepaySettings> sepayOptions,
        IOptions<BookingSettings> bookingOptions,
        IRefundClaimIssuer refundClaimIssuer,
        IEmailSender emailSender,
        IOptions<RefundSettings> refundOptions,
        IClock clock,
        ILogger<PaymentService> logger)
    {
        // Gán DbContext được tiêm vào biến private
        _db = db;
        // Lấy giá trị cấu hình SePay từ IOptions và gán vào biến private
        _sepaySettings = sepayOptions.Value;
        _bookingSettings = bookingOptions.Value;
        _refundClaimIssuer = refundClaimIssuer;
        _emailSender = emailSender;
        _refundSettings = refundOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    // Phương thức tạo bản ghi thanh toán cho một đặt vé và trả về thông tin ngân hàng kèm mã giao dịch
    public async Task<CreatePaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Loại bỏ khoảng trắng thừa ở hai đầu của ID đặt vé
        var bookingId = request.BookingId.Trim();
        // Loại bỏ khoảng trắng thừa ở hai đầu của ID nhà cung cấp thanh toán
        var paymentProviderId = request.PaymentProviderId.Trim();
        // Loại bỏ khoảng trắng thừa ở hai đầu của ID người dùng (nếu có)
        var normalizedUserId = userId?.Trim();

        // Kiểm tra nếu ID đặt vé bị trống thì ném ra ngoại lệ ArgumentException
        if (string.IsNullOrWhiteSpace(bookingId))
            throw new ArgumentException("BookingId must be provided.", nameof(request));
        // Kiểm tra nếu ID nhà cung cấp thanh toán bị trống thì ném ra ngoại lệ ArgumentException
        if (string.IsNullOrWhiteSpace(paymentProviderId))
            throw new ArgumentException("PaymentProviderId must be provided.", nameof(request));
        // Kiểm tra nếu ID người dùng bị trống thì ném ra ngoại lệ UnauthorizedAccessException (Không được phép)
        if (string.IsNullOrWhiteSpace(normalizedUserId))
            throw new UnauthorizedAccessException("Unauthorized.");

        // Truy vấn thông tin đặt vé từ cơ sở dữ liệu dựa trên ID đặt vé
        var booking = await _db.Bookings
            // Bao gồm danh sách các thanh toán liên quan đến đặt vé này
            .Include(item => item.Payments)
            // Bao gồm thông tin hồ sơ khách hàng của đặt vé này
            .Include(item => item.CustomerProfile)
            // Lấy ra bản ghi duy nhất khớp với điều kiện, nếu không có trả về null
            .SingleOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);
        // Nếu không tìm thấy đặt vé, ném ra ngoại lệ InvalidOperationException
        if (booking == null)
            throw new InvalidOperationException($"Booking {bookingId} not found.");

        // Kiểm tra xem khách hàng thực hiện thanh toán có phải là chủ sở hữu của vé hay không
        if (booking.CustomerProfile is null ||
            !string.Equals(booking.CustomerProfile.UserId, normalizedUserId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("You are not allowed to pay for this booking.");

        // Kiểm tra trạng thái của đặt vé có phải là "Chờ thanh toán" hay không
        if (!string.Equals(booking.BookingStatus, DomainConstants.EntityStatus.PendingPayment, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Booking {booking.BookingId} is not awaiting payment.");

        // Truy vấn thông tin nhà cung cấp thanh toán từ cơ sở dữ liệu
        var provider = await _db.PaymentProviders.SingleOrDefaultAsync(p => p.PaymentProviderId == paymentProviderId, cancellationToken);
        // Nếu không tìm thấy nhà cung cấp thanh toán, ném ra ngoại lệ
        if (provider == null)
            throw new InvalidOperationException($"Payment provider {paymentProviderId} not found.");
        // Kiểm tra xem nhà cung cấp thanh toán có đang hoạt động (Active) hay không
        if (!string.Equals(provider.ProviderStatus, DomainConstants.EntityStatus.Active, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Payment provider {paymentProviderId} is not active.");

        // Tìm kiếm xem đặt vé này đã có bất kỳ khoản thanh toán nào thành công chưa
        var successfulPayment = booking.Payments
            .FirstOrDefault(item => string.Equals(item.PaymentStatus, DomainConstants.PaymentStatus.Success, StringComparison.OrdinalIgnoreCase));
        // Nếu đã có thanh toán thành công, ném ra ngoại lệ báo lỗi vé đã được thanh toán
        if (successfulPayment != null)
            throw new InvalidOperationException($"Booking {booking.BookingId} has already been paid.");

        // Tính toán số tiền cần thanh toán dựa trên tổng tiền của đặt vé (có thể override trong môi trường dev)
        var paymentAmount = GetPaymentAmount(booking.TotalAmount);
        // Tìm kiếm xem có khoản thanh toán nào đang ở trạng thái "Chờ xử lý" (Pending) với cùng nhà cung cấp hay không
        var pendingPayment = booking.Payments
            .Where(item =>
                item.PaymentProviderId == provider.PaymentProviderId
                && string.Equals(item.PaymentStatus, DomainConstants.PaymentStatus.Pending, StringComparison.OrdinalIgnoreCase))
            // Sắp xếp giảm dần theo thời gian tạo để lấy giao dịch mới nhất
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        // Nếu đã có một giao dịch đang chờ thanh toán
        if (pendingPayment != null)
        {
            // Kiểm tra xem số tiền của giao dịch chờ có khớp với số tiền mới tính không
            if (pendingPayment.Amount != paymentAmount)
            {
                // Nếu không khớp, cập nhật lại số tiền cho giao dịch chờ
                pendingPayment.Amount = paymentAmount;
                // Lưu thay đổi vào cơ sở dữ liệu
                await _db.SaveChangesAsync(cancellationToken);
            }

            // Trả về thông tin thanh toán từ giao dịch pending đã có sẵn
            return ToCreatePaymentResponse(pendingPayment, booking.ExpiredAt);
        }

        // Lấy thời gian hiện tại chuẩn UTC
        var now = _clock.UtcNow;

        // Khởi tạo một đối tượng Thanh toán (Payment) mới
        var payment = new Payment
        {
            // Tạo ID tự động với tiền tố "PAY"
            PaymentId = GenerateId(DomainConstants.EntityIdPrefix.Payment),
            // Gán ID đặt vé
            BookingId = booking.BookingId,
            // Gán ID nhà cung cấp thanh toán
            PaymentProviderId = provider.PaymentProviderId,
            // Gán số tiền cần thanh toán
            Amount = paymentAmount,
            // Khởi tạo ngẫu nhiên một mã giao dịch (Transaction Code)
            TransactionCode = GenerateTransactionCode(),
            // Đặt trạng thái ban đầu là "Đang chờ" (Pending)
            PaymentStatus = DomainConstants.PaymentStatus.Pending,
            // Ghi nhận thời điểm tạo thanh toán
            CreatedAt = now,
            PaymentMethod = provider.ProviderName
        };

        // Gia hạn thêm thời gian hết hạn cho đặt vé (thêm 10 phút)
        booking.ExpiredAt = now.AddMinutes(_bookingSettings.PendingPaymentExpiryMinutes);
        // Thêm đối tượng Payment mới vào Entity Framework
        _db.Payments.Add(payment);
        // Lưu các thay đổi vào cơ sở dữ liệu
        await _db.SaveChangesAsync(cancellationToken);

        // Trả về đối tượng phản hồi tạo thanh toán
        return ToCreatePaymentResponse(payment, booking.ExpiredAt);
    }

    // Phương thức trợ giúp để chuyển đổi thực thể Payment sang đối tượng CreatePaymentResponse
    private CreatePaymentResponse ToCreatePaymentResponse(Payment payment, DateTime? expiresAt)
    {
        // Khởi tạo và trả về DTO chứa thông tin thanh toán
        return new CreatePaymentResponse
        {
            // Gán ID thanh toán
            PaymentId = payment.PaymentId,
            // Gán số tiền
            Amount = payment.Amount,
            // Gán mã giao dịch (nếu null thì dùng chuỗi rỗng)
            TransactionCode = payment.TransactionCode ?? string.Empty,
            // Lấy tên ngân hàng từ cấu hình SePay
            BankName = _sepaySettings.BankName,
            // Lấy số tài khoản ngân hàng từ cấu hình SePay
            BankAccount = _sepaySettings.BankAccount,
            // Gán thời gian hết hạn của thanh toán
            ExpiresAt = expiresAt
        };
    }

    // Phương thức lấy số tiền thanh toán (Hỗ trợ override cho môi trường dev)
    private decimal GetPaymentAmount(decimal bookingTotalAmount)
    {
        // Trả về số tiền ghi đè nếu lớn hơn 0, ngược lại trả về đúng tổng tiền đặt vé
        return _sepaySettings.DevelopmentPaymentAmountOverride is > 0
            ? _sepaySettings.DevelopmentPaymentAmountOverride.Value
            : bookingTotalAmount;
    }

    // Phương thức xác nhận thanh toán khi có webhook hoặc callback gửi về
    public async Task ConfirmPaymentAsync(
        string transactionContent,
        decimal amount,
        string? providerTransactionCode = null,
        string? rawCallbackPayload = null,
        CancellationToken cancellationToken = default)
    {
        // Kiểm tra nếu nội dung giao dịch bị trống
        if (string.IsNullOrWhiteSpace(transactionContent))
            throw new ArgumentException("Transaction content must be provided.", nameof(transactionContent));
        // Kiểm tra nếu số tiền thanh toán <= 0
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        // Trích xuất mã giao dịch từ nội dung chuyển khoản bằng Regex (VD: mã có dạng TXXXXXXXXXX)
        var match = TransactionCodeRegex.Match(transactionContent);
        // Nếu không khớp với định dạng mã giao dịch
        if (!match.Success)
            throw new InvalidOperationException("No transaction code found in the provided transaction content.");

        // Lấy mã giao dịch và chuyển sang in hoa
        var transactionCode = match.Value.ToUpperInvariant();

        // Tìm kiếm giao dịch trong cơ sở dữ liệu dựa trên mã giao dịch vừa trích xuất
        // SQL Server retry is enabled for transient connection failures. An explicit
        // transaction must therefore be created inside the execution strategy; this
        // makes the complete callback state transition retryable as one unit.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var payment = await _db.Payments
                .Include(p => p.Refunds)
                // Bao gồm thông tin Đặt vé (Booking)
                .Include(p => p.Booking)
                    // Bao gồm thông tin Suất chiếu (Showtime) của Booking đó
                    .ThenInclude(b => b.Showtime)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.VoucherUsage)
                        .ThenInclude(vu => vu!.Voucher)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.CustomerProfile)
                        .ThenInclude(c => c!.User)
                // Bao gồm nhánh BookingSeats (các ghế đã đặt)
                .Include(p => p.Booking)
                    // Bao gồm danh sách BookingSeats
                    .ThenInclude(b => b.BookingSeats)
                        // Bao gồm thông tin ShowtimeSeat (ghế của suất chiếu)
                        .ThenInclude(bs => bs.ShowtimeSeat)
                // Bao gồm nhánh Ticket (vé xem phim)
                .Include(p => p.Booking)
                    // Bao gồm danh sách BookingSeats
                    .ThenInclude(b => b.BookingSeats)
                        // Bao gồm thông tin vé (Ticket) được sinh ra cho ghế đó
                        .ThenInclude(bs => bs.Ticket)
                .AsSplitQuery()
                // Lấy ra bản ghi duy nhất khớp mã giao dịch
                .SingleOrDefaultAsync(
                    p => p.TransactionCode != null
                        && p.TransactionCode.ToUpper() == transactionCode,
                    cancellationToken);

            // Nếu không tìm thấy giao dịch tương ứng
            if (payment == null)
                throw new InvalidOperationException($"Payment with transaction code {transactionCode} not found.");

            // Xác thực số tiền gửi về có khớp với số tiền yêu cầu trong database hay không
            if (payment.Amount != amount)
                throw new InvalidOperationException($"Payment amount mismatch. Expected {payment.Amount} got {amount}.");

            // Nếu thanh toán này đã được xác nhận thành công từ trước thì kết thúc sớm (Idempotent)
            if (string.Equals(payment.PaymentStatus, DomainConstants.PaymentStatus.Success, StringComparison.OrdinalIgnoreCase))
                return;

            // Bắt đầu một Transaction mới để đảm bảo tính toàn vẹn dữ liệu
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Reload payment status from DB inside transaction to prevent race conditions from duplicate concurrent webhooks
                await _db.Entry(payment).ReloadAsync(cancellationToken);
                if (string.Equals(payment.PaymentStatus, DomainConstants.PaymentStatus.Success, StringComparison.OrdinalIgnoreCase))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return;
                }

                var now = _clock.UtcNow;
                // Cập nhật trạng thái thanh toán thành "Thành công" (Success)
                payment.PaymentStatus = DomainConstants.PaymentStatus.Success;
                // Cập nhật thời điểm thanh toán thành công
                payment.PaidAt = now;
                // Cập nhật thời gian chỉnh sửa mới nhất
                payment.UpdatedAt = now;
                // Ghi nhận mã giao dịch từ phía nhà cung cấp (nếu có)
                payment.ProviderTransactionCode = string.IsNullOrWhiteSpace(providerTransactionCode)
                    ? payment.ProviderTransactionCode
                    : providerTransactionCode.Trim();
                // Ghi nhận dữ liệu callback gốc để debug/kiểm tra sau này (nếu có)
                payment.RawCallbackPayload = string.IsNullOrWhiteSpace(rawCallbackPayload)
                    ? payment.RawCallbackPayload
                    : rawCallbackPayload;

                // Lấy thông tin Booking liên kết với Payment này
                var booking = payment.Booking ?? await _db.Bookings.SingleOrDefaultAsync(b => b.BookingId == payment.BookingId, cancellationToken);
                // Nếu không tìm thấy Booking, ném ra ngoại lệ
                if (booking == null)
                    throw new InvalidOperationException($"Booking {payment.BookingId} not found.");

                // Cập nhật trạng thái Booking thành "Đã thanh toán" (Paid) nếu đang ở trạng thái "Chờ thanh toán"
                if (string.Equals(booking.BookingStatus, DomainConstants.EntityStatus.PendingPayment, StringComparison.OrdinalIgnoreCase))
                {
                    booking.BookingStatus = DomainConstants.EntityStatus.Paid;

                    // Xác nhận sử dụng Voucher
                    if (booking.VoucherUsage != null && string.Equals(booking.VoucherUsage.UsageStatus, DomainConstants.VoucherUsageStatus.Applied, StringComparison.OrdinalIgnoreCase))
                    {
                        booking.VoucherUsage.UsageStatus = DomainConstants.VoucherUsageStatus.Confirmed;
                        booking.VoucherUsage.UsedAt = now;
                        if (booking.VoucherUsage.Voucher != null)
                        {
                            booking.VoucherUsage.Voucher.UsedCount += 1;
                        }
                    }
                }

                // Kiểm tra trường hợp suất chiếu đã bị hủy trong lúc người dùng đang chuyển khoản
                if (booking.Showtime != null && booking.Showtime.Status == DomainConstants.EntityStatus.Cancelled)
                {
                    await _db.Entry(booking.Showtime)
                        .Reference(showtime => showtime.Movie)
                        .LoadAsync(cancellationToken);
                    await _db.Entry(booking.Showtime)
                        .Reference(showtime => showtime.ShowtimeCancellation)
                        .LoadAsync(cancellationToken);

                    // Chuyển trạng thái Booking sang "Chờ hoàn tiền" (PendingRefund) thay vì hoàn tất vé
                    booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;

                    // Khởi tạo một đối tượng Hoàn tiền (Refund) mới
                    var refund = new Refund
                    {
                        // Tạo ID tự động cho giao dịch hoàn tiền
                        RefundId = GenerateId(DomainConstants.EntityIdPrefix.Refund),
                        // Gán ID đặt vé
                        BookingId = booking.BookingId,
                        // Gán ID thanh toán gốc
                        PaymentId = payment.PaymentId,
                        // Gán ID nhà cung cấp dịch vụ thanh toán
                        PaymentProviderId = payment.PaymentProviderId,
                        // Gán số tiền cần hoàn trả
                        RefundAmount = payment.Amount,
                        // Đặt trạng thái hoàn tiền là "Đang chờ" (Pending)
                        RefundStatus = DomainConstants.RefundStatus.Pending,
                        // Ghi nhận thời điểm yêu cầu hoàn tiền
                        RequestedAt = now,
                        // Ghi nhận lý do hoàn tiền (Thanh toán chậm cho một suất chiếu đã bị hủy)
                        RefundReason = "Late payment received for a cancelled showtime.",
                        ShowtimeCancellationId =
                            booking.Showtime.ShowtimeCancellation?.ShowtimeCancellationId
                    };
                    _db.Refunds.Add(refund);
                    var claimIssue = CreateRefundClaim(refund, booking, now);

                    // Lưu các thay đổi vào cơ sở dữ liệu
                    await _db.SaveChangesAsync(cancellationToken);
                    // Xác nhận lưu transaction
                    await tx.CommitAsync(cancellationToken);
                    await TrySendLatePaymentClaimEmailAsync(
                        booking,
                        claimIssue,
                        cancellationToken);
                    // Thoát sớm để không sinh ra vé
                    return;
                }

                // Kiểm tra trường hợp đặt vé đã bị hủy (ví dụ: hết hạn time-out) nhưng tiền lại đến chậm
                if (string.Equals(booking.BookingStatus, DomainConstants.EntityStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
                {
                    // Chuyển trạng thái Booking sang "Chờ hoàn tiền" (PendingRefund) để admin xử lý
                    booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;

                    // Khởi tạo một yêu cầu hoàn tiền mới
                    var refund = new Refund
                    {
                        // Tạo ID hoàn tiền
                        RefundId = GenerateId(DomainConstants.EntityIdPrefix.Refund),
                        // Map với ID đặt vé
                        BookingId = booking.BookingId,
                        // Map với ID thanh toán
                        PaymentId = payment.PaymentId,
                        // Map với ID nhà cung cấp
                        PaymentProviderId = payment.PaymentProviderId,
                        // Set số tiền cần hoàn
                        RefundAmount = payment.Amount,
                        // Trạng thái chờ hoàn tiền
                        RefundStatus = DomainConstants.RefundStatus.Pending,
                        // Thời điểm tạo yêu cầu hoàn tiền
                        RequestedAt = now,
                        // Lý do: Thanh toán trễ cho một đơn vé đã hết hạn/bị hủy
                        RefundReason = "Late payment received for an expired booking."
                    };
                    _db.Refunds.Add(refund);
                    var claimIssue = CreateRefundClaim(refund, booking, now);

                    // Lưu các thay đổi
                    await _db.SaveChangesAsync(cancellationToken);
                    // Xác nhận transaction
                    await tx.CommitAsync(cancellationToken);
                    await TrySendLatePaymentClaimEmailAsync(
                        booking,
                        claimIssue,
                        cancellationToken);
                    // Thoát sớm
                    return;
                }

                // Duyệt qua từng ghế đã đặt trong đơn hàng để cập nhật trạng thái và sinh vé
                foreach (var bookingSeat in booking.BookingSeats)
                {
                    // Chuyển trạng thái ghế của suất chiếu sang "Đã đặt" (Booked)
                    bookingSeat.ShowtimeSeat.SeatStatus = DomainConstants.EntityStatus.Booked;
                    // Gỡ bỏ thời gian khóa ghế (LockedUntil)
                    bookingSeat.ShowtimeSeat.LockedUntil = null;
                    // Gỡ bỏ thông tin người đang khóa ghế
                    bookingSeat.ShowtimeSeat.LockedByUserId = null;

                    // Kiểm tra nếu chưa tồn tại vé cho ghế này
                    if (bookingSeat.Ticket == null)
                    {
                        // Khởi tạo một đối tượng Vé (Ticket) mới
                        _db.Tickets.Add(new Ticket
                        {
                            // Sinh ID vé tự động
                            TicketId = GenerateId(DomainConstants.EntityIdPrefix.Ticket),
                            // Liên kết với bản ghi BookingSeat
                            BookingSeatId = bookingSeat.BookingSeatId,
                            // Sinh mã QR Code dùng để quét vé
                            QrCode = GenerateTicketQrCode(booking.BookingId, bookingSeat.BookingSeatId),
                            // Đặt trạng thái ban đầu của vé là "Chưa sử dụng" (Unused)
                            TicketStatus = DomainConstants.TicketStatus.Unused,
                            // Ghi nhận thời điểm phát hành vé
                            GeneratedAt = now
                        });
                    }
                }

                // Lưu tất cả các thay đổi vào DB
                await _db.SaveChangesAsync(cancellationToken);
                // Xác nhận thành công (Commit) transaction
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                // Nếu có bất kỳ lỗi nào xảy ra, hoàn tác lại toàn bộ thay đổi (Rollback)
                await tx.RollbackAsync(cancellationToken);
                // Ném lại ngoại lệ để cấp trên xử lý
                throw;
            }
        });
    }

    // Phương thức tiện ích để sinh ID duy nhất có tiền tố
    private RefundClaimIssue? CreateRefundClaim(
        Refund refund,
        Booking booking,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(booking.CustomerProfileId))
        {
            return null;
        }

        var issue = _refundClaimIssuer.Create(
            refund.RefundId,
            booking.CustomerProfileId,
            now);
        _db.RefundClaims.Add(issue.Claim);
        return issue;
    }

    private async Task TrySendLatePaymentClaimEmailAsync(
        Booking booking,
        RefundClaimIssue? issue,
        CancellationToken cancellationToken)
    {
        if (issue is null || booking.CustomerProfile?.User is null)
        {
            return;
        }

        try
        {
            var link = $"{_refundSettings.FrontendBaseUrl.TrimEnd('/')}"
                + $"{RefundSettings.ClaimRoute}?t={Uri.EscapeDataString(issue.RawToken)}";
            var movieTitle = booking.Showtime?.Movie.Title ?? "cancelled showtime";
            await _emailSender.SendEmailAsync(
                booking.CustomerProfile.User.Email,
                "Refund information required",
                $"A late payment was received for the cancelled showtime {movieTitle}. "
                + $"Submit bank information before {issue.Token.ExpiresAt:O}: {link}",
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Late-payment refund claim email could not be sent.");
        }
    }

    private static string GenerateId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    // Phương thức sinh nội dung mã QR dùng để quét vé
    private static string GenerateTicketQrCode(string bookingId, string bookingSeatId) =>
        string.Join(
            DomainConstants.TicketQrCode.Separator,
            DomainConstants.TicketQrCode.Prefix,
            bookingId,
            bookingSeatId,
            Guid.NewGuid().ToString("N"));

    // Phương thức tạo mã giao dịch ngẫu nhiên (Dạng T + 10 ký tự Alphanumeric)
    private static string GenerateTransactionCode()
    {
        // Khai báo bộ ký tự được phép sử dụng
        // Khởi tạo StringBuilder để ghép chuỗi hiệu quả
        var sb = new System.Text.StringBuilder();
        // Bắt đầu bằng ký tự 'T'
        sb.Append(DomainConstants.PaymentTransactionCode.Prefix);
        // Vòng lặp 10 lần để chọn ngẫu nhiên 10 ký tự
        for (var i = 0; i < DomainConstants.PaymentTransactionCode.RandomPartLength; i++)
        {
            var characters = DomainConstants.PaymentTransactionCode.AllowedCharacters;
            sb.Append(characters[RandomNumberGenerator.GetInt32(characters.Length)]);
        }
        // Trả về chuỗi kết quả
        return sb.ToString();
    }
}
