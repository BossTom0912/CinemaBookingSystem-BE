using System;
using System.Collections.Generic;
using CinemaSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Persistence;

public partial class CinemaDbContext : DbContext
{
    public CinemaDbContext(DbContextOptions<CinemaDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<BookingFbItem> BookingFbItems { get; set; }

    public virtual DbSet<BookingSeat> BookingSeats { get; set; }

    public virtual DbSet<CheckinLog> CheckinLogs { get; set; }

    public virtual DbSet<Cinema> Cinemas { get; set; }

    public virtual DbSet<CinemaFbInventory> CinemaFbInventories { get; set; }

    public virtual DbSet<CustomerProfile> CustomerProfiles { get; set; }

    public virtual DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }

    public virtual DbSet<FbItem> FbItems { get; set; }

    public virtual DbSet<Movie> Movies { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentProvider> PaymentProviders { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Refund> Refunds { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<RewardPointTransaction> RewardPointTransactions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<Seat> Seats { get; set; }

    public virtual DbSet<SeatType> SeatTypes { get; set; }

    public virtual DbSet<Showtime> Showtimes { get; set; }

    public virtual DbSet<ShowtimeCancellation> ShowtimeCancellations { get; set; }

    public virtual DbSet<ShowtimeSeat> ShowtimeSeats { get; set; }

    public virtual DbSet<StaffProfile> StaffProfiles { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<VoucherUsage> VoucherUsages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ChangeRequest workflow removed - direct CRUD used instead
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AUDIT_LO__56A1B857E9725B26");

            entity.ToTable("AUDIT_LOG");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_AUDIT_LOG_USER_CREATED_AT");

            entity.Property(e => e.AuditLogId)
                .HasMaxLength(50)
                .HasColumnName("auditLogId");
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");
            entity.Property(e => e.CorrelationId)
                .HasMaxLength(100)
                .HasColumnName("correlationId");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.EntityId)
                .HasMaxLength(50)
                .HasColumnName("entityId");
            entity.Property(e => e.EntityName)
                .HasMaxLength(100)
                .HasColumnName("entityName");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(100)
                .HasColumnName("ipAddress");
            entity.Property(e => e.NewValue).HasColumnName("newValue");
            entity.Property(e => e.OldValue).HasColumnName("oldValue");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(500)
                .HasColumnName("userAgent");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("userId");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AUDIT_LOG_USER");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__BOOKING__C6D03BCD1EDCF929");

            entity.ToTable("BOOKING");

            entity.HasIndex(e => e.BookingChannel, "IX_BOOKING_CHANNEL");

            entity.HasIndex(e => e.CreatedByStaffProfileId, "IX_BOOKING_CREATED_BY_STAFF_PROFILE_ID");

            entity.HasIndex(e => e.CustomerProfileId, "IX_BOOKING_CUSTOMER_PROFILE_ID");

            entity.HasIndex(e => e.ShowtimeId, "IX_BOOKING_SHOWTIME_ID");

            entity.HasIndex(e => e.BookingStatus, "IX_BOOKING_STATUS");

            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.BookingChannel)
                .HasMaxLength(30)
                .HasDefaultValue("ONLINE")
                .HasColumnName("bookingChannel");
            entity.Property(e => e.BookingStatus)
                .HasMaxLength(30)
                .HasDefaultValue("CREATED")
                .HasColumnName("bookingStatus");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.CreatedByStaffProfileId)
                .HasMaxLength(50)
                .HasColumnName("createdByStaffProfileId");
            entity.Property(e => e.CustomerProfileId)
                .HasMaxLength(50)
                .HasColumnName("customerProfileId");
            entity.Property(e => e.ExpiredAt).HasColumnName("expiredAt");
            entity.Property(e => e.GuestEmail)
                .HasMaxLength(255)
                .HasColumnName("guestEmail");
            entity.Property(e => e.GuestName)
                .HasMaxLength(255)
                .HasColumnName("guestName");
            entity.Property(e => e.GuestPhone)
                .HasMaxLength(30)
                .HasColumnName("guestPhone");
            entity.Property(e => e.ShowtimeId)
                .HasMaxLength(50)
                .HasColumnName("showtimeId");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("totalAmount");

            entity.HasOne(d => d.CreatedByStaffProfile).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CreatedByStaffProfileId)
                .HasConstraintName("FK_BOOKING_CREATED_BY_STAFF");

            entity.HasOne(d => d.CustomerProfile).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CustomerProfileId)
                .HasConstraintName("FK_BOOKING_CUSTOMER_PROFILE");

            entity.HasOne(d => d.Showtime).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.ShowtimeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BOOKING_SHOWTIME");
        });

        modelBuilder.Entity<BookingFbItem>(entity =>
        {
            entity.HasKey(e => e.BookingFbitemId).HasName("PK__BOOKING___57F09C0846290D54");

            entity.ToTable("BOOKING_FB_ITEM");

            entity.Property(e => e.BookingFbitemId)
                .HasMaxLength(50)
                .HasColumnName("bookingFBItemId");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.FbItemId)
                .HasMaxLength(50)
                .HasColumnName("fbItemId");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Subtotal)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("subtotal");
            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("unitPrice");

            entity.HasOne(d => d.Booking).WithMany(p => p.BookingFbItems)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BOOKING_FB_ITEM_BOOKING");

            entity.HasOne(d => d.FbItem).WithMany(p => p.BookingFbItems)
                .HasForeignKey(d => d.FbItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BOOKING_FB_ITEM_FB_ITEM");
        });

        modelBuilder.Entity<BookingSeat>(entity =>
        {
            entity.HasKey(e => e.BookingSeatId).HasName("PK__BOOKING___0F3B47D674E665BF");

            entity.ToTable("BOOKING_SEAT");

            entity.HasIndex(e => e.ShowtimeSeatId, "UQ_BOOKING_SEAT_SHOWTIME_SEAT").IsUnique();

            entity.Property(e => e.BookingSeatId)
                .HasMaxLength(50)
                .HasColumnName("bookingSeatId");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.SeatPrice)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("seatPrice");
            entity.Property(e => e.ShowtimeSeatId)
                .HasMaxLength(50)
                .HasColumnName("showtimeSeatId");

            entity.HasOne(d => d.Booking).WithMany(p => p.BookingSeats)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BOOKING_SEAT_BOOKING");

            entity.HasOne(d => d.ShowtimeSeat).WithOne(p => p.BookingSeat)
                .HasForeignKey<BookingSeat>(d => d.ShowtimeSeatId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BOOKING_SEAT_SHOWTIME_SEAT");
        });

        modelBuilder.Entity<CheckinLog>(entity =>
        {
            entity.HasKey(e => e.CheckInLogId).HasName("PK__CHECKIN___2243820849225C05");

            entity.ToTable("CHECKIN_LOG");

            entity.HasIndex(e => e.RawQrCode, "IX_CHECKIN_LOG_RAW_QR_CODE").HasFilter("([rawQrCode] IS NOT NULL)");

            entity.HasIndex(e => e.TicketId, "IX_CHECKIN_LOG_TICKET_ID");

            entity.Property(e => e.CheckInLogId)
                .HasMaxLength(50)
                .HasColumnName("checkInLogId");
            entity.Property(e => e.FailureReason)
                .HasMaxLength(500)
                .HasColumnName("failureReason");
            entity.Property(e => e.RawQrCode).HasColumnName("rawQrCode");
            entity.Property(e => e.Result)
                .HasMaxLength(30)
                .HasColumnName("result");
            entity.Property(e => e.ScanTime)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("scanTime");
            entity.Property(e => e.StaffProfileId)
                .HasMaxLength(50)
                .HasColumnName("staffProfileId");
            entity.Property(e => e.TicketId)
                .HasMaxLength(50)
                .HasColumnName("ticketId");

            entity.HasOne(d => d.StaffProfile).WithMany(p => p.CheckinLogs)
                .HasForeignKey(d => d.StaffProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CHECKIN_LOG_STAFF_PROFILE");

            entity.HasOne(d => d.Ticket).WithMany(p => p.CheckinLogs)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("FK_CHECKIN_LOG_TICKET");
        });

        modelBuilder.Entity<Cinema>(entity =>
        {
            entity.HasKey(e => e.CinemaId).HasName("PK__CINEMA__4E679F684FB25BEB");

            entity.ToTable("CINEMA");

            entity.Property(e => e.CinemaId)
                .HasMaxLength(50)
                .HasColumnName("cinemaId");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.CinemaName)
                .HasMaxLength(255)
                .HasColumnName("cinemaName");
            entity.Property(e => e.CinemaStatus)
                .HasMaxLength(30)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("cinemaStatus");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(30)
                .HasColumnName("phoneNumber");
        });

        modelBuilder.Entity<CinemaFbInventory>(entity =>
        {
            entity.HasKey(e => e.CinemaInventoryId).HasName("PK__CINEMA_F__5F0134C6DE981B97");

            entity.ToTable("CINEMA_FB_INVENTORY");

            entity.HasIndex(e => new { e.CinemaId, e.FbItemId }, "UQ_CINEMA_FB_INVENTORY").IsUnique();

            entity.Property(e => e.CinemaInventoryId)
                .HasMaxLength(50)
                .HasColumnName("cinemaInventoryId");
            entity.Property(e => e.CinemaId)
                .HasMaxLength(50)
                .HasColumnName("cinemaId");
            entity.Property(e => e.FbItemId)
                .HasMaxLength(50)
                .HasColumnName("fbItemId");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.Cinema).WithMany(p => p.CinemaFbInventories)
                .HasForeignKey(d => d.CinemaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CINEMA_FB_INVENTORY_CINEMA");

            entity.HasOne(d => d.FbItem).WithMany(p => p.CinemaFbInventories)
                .HasForeignKey(d => d.FbItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CINEMA_FB_INVENTORY_FB_ITEM");
        });

        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.HasKey(e => e.CustomerProfileId).HasName("PK__CUSTOMER__E68F35A03EED0092");

            entity.ToTable("CUSTOMER_PROFILE");

            entity.HasIndex(e => e.UserId, "UQ_CUSTOMER_PROFILE_USER").IsUnique();

            entity.HasIndex(e => e.IdentityCard, "UX_CUSTOMER_PROFILE_IDENTITY_CARD")
                .IsUnique()
                .HasFilter("([identityCard] IS NOT NULL)");

            entity.Property(e => e.CustomerProfileId)
                .HasMaxLength(50)
                .HasColumnName("customerProfileId");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(1000)
                .HasColumnName("avatarUrl");
            entity.Property(e => e.DateOfBirth).HasColumnName("dateOfBirth");
            entity.Property(e => e.Gender)
                .HasMaxLength(20)
                .HasColumnName("gender");
            entity.Property(e => e.IdentityCard)
                .HasMaxLength(50)
                .HasColumnName("identityCard");
            entity.Property(e => e.MemberLevel)
                .HasMaxLength(30)
                .HasDefaultValue("STANDARD")
                .HasColumnName("memberLevel");
            entity.Property(e => e.RewardPoints).HasColumnName("rewardPoints");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("userId");

            entity.HasOne(d => d.User).WithOne(p => p.CustomerProfile)
                .HasForeignKey<CustomerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CUSTOMER_PROFILE_USER");
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("PK__EMAIL_VE__AC16DB47312820A8");

            entity.ToTable("EMAIL_VERIFICATION_TOKEN");

            entity.HasIndex(e => e.Token, "UQ_EMAIL_VERIFICATION_TOKEN").IsUnique();

            entity.Property(e => e.TokenId)
                .HasMaxLength(50)
                .HasColumnName("tokenId");
            entity.Property(e => e.AttemptCount).HasColumnName("attemptCount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.ExpiredAt).HasColumnName("expiredAt");
            entity.Property(e => e.IsUsed).HasColumnName("isUsed");
            entity.Property(e => e.Purpose)
                .HasMaxLength(30)
                .HasDefaultValue("EMAIL_VERIFICATION")
                .HasColumnName("purpose");
            entity.Property(e => e.Token)
                .HasMaxLength(255)
                .HasColumnName("token");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("userId");
            entity.Property(e => e.VerifiedAt).HasColumnName("verifiedAt");

            entity.HasOne(d => d.User).WithMany(p => p.EmailVerificationTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMAIL_VERIFICATION_USER");
        });

        modelBuilder.Entity<FbItem>(entity =>
        {
            entity.HasKey(e => e.FbItemId).HasName("PK__FB_ITEM__B91DF1DD80E826D9");

            entity.ToTable("FB_ITEM");

            entity.Property(e => e.FbItemId)
                .HasMaxLength(50)
                .HasColumnName("fbItemId");
            entity.Property(e => e.ItemName)
                .HasMaxLength(255)
                .HasColumnName("itemName");
            entity.Property(e => e.ItemStatus)
                .HasMaxLength(30)
                .HasDefaultValue("AVAILABLE")
                .HasColumnName("itemStatus");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("price");
        });

        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(e => e.MovieId).HasName("PK__MOVIE__42EB374E18A44435");

            entity.ToTable("MOVIE");

            entity.Property(e => e.MovieId)
                .HasMaxLength(50)
                .HasColumnName("movieId");
            entity.Property(e => e.AgeRating)
                .HasMaxLength(30)
                .HasColumnName("ageRating");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DurationMinutes).HasColumnName("durationMinutes");
            entity.Property(e => e.Genre)
                .HasMaxLength(255)
                .HasColumnName("genre");
            entity.Property(e => e.Language)
                .HasMaxLength(100)
                .HasColumnName("language");
            entity.Property(e => e.MovieStatus)
                .HasMaxLength(30)
                .HasDefaultValue("COMING_SOON")
                .HasColumnName("movieStatus");
            entity.Property(e => e.PosterUrl)
                .HasMaxLength(1000)
                .HasColumnName("posterUrl");
            entity.Property(e => e.ReleaseDate).HasColumnName("releaseDate");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.TrailerUrl)
                .HasMaxLength(1000)
                .HasColumnName("trailerUrl");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__NOTIFICA__4BA5CEA9CB086DEB");

            entity.ToTable("NOTIFICATION");

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "IX_NOTIFICATION_USER_READ");

            entity.Property(e => e.NotificationId)
                .HasMaxLength(50)
                .HasColumnName("notificationId");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.IsRead).HasColumnName("isRead");
            entity.Property(e => e.Message)
                .HasMaxLength(1000)
                .HasColumnName("message");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("userId");

            entity.HasOne(d => d.Booking).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("FK_NOTIFICATION_BOOKING");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NOTIFICATION_USER");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__PAYMENT__A0D9EFC6CE4D9F45");

            entity.ToTable("PAYMENT");

            entity.HasIndex(e => e.BookingId, "IX_PAYMENT_BOOKING_ID");

            entity.HasIndex(e => e.BookingId, "UX_PAYMENT_ONE_SUCCESS_PER_BOOKING")
                .IsUnique()
                .HasFilter("([paymentStatus]='SUCCESS')");

            entity.HasIndex(e => e.ProviderTransactionCode, "UX_PAYMENT_PROVIDER_TRANSACTION_CODE")
                .IsUnique()
                .HasFilter("([providerTransactionCode] IS NOT NULL)");

            entity.HasIndex(e => e.TransactionCode, "UX_PAYMENT_TRANSACTION_CODE")
                .IsUnique()
                .HasFilter("([transactionCode] IS NOT NULL)");

            entity.Property(e => e.PaymentId)
                .HasMaxLength(50)
                .HasColumnName("paymentId");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.FailureReason)
                .HasMaxLength(1000)
                .HasColumnName("failureReason");
            entity.Property(e => e.PaidAt).HasColumnName("paidAt");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("paymentMethod");
            entity.Property(e => e.PaymentProviderId)
                .HasMaxLength(50)
                .HasColumnName("paymentProviderId");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(30)
                .HasDefaultValue("PENDING")
                .HasColumnName("paymentStatus");
            entity.Property(e => e.ProviderTransactionCode)
                .HasMaxLength(255)
                .HasColumnName("providerTransactionCode");
            entity.Property(e => e.RawCallbackPayload).HasColumnName("rawCallbackPayload");
            entity.Property(e => e.TransactionCode)
                .HasMaxLength(255)
                .HasColumnName("transactionCode");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");

            entity.HasOne(d => d.Booking).WithMany(p => p.Payments)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PAYMENT_BOOKING");

            entity.HasOne(d => d.PaymentProvider).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PaymentProviderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PAYMENT_PAYMENT_PROVIDER");
        });

        modelBuilder.Entity<PaymentProvider>(entity =>
        {
            entity.HasKey(e => e.PaymentProviderId).HasName("PK__PAYMENT___BD97D5097BEA8EB1");

            entity.ToTable("PAYMENT_PROVIDER");

            entity.HasIndex(e => e.ProviderName, "UQ_PAYMENT_PROVIDER_NAME").IsUnique();

            entity.Property(e => e.PaymentProviderId)
                .HasMaxLength(50)
                .HasColumnName("paymentProviderId");
            entity.Property(e => e.ApiEndpoint)
                .HasMaxLength(1000)
                .HasColumnName("apiEndpoint");
            entity.Property(e => e.ProviderName)
                .HasMaxLength(100)
                .HasColumnName("providerName");
            entity.Property(e => e.ProviderStatus)
                .HasMaxLength(30)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("providerStatus");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId).HasName("PK__REFRESH___FEAC95C8CD18D87E");

            entity.ToTable("REFRESH_TOKEN");

            entity.HasIndex(e => e.TokenHash, "UQ_REFRESH_TOKEN_HASH").IsUnique();

            entity.Property(e => e.RefreshTokenId)
                .HasMaxLength(50)
                .HasColumnName("refreshTokenId");
            entity.Property(e => e.ExpiresAt).HasColumnName("expiresAt");
            entity.Property(e => e.IsRevoked).HasColumnName("isRevoked");
            entity.Property(e => e.IssuedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("issuedAt");
            entity.Property(e => e.RevokedAt).HasColumnName("revokedAt");
            entity.Property(e => e.TokenHash).HasColumnName("tokenHash");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("userId");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REFRESH_TOKEN_USER");
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.RefundId).HasName("PK__REFUND__B219848F090ED840");

            entity.ToTable("REFUND");

            entity.HasIndex(e => e.BookingId, "IX_REFUND_BOOKING_ID");

            entity.HasIndex(e => e.ProviderRefundCode, "UX_REFUND_PROVIDER_REFUND_CODE")
                .IsUnique()
                .HasFilter("([providerRefundCode] IS NOT NULL)");

            entity.Property(e => e.RefundId)
                .HasMaxLength(50)
                .HasColumnName("refundId");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.FailureReason)
                .HasMaxLength(1000)
                .HasColumnName("failureReason");
            entity.Property(e => e.PaymentId)
                .HasMaxLength(50)
                .HasColumnName("paymentId");
            entity.Property(e => e.PaymentProviderId)
                .HasMaxLength(50)
                .HasColumnName("paymentProviderId");
            entity.Property(e => e.ProviderRefundCode)
                .HasMaxLength(255)
                .HasColumnName("providerRefundCode");
            entity.Property(e => e.RefundAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("refundAmount");
            entity.Property(e => e.RefundReason)
                .HasMaxLength(1000)
                .HasColumnName("refundReason");
            entity.Property(e => e.RefundStatus)
                .HasMaxLength(30)
                .HasDefaultValue("PENDING")
                .HasColumnName("refundStatus");
            entity.Property(e => e.RefundedAt).HasColumnName("refundedAt");
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("requestedAt");
            entity.Property(e => e.ShowtimeCancellationId)
                .HasMaxLength(50)
                .HasColumnName("showtimeCancellationId");

            entity.HasOne(d => d.Booking).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REFUND_BOOKING");

            entity.HasOne(d => d.Payment).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REFUND_PAYMENT");

            entity.HasOne(d => d.PaymentProvider).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.PaymentProviderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REFUND_PAYMENT_PROVIDER");

            entity.HasOne(d => d.ShowtimeCancellation).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.ShowtimeCancellationId)
                .HasConstraintName("FK_REFUND_SHOWTIME_CANCELLATION");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__REVIEW__2ECD6E044225F0E6");

            entity.ToTable("REVIEW");

            entity.HasIndex(e => e.BookingId, "UX_REVIEW_BOOKING")
                .IsUnique()
                .HasFilter("([bookingId] IS NOT NULL)");

            entity.Property(e => e.ReviewId)
                .HasMaxLength(50)
                .HasColumnName("reviewId");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.Comment)
                .HasMaxLength(1000)
                .HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.CustomerProfileId)
                .HasMaxLength(50)
                .HasColumnName("customerProfileId");
            entity.Property(e => e.MovieId)
                .HasMaxLength(50)
                .HasColumnName("movieId");
            entity.Property(e => e.Rating).HasColumnName("rating");

            entity.HasOne(d => d.Booking).WithOne(p => p.Review)
                .HasForeignKey<Review>(d => d.BookingId)
                .HasConstraintName("FK_REVIEW_BOOKING");

            entity.HasOne(d => d.CustomerProfile).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.CustomerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REVIEW_CUSTOMER_PROFILE");

            entity.HasOne(d => d.Movie).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.MovieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REVIEW_MOVIE");
        });

        modelBuilder.Entity<RewardPointTransaction>(entity =>
        {
            entity.HasKey(e => e.RewardTransactionId).HasName("PK__REWARD_P__F1F5882BD2566DA5");

            entity.ToTable("REWARD_POINT_TRANSACTION");

            entity.Property(e => e.RewardTransactionId)
                .HasMaxLength(50)
                .HasColumnName("rewardTransactionId");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.CustomerProfileId)
                .HasMaxLength(50)
                .HasColumnName("customerProfileId");
            entity.Property(e => e.Points).HasColumnName("points");
            entity.Property(e => e.TransactionType)
                .HasMaxLength(30)
                .HasColumnName("transactionType");

            entity.HasOne(d => d.Booking).WithMany(p => p.RewardPointTransactions)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("FK_REWARD_POINT_TRANSACTION_BOOKING");

            entity.HasOne(d => d.CustomerProfile).WithMany(p => p.RewardPointTransactions)
                .HasForeignKey(d => d.CustomerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REWARD_POINT_TRANSACTION_CUSTOMER_PROFILE");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__ROLE__CD98462A6C3AD81E");

            entity.ToTable("ROLE");

            entity.HasIndex(e => e.RoleName, "UQ_ROLE_ROLE_NAME").IsUnique();

            entity.Property(e => e.RoleId)
                .HasMaxLength(50)
                .HasColumnName("roleId");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.RoleName)
                .HasMaxLength(100)
                .HasColumnName("roleName");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__ROOM__6C3BF5BE71CF940F");

            entity.ToTable("ROOM");

            entity.HasIndex(e => e.CinemaId, "IX_ROOM_CINEMA_ID");

            entity.HasIndex(e => new { e.CinemaId, e.RoomName }, "UQ_ROOM_CINEMA_ROOM_NAME").IsUnique();

            entity.Property(e => e.RoomId)
                .HasMaxLength(50)
                .HasColumnName("roomId");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.CinemaId)
                .HasMaxLength(50)
                .HasColumnName("cinemaId");
            entity.Property(e => e.RoomName)
                .HasMaxLength(100)
                .HasColumnName("roomName");
            entity.Property(e => e.RoomStatus)
                .HasMaxLength(30)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("roomStatus");

            entity.HasOne(d => d.Cinema).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.CinemaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ROOM_CINEMA");
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(e => e.SeatId).HasName("PK__SEAT__BC5329EA8B0220AA");

            entity.ToTable("SEAT");

            entity.HasIndex(e => e.RoomId, "IX_SEAT_ROOM_ID");

            entity.HasIndex(e => new { e.RoomId, e.RowLabel, e.SeatNumber }, "UQ_SEAT_ROOM_ROW_NUMBER").IsUnique();

            entity.HasIndex(e => new { e.RoomId, e.SeatCode }, "UQ_SEAT_ROOM_SEAT_CODE").IsUnique();

            entity.Property(e => e.SeatId)
                .HasMaxLength(50)
                .HasColumnName("seatId");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.RoomId)
                .HasMaxLength(50)
                .HasColumnName("roomId");
            entity.Property(e => e.RowLabel)
                .HasMaxLength(10)
                .HasColumnName("rowLabel");
            entity.Property(e => e.SeatCode)
                .HasMaxLength(20)
                .HasColumnName("seatCode");
            entity.Property(e => e.SeatNumber).HasColumnName("seatNumber");
            entity.Property(e => e.SeatTypeId)
                .HasMaxLength(50)
                .HasColumnName("seatTypeId");

            entity.HasOne(d => d.Room).WithMany(p => p.Seats)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SEAT_ROOM");

            entity.HasOne(d => d.SeatType).WithMany(p => p.Seats)
                .HasForeignKey(d => d.SeatTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SEAT_SEAT_TYPE");
        });

        modelBuilder.Entity<SeatType>(entity =>
        {
            entity.HasKey(e => e.SeatTypeId).HasName("PK__SEAT_TYP__0DE1222D8F93942B");

            entity.ToTable("SEAT_TYPE");

            entity.HasIndex(e => e.TypeName, "UQ_SEAT_TYPE_NAME").IsUnique();

            entity.Property(e => e.SeatTypeId)
                .HasMaxLength(50)
                .HasColumnName("seatTypeId");
            entity.Property(e => e.ExtraFee)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("extraFee");
            entity.Property(e => e.TypeName)
                .HasMaxLength(100)
                .HasColumnName("typeName");
        });

        modelBuilder.Entity<Showtime>(entity =>
        {
            entity.HasKey(e => e.ShowtimeId).HasName("PK__SHOWTIME__B4CBD8842A34432D");

            entity.ToTable("SHOWTIME");

            entity.HasIndex(e => e.MovieId, "IX_SHOWTIME_MOVIE_ID");

            entity.HasIndex(e => new { e.RoomId, e.StartTime, e.EndTime }, "IX_SHOWTIME_ROOM_TIME");
            entity.HasIndex(
        e => new
        {
            e.RoomId,
            e.StartTime
        },
        "UQ_SHOWTIME_ROOM_STARTTIME")
    .IsUnique();
            entity.Property(e => e.ShowtimeId)
                .HasMaxLength(50)
                .HasColumnName("showtimeId");
            entity.Property(e => e.BasePrice)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("basePrice");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.EndTime).HasColumnName("endTime");
            entity.Property(e => e.MovieId)
                .HasMaxLength(50)
                .HasColumnName("movieId");
            entity.Property(e => e.RoomId)
                .HasMaxLength(50)
                .HasColumnName("roomId");
            entity.Property(e => e.StartTime).HasColumnName("startTime");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("OPEN")
                .HasColumnName("status");

            entity.HasOne(d => d.Movie).WithMany(p => p.Showtimes)
                .HasForeignKey(d => d.MovieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHOWTIME_MOVIE");

            entity.HasOne(d => d.Room).WithMany(p => p.Showtimes)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHOWTIME_ROOM");
        });

        modelBuilder.Entity<ShowtimeCancellation>(entity =>
        {
            entity.HasKey(e => e.ShowtimeCancellationId).HasName("PK__SHOWTIME__653AA5AFEB70BC94");

            entity.ToTable("SHOWTIME_CANCELLATION");

            entity.HasIndex(e => e.ShowtimeId, "UQ_SHOWTIME_CANCELLATION_SHOWTIME").IsUnique();

            entity.Property(e => e.ShowtimeCancellationId)
                .HasMaxLength(50)
                .HasColumnName("showtimeCancellationId");
            entity.Property(e => e.CancelReason)
                .HasMaxLength(1000)
                .HasColumnName("cancelReason");
            entity.Property(e => e.CancelledAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("cancelledAt");
            entity.Property(e => e.CancelledByStaffId)
                .HasMaxLength(50)
                .HasColumnName("cancelledByStaffId");
            entity.Property(e => e.CancelledByUserId)
                .HasMaxLength(50)
                .HasColumnName("cancelledByUserId");
            entity.Property(e => e.ShowtimeId)
                .HasMaxLength(50)
                .HasColumnName("showtimeId");

            entity.HasOne(d => d.CancelledByStaff).WithMany(p => p.ShowtimeCancellations)
                .HasForeignKey(d => d.CancelledByStaffId)
                .HasConstraintName("FK_SHOWTIME_CANCELLATION_STAFF_PROFILE");

            entity.HasOne(d => d.CancelledByUser).WithMany(p => p.ShowtimeCancellations)
                .HasForeignKey(d => d.CancelledByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHOWTIME_CANCELLATION_USER");

            entity.HasOne(d => d.Showtime).WithOne(p => p.ShowtimeCancellation)
                .HasForeignKey<ShowtimeCancellation>(d => d.ShowtimeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHOWTIME_CANCELLATION_SHOWTIME");
        });

        modelBuilder.Entity<ShowtimeSeat>(entity =>
        {
            entity.HasKey(e => e.ShowtimeSeatId).HasName("PK__SHOWTIME__EA7AD159C6275EB6");

            entity.ToTable("SHOWTIME_SEAT");

            entity.HasIndex(e => e.ShowtimeId, "IX_SHOWTIME_SEAT_SHOWTIME_ID");

            entity.HasIndex(e => new { e.ShowtimeId, e.SeatStatus }, "IX_SHOWTIME_SEAT_STATUS");

            entity.HasIndex(e => new { e.ShowtimeId, e.SeatId }, "UQ_SHOWTIME_SEAT_SHOWTIME_SEAT").IsUnique();

            entity.Property(e => e.ShowtimeSeatId)
                .HasMaxLength(50)
                .HasColumnName("showtimeSeatId");
            entity.Property(e => e.LockedByUserId)
                .HasMaxLength(50)
                .HasColumnName("lockedByUserId");
            entity.Property(e => e.LockedUntil).HasColumnName("lockedUntil");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowVersion");
            entity.Property(e => e.SeatId)
                .HasMaxLength(50)
                .HasColumnName("seatId");
            entity.Property(e => e.SeatStatus)
                .HasMaxLength(30)
                .HasDefaultValue("AVAILABLE")
                .HasColumnName("seatStatus");
            entity.Property(e => e.ShowtimeId)
                .HasMaxLength(50)
                .HasColumnName("showtimeId");

            entity.HasOne(d => d.LockedByUser).WithMany(p => p.ShowtimeSeats)
                .HasForeignKey(d => d.LockedByUserId)
                .HasConstraintName("FK_SHOWTIME_SEAT_LOCKED_BY_USER");

            entity.HasOne(d => d.Seat).WithMany(p => p.ShowtimeSeats)
                .HasForeignKey(d => d.SeatId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHOWTIME_SEAT_SEAT");

            entity.HasOne(d => d.Showtime).WithMany(p => p.ShowtimeSeats)
                .HasForeignKey(d => d.ShowtimeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SHOWTIME_SEAT_SHOWTIME");
        });

        modelBuilder.Entity<StaffProfile>(entity =>
        {
            entity.HasKey(e => e.StaffProfileId).HasName("PK__STAFF_PR__792B9F5817DC9A79");

            entity.ToTable("STAFF_PROFILE");

            entity.HasIndex(e => e.CinemaId, "IX_STAFF_PROFILE_CINEMA_ID");

            entity.HasIndex(e => e.UserId, "UQ_STAFF_PROFILE_USER").IsUnique();

            entity.HasIndex(e => e.IdentityCard, "UX_STAFF_PROFILE_IDENTITY_CARD")
                .IsUnique()
                .HasFilter("([identityCard] IS NOT NULL)");

            entity.Property(e => e.StaffProfileId)
                .HasMaxLength(50)
                .HasColumnName("staffProfileId");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(1000)
                .HasColumnName("avatarUrl");
            entity.Property(e => e.CinemaId)
                .HasMaxLength(50)
                .HasColumnName("cinemaId");
            entity.Property(e => e.DateOfBirth).HasColumnName("dateOfBirth");
            entity.Property(e => e.EmploymentStatus)
                .HasMaxLength(30)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("employmentStatus");
            entity.Property(e => e.Gender)
                .HasMaxLength(20)
                .HasColumnName("gender");
            entity.Property(e => e.HireDate).HasColumnName("hireDate");
            entity.Property(e => e.IdentityCard)
                .HasMaxLength(50)
                .HasColumnName("identityCard");
            entity.Property(e => e.Position)
                .HasMaxLength(100)
                .HasColumnName("position");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("userId");

            entity.HasOne(d => d.Cinema).WithMany(p => p.StaffProfiles)
                .HasForeignKey(d => d.CinemaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STAFF_PROFILE_CINEMA");

            entity.HasOne(d => d.User).WithOne(p => p.StaffProfile)
                .HasForeignKey<StaffProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_STAFF_PROFILE_USER");
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.TicketId).HasName("PK__TICKET__3333C61084CF344A");

            entity.ToTable("TICKET");

            entity.HasIndex(e => e.BookingSeatId, "UQ_TICKET_BOOKING_SEAT").IsUnique();

            entity.HasIndex(e => e.QrCode, "UQ_TICKET_QR_CODE").IsUnique();

            entity.Property(e => e.TicketId)
                .HasMaxLength(50)
                .HasColumnName("ticketId");
            entity.Property(e => e.BookingSeatId)
                .HasMaxLength(50)
                .HasColumnName("bookingSeatId");
            entity.Property(e => e.GeneratedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("generatedAt");
            entity.Property(e => e.QrCode).HasColumnName("qrCode");
            entity.Property(e => e.TicketStatus)
                .HasMaxLength(30)
                .HasDefaultValue("UNUSED")
                .HasColumnName("ticketStatus");

            entity.HasOne(d => d.BookingSeat).WithOne(p => p.Ticket)
                .HasForeignKey<Ticket>(d => d.BookingSeatId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TICKET_BOOKING_SEAT");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__USER__CB9A1CFF4C01E85A");

            entity.ToTable("USER");

            entity.HasIndex(e => e.RoleId, "IX_USER_ROLE_ID");

            entity.HasIndex(e => e.Email, "UQ_USER_EMAIL").IsUnique();

            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("userId");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("createdAt");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.EmailVerified).HasColumnName("emailVerified");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("fullName");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(500)
                .HasColumnName("passwordHash");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(30)
                .HasColumnName("phoneNumber");
            entity.Property(e => e.RoleId)
                .HasMaxLength(50)
                .HasColumnName("roleId");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("PENDING_VERIFICATION")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USER_ROLE");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("PK__VOUCHER__F53389E98930B69A");

            entity.ToTable("VOUCHER");

            entity.HasIndex(e => e.VoucherCode, "UQ_VOUCHER_CODE").IsUnique();

            entity.Property(e => e.VoucherId)
                .HasMaxLength(50)
                .HasColumnName("voucherId");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.DiscountType)
                .HasMaxLength(30)
                .HasColumnName("discountType");
            entity.Property(e => e.DiscountValue)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("discountValue");
            entity.Property(e => e.EndDate).HasColumnName("endDate");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(1000)
                .HasColumnName("imageUrl");
            entity.Property(e => e.MaxDiscountAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("maxDiscountAmount");
            entity.Property(e => e.MinOrderAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("minOrderAmount");
            entity.Property(e => e.PerCustomerLimit).HasColumnName("perCustomerLimit");
            entity.Property(e => e.StartDate).HasColumnName("startDate");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UsageLimit).HasColumnName("usageLimit");
            entity.Property(e => e.UsedCount).HasColumnName("usedCount");
            entity.Property(e => e.VoucherCode)
                .HasMaxLength(100)
                .HasColumnName("voucherCode");
            entity.Property(e => e.VoucherStatus)
                .HasMaxLength(30)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("voucherStatus");
        });

        modelBuilder.Entity<VoucherUsage>(entity =>
        {
            entity.HasKey(e => e.VoucherUsageId).HasName("PK__VOUCHER___043EFB915245BCD0");

            entity.ToTable("VOUCHER_USAGE");

            entity.HasIndex(e => e.BookingId, "UQ_VOUCHER_USAGE_BOOKING").IsUnique();

            entity.Property(e => e.VoucherUsageId)
                .HasMaxLength(50)
                .HasColumnName("voucherUsageId");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.CustomerProfileId)
                .HasMaxLength(50)
                .HasColumnName("customerProfileId");
            entity.Property(e => e.DiscountAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("discountAmount");
            entity.Property(e => e.UsageStatus)
                .HasMaxLength(30)
                .HasDefaultValue("APPLIED")
                .HasColumnName("usageStatus");
            entity.Property(e => e.UsedAt).HasColumnName("usedAt");
            entity.Property(e => e.VoucherId)
                .HasMaxLength(50)
                .HasColumnName("voucherId");

            entity.HasOne(d => d.Booking).WithOne(p => p.VoucherUsage)
                .HasForeignKey<VoucherUsage>(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VOUCHER_USAGE_BOOKING");

            entity.HasOne(d => d.CustomerProfile).WithMany(p => p.VoucherUsages)
                .HasForeignKey(d => d.CustomerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VOUCHER_USAGE_CUSTOMER_PROFILE");

            entity.HasOne(d => d.Voucher).WithMany(p => p.VoucherUsages)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VOUCHER_USAGE_VOUCHER");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
