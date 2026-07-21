using System;
using System.Collections.Generic;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CinemaSystem.Infrastructure.Persistence;

public partial class CinemaDbContext : DbContext
{
    public CinemaDbContext(DbContextOptions<CinemaDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<BankDirectory> BankDirectories { get; set; }

    public virtual DbSet<BookingFbItem> BookingFbItems { get; set; }

    public virtual DbSet<BookingSeat> BookingSeats { get; set; }

    public virtual DbSet<CheckinLog> CheckinLogs { get; set; }

    public virtual DbSet<Cinema> Cinemas { get; set; }

    public virtual DbSet<CinemaFbInventory> CinemaFbInventories { get; set; }

    public virtual DbSet<CustomerProfile> CustomerProfiles { get; set; }

    public virtual DbSet<CancellationCompensation> CancellationCompensations { get; set; }

    public virtual DbSet<CompensationTicket> CompensationTickets { get; set; }

    public virtual DbSet<CompensationCombo> CompensationCombos { get; set; }

    public virtual DbSet<CustomerRefundRequest> CustomerRefundRequests { get; set; }

    public virtual DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }

    public virtual DbSet<FbItem> FbItems { get; set; }

    public virtual DbSet<Movie> Movies { get; set; }

    public virtual DbSet<Banner> Banners { get; set; }

    public virtual DbSet<MovieViewLog> MovieViewLogs { get; set; }

    public virtual DbSet<MovieDailyView> MovieDailyViews { get; set; }

    public virtual DbSet<Genre> Genres { get; set; }

    public virtual DbSet<MovieGenre> MovieGenres { get; set; }

    public virtual DbSet<Language> Languages { get; set; }

    public virtual DbSet<ChatHistory> ChatHistories { get; set; }

    public virtual DbSet<ReviewEditHistory> ReviewEditHistories { get; set; }

    public virtual DbSet<ReviewModerationHistory> ReviewModerationHistories { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<ManualRefundProcess> ManualRefundProcesses { get; set; }

    public virtual DbSet<RefundCustomerConfirmation> RefundCustomerConfirmations { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentProvider> PaymentProviders { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Refund> Refunds { get; set; }

    public virtual DbSet<RefundClaim> RefundClaims { get; set; }

    public virtual DbSet<RefundClaimToken> RefundClaimTokens { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<RewardPointTransaction> RewardPointTransactions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RoleAssignmentRule> RoleAssignmentRules { get; set; }

    public virtual DbSet<RoleProvisioningPolicy> RoleProvisioningPolicies { get; set; }

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

    public virtual DbSet<CustomerVoucher> CustomerVouchers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ChangeRequest workflow removed - direct CRUD used instead
        modelBuilder.Entity<BankDirectory>(entity =>
        {
            entity.HasKey(e => e.BankCode);
            entity.ToTable("BANK_DIRECTORY");
            entity.HasIndex(e => e.BankBin, "UQ_BANK_DIRECTORY_BIN").IsUnique();
            entity.Property(e => e.BankCode).HasMaxLength(20).HasColumnName("bankCode");
            entity.Property(e => e.BankBin).HasMaxLength(20).HasColumnName("bankBin");
            entity.Property(e => e.ShortName).HasMaxLength(100).HasColumnName("shortName");
            entity.Property(e => e.FullName).HasMaxLength(255).HasColumnName("fullName");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("isActive");
            entity.Property(e => e.SupportsAccountInquiry).HasColumnName("supportsAccountInquiry");
            entity.Property(e => e.SupportsPayout).HasColumnName("supportsPayout");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
        });

        modelBuilder.Entity<Banner>(entity =>
        {
            entity.HasKey(e => e.BannerId);
            entity.ToTable("BANNER");
            entity.Property(e => e.BannerId).HasMaxLength(50).HasColumnName("bannerId");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.ImageUrl).HasMaxLength(1000).HasColumnName("imageUrl");
            entity.Property(e => e.LinkUrl).HasMaxLength(1000).HasColumnName("linkUrl");
            entity.Property(e => e.BannerType).HasMaxLength(50).HasColumnName("bannerType");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0).HasColumnName("displayOrder");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("isActive");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnName("createdAt");
        });

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

            entity.HasIndex(e => e.FbFulfilledByStaffProfileId, "IX_BOOKING_FB_FULFILLED_BY_STAFF_PROFILE_ID");

            entity.HasIndex(e => e.CustomerProfileId, "IX_BOOKING_CUSTOMER_PROFILE_ID");

            entity.HasIndex(e => new { e.CustomerProfileId, e.ClientRequestId }, "UX_BOOKING_CUSTOMER_CLIENT_REQUEST")
                .IsUnique()
                .HasFilter("[clientRequestId] IS NOT NULL");

            entity.HasIndex(e => e.ShowtimeId, "IX_BOOKING_SHOWTIME_ID");

            entity.HasIndex(e => e.BookingStatus, "IX_BOOKING_STATUS");

            entity.HasIndex(e => new { e.BookingStatus, e.ExpiredAt }, "IX_BOOKING_STATUS_EXPIRED_AT");

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
            entity.Property(e => e.ClientRequestId)
                .HasColumnName("clientRequestId");
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
            entity.Property(e => e.RequestFingerprint)
                .HasMaxLength(64)
                .IsUnicode(false)
                .HasColumnName("requestFingerprint");
            entity.Property(e => e.FbFulfillmentStatus)
                .HasMaxLength(30)
                .HasDefaultValue("NOT_REQUIRED")
                .HasColumnName("fbFulfillmentStatus");
            entity.Property(e => e.FbFulfilledAt)
                .HasColumnName("fbFulfilledAt");
            entity.Property(e => e.FbFulfilledByStaffProfileId)
                .HasMaxLength(50)
                .HasColumnName("fbFulfilledByStaffProfileId");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("totalAmount");
            entity.Property(e => e.CompensationDiscountAmount)
                .HasColumnType("decimal(18, 2)")
                .HasDefaultValue(0m)
                .HasColumnName("compensationDiscountAmount");

            entity.HasOne(d => d.CreatedByStaffProfile).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CreatedByStaffProfileId)
                .HasConstraintName("FK_BOOKING_CREATED_BY_STAFF");

            entity.HasOne(d => d.FbFulfilledByStaffProfile).WithMany(p => p.FulfilledFbBookings)
                .HasForeignKey(d => d.FbFulfilledByStaffProfileId)
                .HasConstraintName("FK_BOOKING_FB_FULFILLED_BY_STAFF");

            entity.HasOne(d => d.CustomerProfile).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CustomerProfileId)
                .HasConstraintName("FK_BOOKING_CUSTOMER_PROFILE");

            entity.HasOne(d => d.Showtime).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict)
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

            entity.HasIndex(
                e => new { e.ScannedByUserId, e.ScanTime },
                "IX_CHECKIN_LOG_SCANNED_BY_USER_TIME");

            entity.HasIndex(e => e.TicketId, "IX_CHECKIN_LOG_TICKET_ID");

            entity.Property(e => e.CheckInLogId)
                .HasMaxLength(50)
                .HasColumnName("checkInLogId");
            entity.Property(e => e.FailureReason)
                .HasMaxLength(500)
                .HasColumnName("failureReason");
            entity.Property(e => e.RawQrCode)
                .HasMaxLength(450)
                .HasColumnName("rawQrCode");
            entity.Property(e => e.Result)
                .HasMaxLength(30)
                .HasColumnName("result");
            entity.Property(e => e.ScannedByUserId)
                .HasMaxLength(50)
                .HasColumnName("scannedByUserId");
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
                .HasConstraintName("FK_CHECKIN_LOG_STAFF_PROFILE");

            entity.HasOne(d => d.ScannedByUser).WithMany(p => p.CheckinLogs)
                .HasForeignKey(d => d.ScannedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CHECKIN_LOG_SCANNED_BY_USER");

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
            entity.Property(e => e.Highlight)
                .HasMaxLength(30)
                .HasColumnName("highlight");
            entity.Property(e => e.LanguageId)
                .HasMaxLength(50)
                .HasColumnName("languageId");
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
            entity.Property(e => e.BannerUrl)
                .HasMaxLength(1000)
                .HasColumnName("bannerUrl");
            entity.Property(e => e.ViewCount)
                .HasDefaultValue(0)
                .HasColumnName("viewCount");
            entity.Property(e => e.AverageRating)
                .HasColumnType("decimal(3, 2)")
                .HasDefaultValue(0.0m)
                .HasColumnName("averageRating");
            entity.Property(e => e.TotalReviews)
                .HasDefaultValue(0)
                .HasColumnName("totalReviews");
            entity.Property(e => e.TotalViews)
                .HasDefaultValue(0)
                .HasColumnName("totalViews");
            entity.Property(e => e.DailyViews)
                .HasDefaultValue(0)
                .HasColumnName("dailyViews");

            entity.HasOne(d => d.Language).WithMany(p => p.Movies)
                .HasForeignKey(d => d.LanguageId)
                .HasConstraintName("FK_MOVIE_LANGUAGE");
        });

        modelBuilder.Entity<MovieViewLog>(entity =>
        {
            entity.HasKey(e => e.MovieViewLogId);
            entity.ToTable("MOVIE_VIEW_LOG");

            entity.Property(e => e.MovieViewLogId)
                .HasMaxLength(50)
                .HasColumnName("movieViewLogId");
            entity.Property(e => e.MovieId)
                .HasMaxLength(50)
                .HasColumnName("movieId");
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .HasColumnName("userId");
            entity.Property(e => e.ViewedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .HasColumnName("viewedAt");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(100)
                .HasColumnName("ipAddress");

            entity.HasOne(d => d.Movie).WithMany(p => p.MovieViewLogs)
                .HasForeignKey(d => d.MovieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MOVIE_VIEW_LOG_MOVIE");
        });

        modelBuilder.Entity<MovieDailyView>(entity =>
        {
            entity.HasKey(e => new { e.MovieId, e.ViewDate });
            entity.ToTable("MOVIE_DAILY_VIEW");
            entity.HasIndex(e => e.ViewDate, "IX_MOVIE_DAILY_VIEW_DATE");

            entity.Property(e => e.MovieId).HasMaxLength(50).HasColumnName("movieId");
            entity.Property(e => e.ViewDate).HasColumnName("viewDate");
            entity.Property(e => e.ViewCount).HasDefaultValue(0).HasColumnName("viewCount");

            entity.HasOne(d => d.Movie).WithMany(p => p.MovieDailyViews)
                .HasForeignKey(d => d.MovieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MOVIE_DAILY_VIEW_MOVIE");
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.GenreId);
            entity.ToTable("GENRE");
            entity.Property(e => e.GenreId).HasColumnName("genreId");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
        });

        modelBuilder.Entity<MovieGenre>(entity =>
        {
            entity.HasKey(e => new { e.MovieId, e.GenreId });
            entity.ToTable("MOVIE_GENRE");
            entity.Property(e => e.MovieId).HasMaxLength(50).HasColumnName("movieId");
            entity.Property(e => e.GenreId).HasColumnName("genreId");

            entity.HasOne(d => d.Movie).WithMany(p => p.MovieGenres)
                .HasForeignKey(d => d.MovieId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MOVIE_GENRE_MOVIE");

            entity.HasOne(d => d.Genre).WithMany(p => p.MovieGenres)
                .HasForeignKey(d => d.GenreId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MOVIE_GENRE_GENRE");
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.HasKey(e => e.LanguageId);
            entity.ToTable("LANGUAGE");
            entity.Property(e => e.LanguageId).HasMaxLength(50).HasColumnName("languageId");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
        });

        modelBuilder.Entity<ChatHistory>(entity =>
        {
            entity.HasKey(e => e.ChatHistoryId);
            entity.ToTable("CHAT_HISTORY");

            entity.Property(e => e.ChatHistoryId).HasMaxLength(50).HasColumnName("chatHistoryId");
            entity.Property(e => e.UserId).HasMaxLength(50).HasColumnName("userId");
            entity.Property(e => e.UserMessage).HasColumnName("userMessage");
            entity.Property(e => e.AiReplyMessage).HasColumnName("aiReplyMessage");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").HasColumnName("createdAt");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_CHAT_HISTORY_USER");
        });

        modelBuilder.Entity<ReviewEditHistory>(entity =>
        {
            entity.HasKey(e => e.ReviewEditHistoryId);
            entity.ToTable("REVIEW_EDIT_HISTORY");
            entity.HasIndex(e => e.ReviewId, "IX_REVIEW_EDIT_HISTORY_REVIEW_ID");

            entity.Property(e => e.ReviewEditHistoryId).HasMaxLength(50).HasColumnName("reviewEditHistoryId");
            entity.Property(e => e.ReviewId).HasMaxLength(50).HasColumnName("reviewId");
            entity.Property(e => e.OldRating).HasColumnName("oldRating");
            entity.Property(e => e.NewRating).HasColumnName("newRating");
            entity.Property(e => e.OldComment).HasMaxLength(1000).HasColumnName("oldComment");
            entity.Property(e => e.NewComment).HasMaxLength(1000).HasColumnName("newComment");
            entity.Property(e => e.EditedAt).HasDefaultValueSql("SYSUTCDATETIME()").HasColumnName("editedAt");

            entity.HasOne(d => d.Review).WithMany(p => p.EditHistories)
                .HasForeignKey(d => d.ReviewId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REVIEW_EDIT_HISTORY_REVIEW");
        });

        modelBuilder.Entity<ReviewModerationHistory>(entity =>
        {
            entity.HasKey(e => e.ModerationHistoryId);
            entity.ToTable("REVIEW_MODERATION_HISTORY");
            entity.HasIndex(e => e.ReviewId, "IX_REVIEW_MODERATION_HISTORY_REVIEW_ID");

            entity.Property(e => e.ModerationHistoryId).HasMaxLength(50).HasColumnName("moderationHistoryId");
            entity.Property(e => e.ReviewId).HasMaxLength(50).HasColumnName("reviewId");
            entity.Property(e => e.OldStatus).HasMaxLength(30).HasColumnName("oldStatus");
            entity.Property(e => e.NewStatus).HasMaxLength(30).HasColumnName("newStatus");
            entity.Property(e => e.ModeratorId).HasMaxLength(50).HasColumnName("moderatorId");
            entity.Property(e => e.RejectedReason).HasMaxLength(1000).HasColumnName("rejectedReason");
            entity.Property(e => e.ModeratedAt).HasDefaultValueSql("SYSUTCDATETIME()").HasColumnName("moderatedAt");

            entity.HasOne(d => d.Review).WithMany(p => p.ModerationHistories)
                .HasForeignKey(d => d.ReviewId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REVIEW_MODERATION_HISTORY_REVIEW");
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

        modelBuilder.Entity<RefundClaim>(entity =>
        {
            entity.HasKey(e => e.RefundClaimId);
            entity.ToTable("REFUND_CLAIM");
            entity.HasIndex(e => e.RefundId, "UQ_REFUND_CLAIM_REFUND").IsUnique();
            entity.HasIndex(e => e.CustomerProfileId, "IX_REFUND_CLAIM_CUSTOMER_PROFILE_ID");
            entity.HasIndex(e => new { e.ClaimStatus, e.ExpiresAt }, "IX_REFUND_CLAIM_STATUS");
            entity.Property(e => e.RefundClaimId).HasMaxLength(50).HasColumnName("refundClaimId");
            entity.Property(e => e.RefundId).HasMaxLength(50).HasColumnName("refundId");
            entity.Property(e => e.CustomerProfileId).HasMaxLength(50).HasColumnName("customerProfileId");
            entity.Property(e => e.BankCode).HasMaxLength(20).HasColumnName("bankCode");
            entity.Property(e => e.ClaimStatus).HasMaxLength(30).HasColumnName("claimStatus");
            entity.Property(e => e.AccountValidationStatus).HasMaxLength(30).HasColumnName("accountValidationStatus");
            entity.Property(e => e.BankAccountEncrypted).HasColumnName("bankAccountEncrypted");
            entity.Property(e => e.BankAccountLast4).HasMaxLength(4).HasColumnName("bankAccountLast4");
            entity.Property(e => e.AccountHolderNameEncrypted).HasColumnName("accountHolderNameEncrypted");
            entity.Property(e => e.VerifiedAccountHolderNameEncrypted).HasColumnName("verifiedAccountHolderNameEncrypted");
            entity.Property(e => e.VerificationProvider).HasMaxLength(100).HasColumnName("verificationProvider");
            entity.Property(e => e.VerificationReferenceCode).HasMaxLength(255).HasColumnName("verificationReferenceCode");
            entity.Property(e => e.VerificationFailureReason).HasMaxLength(1000).HasColumnName("verificationFailureReason");
            entity.Property(e => e.ExpiresAt).HasColumnName("expiresAt");
            entity.Property(e => e.SubmittedAt).HasColumnName("submittedAt");
            entity.Property(e => e.ProcessingAt).HasColumnName("processingAt");
            entity.Property(e => e.CompletedAt).HasColumnName("completedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedAt");
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken().HasColumnName("rowVersion");
            entity.HasOne(e => e.Refund).WithOne(e => e.RefundClaim)
                .HasForeignKey<RefundClaim>(e => e.RefundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REFUND_CLAIM_REFUND");
            entity.HasOne(e => e.CustomerProfile).WithMany(e => e.RefundClaims)
                .HasForeignKey(e => e.CustomerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REFUND_CLAIM_CUSTOMER_PROFILE");
            entity.HasOne(e => e.Bank).WithMany(e => e.RefundClaims)
                .HasForeignKey(e => e.BankCode)
                .HasConstraintName("FK_REFUND_CLAIM_BANK_DIRECTORY");
        });

        modelBuilder.Entity<RefundClaimToken>(entity =>
        {
            entity.HasKey(e => e.RefundClaimTokenId);
            entity.ToTable("REFUND_CLAIM_TOKEN");
            entity.HasIndex(e => e.TokenHash, "UQ_REFUND_CLAIM_TOKEN_HASH").IsUnique();
            entity.HasIndex(e => new { e.RefundClaimId, e.ExpiresAt }, "IX_REFUND_CLAIM_TOKEN_CLAIM");
            entity.Property(e => e.RefundClaimTokenId).HasMaxLength(50).HasColumnName("refundClaimTokenId");
            entity.Property(e => e.RefundClaimId).HasMaxLength(50).HasColumnName("refundClaimId");
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsFixedLength().HasColumnName("tokenHash");
            entity.Property(e => e.ExpiresAt).HasColumnName("expiresAt");
            entity.Property(e => e.UsedAt).HasColumnName("usedAt");
            entity.Property(e => e.RevokedAt).HasColumnName("revokedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.HasOne(e => e.RefundClaim).WithMany(e => e.Tokens)
                .HasForeignKey(e => e.RefundClaimId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REFUND_CLAIM_TOKEN_CLAIM");
        });

        modelBuilder.Entity<CustomerRefundRequest>(entity =>
        {
            entity.HasKey(e => e.CustomerRefundRequestId);
            entity.ToTable("CUSTOMER_REFUND_REQUEST");
            entity.HasIndex(e => new { e.CustomerProfileId, e.RequestStatus, e.CreatedAt },
                "IX_CUSTOMER_REFUND_REQUEST_CUSTOMER_STATUS");
            entity.Property(e => e.CustomerRefundRequestId).HasMaxLength(50).HasColumnName("customerRefundRequestId");
            entity.Property(e => e.RefundId).HasMaxLength(50).HasColumnName("refundId");
            entity.Property(e => e.CustomerProfileId).HasMaxLength(50).HasColumnName("customerProfileId");
            entity.Property(e => e.TicketId).HasMaxLength(50).HasColumnName("ticketId");
            entity.Property(e => e.RequestReason).HasMaxLength(1000).HasColumnName("requestReason");
            entity.Property(e => e.RequestStatus).HasMaxLength(30).HasColumnName("requestStatus");
            entity.Property(e => e.ProcessedByUserId).HasMaxLength(50).HasColumnName("processedByUserId");
            entity.Property(e => e.ProcessedAt).HasColumnName("processedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.HasOne(e => e.Refund).WithMany(e => e.CustomerRefundRequests)
                .HasForeignKey(e => e.RefundId).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CUSTOMER_REFUND_REQUEST_REFUND");
            entity.HasOne(e => e.CustomerProfile).WithMany(e => e.CustomerRefundRequests)
                .HasForeignKey(e => e.CustomerProfileId).OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CUSTOMER_REFUND_REQUEST_CUSTOMER_PROFILE");
            entity.HasOne(e => e.Ticket).WithMany()
                .HasForeignKey(e => e.TicketId)
                .HasConstraintName("FK_CUSTOMER_REFUND_REQUEST_TICKET");
            entity.HasOne(e => e.ProcessedByUser).WithMany(e => e.ProcessedCustomerRefundRequests)
                .HasForeignKey(e => e.ProcessedByUserId)
                .HasConstraintName("FK_CUSTOMER_REFUND_REQUEST_PROCESSED_BY_USER");
        });

        modelBuilder.Entity<ManualRefundProcess>(entity =>
        {
            entity.HasKey(e => e.ManualRefundProcessId);
            entity.ToTable("MANUAL_REFUND_PROCESS");
            entity.HasIndex(e => e.RefundId, "UQ_MANUAL_REFUND_PROCESS_REFUND").IsUnique();
            entity.HasIndex(e => e.RefundClaimId, "UQ_MANUAL_REFUND_PROCESS_CLAIM").IsUnique();
            entity.HasIndex(e => new { e.ProcessStatus, e.CreatedAt }, "IX_MANUAL_REFUND_PROCESS_STATUS_CREATED");
            entity.HasIndex(e => e.BankTransactionCode, "UX_MANUAL_REFUND_BANK_TRANSACTION_CODE")
                .IsUnique().HasFilter("([bankTransactionCode] IS NOT NULL)");
            entity.Property(e => e.ManualRefundProcessId).HasMaxLength(50).HasColumnName("manualRefundProcessId");
            entity.Property(e => e.RefundId).HasMaxLength(50).HasColumnName("refundId");
            entity.Property(e => e.RefundClaimId).HasMaxLength(50).HasColumnName("refundClaimId");
            entity.Property(e => e.AssignedToUserId).HasMaxLength(50).HasColumnName("assignedToUserId");
            entity.Property(e => e.ProcessStatus).HasMaxLength(30).HasColumnName("processStatus");
            entity.Property(e => e.BankTransactionCode).HasMaxLength(255).HasColumnName("bankTransactionCode");
            entity.Property(e => e.TransferredAmount).HasColumnType("decimal(18, 2)").HasColumnName("transferredAmount");
            entity.Property(e => e.ProofUrl).HasMaxLength(1000).HasColumnName("proofUrl");
            entity.Property(e => e.AdminNote).HasMaxLength(1000).HasColumnName("adminNote");
            entity.Property(e => e.AssignedAt).HasColumnName("assignedAt");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken().HasColumnName("rowVersion");
            entity.HasOne(e => e.Refund).WithOne(e => e.ManualRefundProcess)
                .HasForeignKey<ManualRefundProcess>(e => e.RefundId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MANUAL_REFUND_PROCESS_REFUND");
            entity.HasOne(e => e.RefundClaim).WithOne(e => e.ManualRefundProcess)
                .HasForeignKey<ManualRefundProcess>(e => e.RefundClaimId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MANUAL_REFUND_PROCESS_CLAIM");
            entity.HasOne(e => e.AssignedToUser).WithMany(e => e.AssignedManualRefundProcesses)
                .HasForeignKey(e => e.AssignedToUserId)
                .HasConstraintName("FK_MANUAL_REFUND_PROCESS_ASSIGNED_USER");
          });

        modelBuilder.Entity<RefundCustomerConfirmation>(entity =>
        {
            entity.HasKey(e => e.RefundCustomerConfirmationId);
            entity.ToTable("REFUND_CUSTOMER_CONFIRMATION");
            entity.HasIndex(e => e.ManualRefundProcessId, "UQ_REFUND_CUSTOMER_CONFIRMATION_PROCESS").IsUnique();
            entity.HasIndex(e => e.TokenHash, "UQ_REFUND_CUSTOMER_CONFIRMATION_TOKEN").IsUnique();
            entity.HasIndex(e => new { e.Status, e.ExpiresAt }, "IX_REFUND_CUSTOMER_CONFIRMATION_STATUS");
            entity.Property(e => e.RefundCustomerConfirmationId).HasMaxLength(50).HasColumnName("refundCustomerConfirmationId");
            entity.Property(e => e.ManualRefundProcessId).HasMaxLength(50).HasColumnName("manualRefundProcessId");
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsFixedLength().HasColumnName("tokenHash");
            entity.Property(e => e.Status).HasMaxLength(30).HasColumnName("status");
            entity.Property(e => e.ExpiresAt).HasColumnName("expiresAt");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.RevokedAt).HasColumnName("revokedAt");
            entity.HasOne(e => e.ManualRefundProcess).WithOne(e => e.CustomerConfirmation)
                .HasForeignKey<RefundCustomerConfirmation>(e => e.ManualRefundProcessId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_REFUND_CUSTOMER_CONFIRMATION_PROCESS");
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
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("('Pending')")
                .HasColumnName("status");
            entity.Property(e => e.EditCount)
                .HasDefaultValue(0)
                .HasColumnName("editCount");
            entity.Property(e => e.RejectedReason)
                .HasMaxLength(500)
                .HasColumnName("rejectedReason");
            entity.Property(e => e.ModeratedBy)
                .HasMaxLength(50)
                .HasColumnName("moderatedBy");
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

        modelBuilder.Entity<RoleAssignmentRule>(entity =>
        {
            entity.HasKey(e => new { e.GrantorRoleId, e.GranteeRoleId })
                .HasName("PK_ROLE_ASSIGNMENT_RULE");

            entity.ToTable("ROLE_ASSIGNMENT_RULE");

            entity.HasIndex(e => e.GranteeRoleId, "IX_ROLE_ASSIGNMENT_RULE_GRANTEE");

            entity.Property(e => e.GrantorRoleId)
                .HasMaxLength(50)
                .HasColumnName("grantorRoleId");
            entity.Property(e => e.GranteeRoleId)
                .HasMaxLength(50)
                .HasColumnName("granteeRoleId");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");

            entity.HasOne(e => e.GrantorRole)
                .WithMany(e => e.GrantedAssignmentRules)
                .HasForeignKey(e => e.GrantorRoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ROLE_ASSIGNMENT_RULE_GRANTOR");

            entity.HasOne(e => e.GranteeRole)
                .WithMany(e => e.ReceivedAssignmentRules)
                .HasForeignKey(e => e.GranteeRoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ROLE_ASSIGNMENT_RULE_GRANTEE");
        });

        modelBuilder.Entity<RoleProvisioningPolicy>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK_ROLE_PROVISIONING_POLICY");

            entity.ToTable("ROLE_PROVISIONING_POLICY");

            entity.HasIndex(e => new { e.IsActive, e.IsPublicRegistrationAllowed }, "IX_ROLE_PROVISIONING_POLICY_PUBLIC");

            entity.Property(e => e.RoleId)
                .HasMaxLength(50)
                .HasColumnName("roleId");
            entity.Property(e => e.ProfileKind)
                .HasMaxLength(20)
                .HasColumnName("profileKind");
            entity.Property(e => e.RequiresCinema)
                .HasDefaultValue(false)
                .HasColumnName("requiresCinema");
            entity.Property(e => e.DefaultStaffPosition)
                .HasMaxLength(100)
                .HasColumnName("defaultStaffPosition");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.IsPublicRegistrationAllowed)
                .HasDefaultValue(false)
                .HasColumnName("isPublicRegistrationAllowed");

            entity.HasOne(e => e.Role)
                .WithOne(e => e.ProvisioningPolicy)
                .HasForeignKey<RoleProvisioningPolicy>(e => e.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ROLE_PROVISIONING_POLICY_ROLE");
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

            entity.HasIndex(e => new { e.LockedByUserId, e.LockedUntil }, "IX_SHOWTIME_SEAT_LOCKED_USER_UNTIL");

            entity.HasIndex(e => new { e.ShowtimeId, e.SeatStatus, e.LockedUntil }, "IX_SHOWTIME_SEAT_SHOWTIME_STATUS_LOCKED");

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
            entity.Property(e => e.QrCode)
                .HasMaxLength(450)
                .HasColumnName("qrCode");
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
            entity.Property(e => e.SpamViolationCount)
                .HasDefaultValue(0)
                .HasColumnName("spamViolationCount");
            entity.Property(e => e.IsBlocked)
                .HasDefaultValue(false)
                .HasColumnName("isBlocked");
            entity.Property(e => e.BlockedUntil).HasColumnName("blockedUntil");

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
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .IsRequired(false)
                .HasDefaultValue("EVENT")
                .HasColumnName("category");
            entity.Property(e => e.ApplicableScope)
                .HasMaxLength(50)
                .IsRequired(false)
                .HasDefaultValue("TOTAL_ORDER")
                .HasColumnName("applicableScope");
            entity.Property(e => e.TargetType)
                .HasMaxLength(50)
                .IsRequired(false)
                .HasDefaultValue("ALL_CUSTOMERS")
                .HasColumnName("targetType");
            entity.Property(e => e.TargetCustomerIds)
                .HasColumnName("targetCustomerIds");
            entity.Property(e => e.SpecificFbItemIds)
                .HasColumnName("specificFbItemIds");
        });

        modelBuilder.Entity<VoucherUsage>(entity =>
        {
            entity.HasKey(e => e.VoucherUsageId).HasName("PK__VOUCHER___043EFB915245BCD0");

            entity.ToTable("VOUCHER_USAGE");

            entity.HasIndex(e => e.BookingId, "UQ_VOUCHER_USAGE_BOOKING").IsUnique();

            entity.HasIndex(e => e.CustomerVoucherId, "UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER")
                .IsUnique()
                .HasFilter("[customerVoucherId] IS NOT NULL AND [usageStatus] <> 'CANCELLED'");

            entity.Property(e => e.VoucherUsageId)
                .HasMaxLength(50)
                .HasColumnName("voucherUsageId");
            entity.Property(e => e.BookingId)
                .HasMaxLength(50)
                .HasColumnName("bookingId");
            entity.Property(e => e.CustomerProfileId)
                .HasMaxLength(50)
                .HasColumnName("customerProfileId");
            entity.Property(e => e.CustomerVoucherId)
                .HasMaxLength(50)
                .HasColumnName("customerVoucherId");
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

            entity.HasOne(d => d.CustomerVoucher).WithMany(p => p.VoucherUsages)
                .HasForeignKey(d => d.CustomerVoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VOUCHER_USAGE_CUSTOMER_VOUCHER");

            entity.HasOne(d => d.Voucher).WithMany(p => p.VoucherUsages)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VOUCHER_USAGE_VOUCHER");
        });

        modelBuilder.Entity<CustomerVoucher>(entity =>
        {
            entity.HasKey(e => e.CustomerVoucherId);
            entity.ToTable("CUSTOMER_VOUCHER");

            entity.Property(e => e.CustomerVoucherId)
                .HasMaxLength(50)
                .HasColumnName("customerVoucherId");

            entity.Property(e => e.CustomerProfileId)
                .HasMaxLength(50)
                .HasColumnName("customerProfileId");

            entity.Property(e => e.VoucherId)
                .HasMaxLength(50)
                .HasColumnName("voucherId");

            entity.Property(e => e.ClaimedAt)
                .HasColumnName("claimedAt");

            entity.Property(e => e.IsUsed)
                .HasColumnName("isUsed");

            entity.Property(e => e.UsedAt)
                .HasColumnName("usedAt");

            entity.HasOne(d => d.CustomerProfile)
                .WithMany()
                .HasForeignKey(d => d.CustomerProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CUSTOMER_VOUCHER_CUSTOMER_PROFILE");

            entity.HasOne(d => d.Voucher)
                .WithMany()
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CUSTOMER_VOUCHER_VOUCHER");
        });

        modelBuilder.Entity<CancellationCompensation>(entity =>
        {
            entity.HasKey(e => e.CancellationCompensationId);
            entity.ToTable("CANCELLATION_COMPENSATION");

            entity.HasIndex(e => e.SourceBookingId, "UQ_CANCELLATION_COMPENSATION_BOOKING")
                .IsUnique();
            entity.HasIndex(e => e.ShowtimeCancellationId, "IX_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION");
            entity.HasIndex(e => new { e.CustomerProfileId, e.Status }, "IX_CANCELLATION_COMPENSATION_CUSTOMER_STATUS");

            entity.Property(e => e.CancellationCompensationId)
                .HasMaxLength(50)
                .HasColumnName("cancellationCompensationId");
            entity.Property(e => e.SourceBookingId)
                .HasMaxLength(50)
                .HasColumnName("sourceBookingId");
            entity.Property(e => e.ShowtimeCancellationId)
                .HasMaxLength(50)
                .HasColumnName("showtimeCancellationId");
            entity.Property(e => e.CustomerProfileId)
                .HasMaxLength(50)
                .HasColumnName("customerProfileId");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("ISSUED")
                .HasColumnName("status");
            entity.Property(e => e.PolicyVersion)
                .HasMaxLength(50)
                .HasColumnName("policyVersion");
            entity.Property(e => e.IssuedAt)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("issuedAt");
            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expiresAt");

            entity.HasOne(e => e.SourceBooking)
                .WithOne(e => e.SourceCancellationCompensation)
                .HasForeignKey<CancellationCompensation>(e => e.SourceBookingId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_CANCELLATION_COMPENSATION_BOOKING");
            entity.HasOne(e => e.ShowtimeCancellation)
                .WithMany(e => e.Compensations)
                .HasForeignKey(e => e.ShowtimeCancellationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION");
            entity.HasOne(e => e.CustomerProfile)
                .WithMany(e => e.CancellationCompensations)
                .HasForeignKey(e => e.CustomerProfileId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_CANCELLATION_COMPENSATION_CUSTOMER_PROFILE");
        });

        modelBuilder.Entity<CompensationTicket>(entity =>
        {
            entity.HasKey(e => e.CompensationTicketId);
            entity.ToTable("COMPENSATION_TICKET");

            entity.HasIndex(e => e.VoucherCode, "UQ_COMPENSATION_TICKET_CODE")
                .IsUnique();
            entity.HasIndex(e => e.CancellationCompensationId, "IX_COMPENSATION_TICKET_COMPENSATION");
            entity.HasIndex(e => e.ReservedBookingId, "IX_COMPENSATION_TICKET_RESERVED_BOOKING");
            entity.HasIndex(e => e.ReservedBookingSeatId, "UQ_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT")
                .IsUnique()
                .HasFilter("[reservedBookingSeatId] IS NOT NULL");

            entity.Property(e => e.CompensationTicketId)
                .HasMaxLength(50)
                .HasColumnName("compensationTicketId");
            entity.Property(e => e.CancellationCompensationId)
                .HasMaxLength(50)
                .HasColumnName("cancellationCompensationId");
            entity.Property(e => e.VoucherCode)
                .HasMaxLength(100)
                .HasColumnName("voucherCode");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("ISSUED")
                .HasColumnName("status");
            entity.Property(e => e.ReservedBookingId)
                .HasMaxLength(50)
                .HasColumnName("reservedBookingId");
            entity.Property(e => e.ReservedBookingSeatId)
                .HasMaxLength(50)
                .HasColumnName("reservedBookingSeatId");
            entity.Property(e => e.ReservedAt)
                .HasColumnName("reservedAt");
            entity.Property(e => e.RedeemedAt)
                .HasColumnName("redeemedAt");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowVersion");

            entity.HasOne(e => e.CancellationCompensation)
                .WithMany(e => e.Tickets)
                .HasForeignKey(e => e.CancellationCompensationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_COMPENSATION_TICKET_COMPENSATION");
            entity.HasOne(e => e.ReservedBooking)
                .WithMany(e => e.ReservedCompensationTickets)
                .HasForeignKey(e => e.ReservedBookingId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_COMPENSATION_TICKET_RESERVED_BOOKING");
            entity.HasOne(e => e.ReservedBookingSeat)
                .WithOne(e => e.CompensationTicket)
                .HasForeignKey<CompensationTicket>(e => e.ReservedBookingSeatId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT");
        });

        modelBuilder.Entity<CompensationCombo>(entity =>
        {
            entity.HasKey(e => e.CompensationComboId);
            entity.ToTable("COMPENSATION_COMBO");

            entity.HasIndex(e => e.CancellationCompensationId, "UQ_COMPENSATION_COMBO_COMPENSATION")
                .IsUnique();
            entity.HasIndex(e => e.VoucherCode, "UQ_COMPENSATION_COMBO_CODE")
                .IsUnique();

            entity.Property(e => e.CompensationComboId)
                .HasMaxLength(50)
                .HasColumnName("compensationComboId");
            entity.Property(e => e.CancellationCompensationId)
                .HasMaxLength(50)
                .HasColumnName("cancellationCompensationId");
            entity.Property(e => e.VoucherCode)
                .HasMaxLength(100)
                .HasColumnName("voucherCode");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255)
                .HasColumnName("displayName");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("ISSUED")
                .HasColumnName("status");
            entity.Property(e => e.RedeemedAt)
                .HasColumnName("redeemedAt");
            entity.Property(e => e.RedeemedAtCinemaId)
                .HasMaxLength(50)
                .HasColumnName("redeemedAtCinemaId");
            entity.Property(e => e.RedeemedByStaffProfileId)
                .HasMaxLength(50)
                .HasColumnName("redeemedByStaffProfileId");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("rowVersion");

            entity.HasOne(e => e.CancellationCompensation)
                .WithOne(e => e.Combo)
                .HasForeignKey<CompensationCombo>(e => e.CancellationCompensationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_COMPENSATION_COMBO_COMPENSATION");
            entity.HasOne(e => e.RedeemedAtCinema)
                .WithMany(e => e.RedeemedCompensationCombos)
                .HasForeignKey(e => e.RedeemedAtCinemaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_COMPENSATION_COMBO_CINEMA");
            entity.HasOne(e => e.RedeemedByStaffProfile)
                .WithMany(e => e.RedeemedCompensationCombos)
                .HasForeignKey(e => e.RedeemedByStaffProfileId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_COMPENSATION_COMBO_STAFF_PROFILE");
        });

        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(dateTimeConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableDateTimeConverter);
            }
        }

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
