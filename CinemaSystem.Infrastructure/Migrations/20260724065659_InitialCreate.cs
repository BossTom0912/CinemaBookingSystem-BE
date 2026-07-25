using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CinemaSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BANK_DIRECTORY",
                columns: table => new
                {
                    bankCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bankBin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    shortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    supportsAccountInquiry = table.Column<bool>(type: "boolean", nullable: false),
                    supportsPayout = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BANK_DIRECTORY", x => x.bankCode);
                });

            migrationBuilder.CreateTable(
                name: "BANNER",
                columns: table => new
                {
                    bannerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    imageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    linkUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    bannerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    displayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    isActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BANNER", x => x.bannerId);
                });

            migrationBuilder.CreateTable(
                name: "CINEMA",
                columns: table => new
                {
                    cinemaId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cinemaName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cinemaStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ACTIVE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CINEMA__4E679F684FB25BEB", x => x.cinemaId);
                });

            migrationBuilder.CreateTable(
                name: "FB_ITEM",
                columns: table => new
                {
                    fbItemId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    itemName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    itemStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "AVAILABLE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__FB_ITEM__B91DF1DD80E826D9", x => x.fbItemId);
                });

            migrationBuilder.CreateTable(
                name: "GENRE",
                columns: table => new
                {
                    genreId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GENRE", x => x.genreId);
                });

            migrationBuilder.CreateTable(
                name: "LANGUAGE",
                columns: table => new
                {
                    languageId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LANGUAGE", x => x.languageId);
                });

            migrationBuilder.CreateTable(
                name: "PAYMENT_PROVIDER",
                columns: table => new
                {
                    paymentProviderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    providerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    apiEndpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    providerStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ACTIVE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PAYMENT___BD97D5097BEA8EB1", x => x.paymentProviderId);
                });

            migrationBuilder.CreateTable(
                name: "ROLE",
                columns: table => new
                {
                    roleId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    roleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ROLE__CD98462A6C3AD81E", x => x.roleId);
                });

            migrationBuilder.CreateTable(
                name: "SEAT_TYPE",
                columns: table => new
                {
                    seatTypeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    typeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    extraFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SEAT_TYP__0DE1222D8F93942B", x => x.seatTypeId);
                });

            migrationBuilder.CreateTable(
                name: "VOUCHER",
                columns: table => new
                {
                    voucherId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucherCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    discountType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    discountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    usageLimit = table.Column<int>(type: "integer", nullable: false),
                    startDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    endDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    voucherStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ACTIVE"),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    imageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    minOrderAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    maxDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    perCustomerLimit = table.Column<int>(type: "integer", nullable: true),
                    usedCount = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "EVENT"),
                    applicableScope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "TOTAL_ORDER"),
                    targetType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "ALL_CUSTOMERS"),
                    targetCustomerIds = table.Column<string>(type: "text", nullable: true),
                    specificFbItemIds = table.Column<string>(type: "text", nullable: true),
                    isPrivate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    requiredTicketCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__VOUCHER__F53389E98930B69A", x => x.voucherId);
                });

            migrationBuilder.CreateTable(
                name: "ROOM",
                columns: table => new
                {
                    roomId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cinemaId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    roomName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    roomStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ACTIVE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ROOM__6C3BF5BE71CF940F", x => x.roomId);
                    table.ForeignKey(
                        name: "FK_ROOM_CINEMA",
                        column: x => x.cinemaId,
                        principalTable: "CINEMA",
                        principalColumn: "cinemaId");
                });

            migrationBuilder.CreateTable(
                name: "CINEMA_FB_INVENTORY",
                columns: table => new
                {
                    cinemaInventoryId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cinemaId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fbItemId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CINEMA_F__5F0134C6DE981B97", x => x.cinemaInventoryId);
                    table.ForeignKey(
                        name: "FK_CINEMA_FB_INVENTORY_CINEMA",
                        column: x => x.cinemaId,
                        principalTable: "CINEMA",
                        principalColumn: "cinemaId");
                    table.ForeignKey(
                        name: "FK_CINEMA_FB_INVENTORY_FB_ITEM",
                        column: x => x.fbItemId,
                        principalTable: "FB_ITEM",
                        principalColumn: "fbItemId");
                });

            migrationBuilder.CreateTable(
                name: "MOVIE",
                columns: table => new
                {
                    movieId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    durationMinutes = table.Column<int>(type: "integer", nullable: false),
                    languageId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    releaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ageRating = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    posterUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    trailerUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    bannerUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Director = table.Column<string>(type: "text", nullable: true),
                    highlight = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    movieStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "COMING_SOON"),
                    viewCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    averageRating = table.Column<decimal>(type: "numeric(3,2)", nullable: false, defaultValue: 0.0m),
                    totalReviews = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    totalViews = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dailyViews = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__MOVIE__42EB374E18A44435", x => x.movieId);
                    table.ForeignKey(
                        name: "FK_MOVIE_LANGUAGE",
                        column: x => x.languageId,
                        principalTable: "LANGUAGE",
                        principalColumn: "languageId");
                });

            migrationBuilder.CreateTable(
                name: "ROLE_ASSIGNMENT_RULE",
                columns: table => new
                {
                    grantorRoleId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granteeRoleId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLE_ASSIGNMENT_RULE", x => new { x.grantorRoleId, x.granteeRoleId });
                    table.ForeignKey(
                        name: "FK_ROLE_ASSIGNMENT_RULE_GRANTEE",
                        column: x => x.granteeRoleId,
                        principalTable: "ROLE",
                        principalColumn: "roleId");
                    table.ForeignKey(
                        name: "FK_ROLE_ASSIGNMENT_RULE_GRANTOR",
                        column: x => x.grantorRoleId,
                        principalTable: "ROLE",
                        principalColumn: "roleId");
                });

            migrationBuilder.CreateTable(
                name: "ROLE_PROVISIONING_POLICY",
                columns: table => new
                {
                    roleId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    profileKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requiresCinema = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    defaultStaffPosition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    isPublicRegistrationAllowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLE_PROVISIONING_POLICY", x => x.roleId);
                    table.ForeignKey(
                        name: "FK_ROLE_PROVISIONING_POLICY_ROLE",
                        column: x => x.roleId,
                        principalTable: "ROLE",
                        principalColumn: "roleId");
                });

            migrationBuilder.CreateTable(
                name: "USER",
                columns: table => new
                {
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    roleId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    passwordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING_VERIFICATION"),
                    emailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    spamViolationCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    isBlocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    blockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__USER__CB9A1CFF4C01E85A", x => x.userId);
                    table.ForeignKey(
                        name: "FK_USER_ROLE",
                        column: x => x.roleId,
                        principalTable: "ROLE",
                        principalColumn: "roleId");
                });

            migrationBuilder.CreateTable(
                name: "SEAT",
                columns: table => new
                {
                    seatId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    roomId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    seatTypeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    seatCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rowLabel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    seatNumber = table.Column<int>(type: "integer", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SEAT__BC5329EA8B0220AA", x => x.seatId);
                    table.ForeignKey(
                        name: "FK_SEAT_ROOM",
                        column: x => x.roomId,
                        principalTable: "ROOM",
                        principalColumn: "roomId");
                    table.ForeignKey(
                        name: "FK_SEAT_SEAT_TYPE",
                        column: x => x.seatTypeId,
                        principalTable: "SEAT_TYPE",
                        principalColumn: "seatTypeId");
                });

            migrationBuilder.CreateTable(
                name: "MOVIE_DAILY_VIEW",
                columns: table => new
                {
                    movieId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    viewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    viewCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVIE_DAILY_VIEW", x => new { x.movieId, x.viewDate });
                    table.ForeignKey(
                        name: "FK_MOVIE_DAILY_VIEW_MOVIE",
                        column: x => x.movieId,
                        principalTable: "MOVIE",
                        principalColumn: "movieId");
                });

            migrationBuilder.CreateTable(
                name: "MOVIE_GENRE",
                columns: table => new
                {
                    movieId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    genreId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVIE_GENRE", x => new { x.movieId, x.genreId });
                    table.ForeignKey(
                        name: "FK_MOVIE_GENRE_GENRE",
                        column: x => x.genreId,
                        principalTable: "GENRE",
                        principalColumn: "genreId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MOVIE_GENRE_MOVIE",
                        column: x => x.movieId,
                        principalTable: "MOVIE",
                        principalColumn: "movieId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MOVIE_VIEW_LOG",
                columns: table => new
                {
                    movieViewLogId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    movieId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    viewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ipAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVIE_VIEW_LOG", x => x.movieViewLogId);
                    table.ForeignKey(
                        name: "FK_MOVIE_VIEW_LOG_MOVIE",
                        column: x => x.movieId,
                        principalTable: "MOVIE",
                        principalColumn: "movieId");
                });

            migrationBuilder.CreateTable(
                name: "SHOWTIME",
                columns: table => new
                {
                    showtimeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    movieId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    roomId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    startTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    endTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    basePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "OPEN"),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SHOWTIME__B4CBD8842A34432D", x => x.showtimeId);
                    table.ForeignKey(
                        name: "FK_SHOWTIME_MOVIE",
                        column: x => x.movieId,
                        principalTable: "MOVIE",
                        principalColumn: "movieId");
                    table.ForeignKey(
                        name: "FK_SHOWTIME_ROOM",
                        column: x => x.roomId,
                        principalTable: "ROOM",
                        principalColumn: "roomId");
                });

            migrationBuilder.CreateTable(
                name: "AUDIT_LOG",
                columns: table => new
                {
                    auditLogId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entityId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    oldValue = table.Column<string>(type: "text", nullable: true),
                    newValue = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    ipAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    userAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AUDIT_LO__56A1B857E9725B26", x => x.auditLogId);
                    table.ForeignKey(
                        name: "FK_AUDIT_LOG_USER",
                        column: x => x.userId,
                        principalTable: "USER",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "CHAT_HISTORY",
                columns: table => new
                {
                    chatHistoryId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    userMessage = table.Column<string>(type: "text", nullable: false),
                    aiReplyMessage = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHAT_HISTORY", x => x.chatHistoryId);
                    table.ForeignKey(
                        name: "FK_CHAT_HISTORY_USER",
                        column: x => x.userId,
                        principalTable: "USER",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMER_PROFILE",
                columns: table => new
                {
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    memberLevel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "STANDARD"),
                    rewardPoints = table.Column<int>(type: "integer", nullable: false),
                    dateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    identityCard = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    avatarUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CUSTOMER__E68F35A03EED0092", x => x.customerProfileId);
                    table.ForeignKey(
                        name: "FK_CUSTOMER_PROFILE_USER",
                        column: x => x.userId,
                        principalTable: "USER",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "EMAIL_VERIFICATION_TOKEN",
                columns: table => new
                {
                    tokenId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isUsed = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "EMAIL_VERIFICATION"),
                    attemptCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__EMAIL_VE__AC16DB47312820A8", x => x.tokenId);
                    table.ForeignKey(
                        name: "FK_EMAIL_VERIFICATION_USER",
                        column: x => x.userId,
                        principalTable: "USER",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "REFRESH_TOKEN",
                columns: table => new
                {
                    refreshTokenId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tokenHash = table.Column<string>(type: "text", nullable: false),
                    issuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    expiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    isRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__REFRESH___FEAC95C8CD18D87E", x => x.refreshTokenId);
                    table.ForeignKey(
                        name: "FK_REFRESH_TOKEN_USER",
                        column: x => x.userId,
                        principalTable: "USER",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "STAFF_PROFILE",
                columns: table => new
                {
                    staffProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cinemaId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    hireDate = table.Column<DateOnly>(type: "date", nullable: true),
                    employmentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ACTIVE"),
                    dateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    identityCard = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    avatarUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__STAFF_PR__792B9F5817DC9A79", x => x.staffProfileId);
                    table.ForeignKey(
                        name: "FK_STAFF_PROFILE_CINEMA",
                        column: x => x.cinemaId,
                        principalTable: "CINEMA",
                        principalColumn: "cinemaId");
                    table.ForeignKey(
                        name: "FK_STAFF_PROFILE_USER",
                        column: x => x.userId,
                        principalTable: "USER",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "SHOWTIME_SEAT",
                columns: table => new
                {
                    showtimeSeatId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    showtimeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    seatId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    seatStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "AVAILABLE"),
                    lockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lockedByUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SHOWTIME__EA7AD159C6275EB6", x => x.showtimeSeatId);
                    table.ForeignKey(
                        name: "FK_SHOWTIME_SEAT_LOCKED_BY_USER",
                        column: x => x.lockedByUserId,
                        principalTable: "USER",
                        principalColumn: "userId");
                    table.ForeignKey(
                        name: "FK_SHOWTIME_SEAT_SEAT",
                        column: x => x.seatId,
                        principalTable: "SEAT",
                        principalColumn: "seatId");
                    table.ForeignKey(
                        name: "FK_SHOWTIME_SEAT_SHOWTIME",
                        column: x => x.showtimeId,
                        principalTable: "SHOWTIME",
                        principalColumn: "showtimeId");
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMER_VOUCHER",
                columns: table => new
                {
                    customerVoucherId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucherId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    claimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    isUsed = table.Column<bool>(type: "boolean", nullable: false),
                    usedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMER_VOUCHER", x => x.customerVoucherId);
                    table.ForeignKey(
                        name: "FK_CUSTOMER_VOUCHER_CUSTOMER_PROFILE",
                        column: x => x.customerProfileId,
                        principalTable: "CUSTOMER_PROFILE",
                        principalColumn: "customerProfileId");
                    table.ForeignKey(
                        name: "FK_CUSTOMER_VOUCHER_VOUCHER",
                        column: x => x.voucherId,
                        principalTable: "VOUCHER",
                        principalColumn: "voucherId");
                });

            migrationBuilder.CreateTable(
                name: "BOOKING",
                columns: table => new
                {
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    showtimeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fbFulfillmentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "NOT_REQUIRED"),
                    fbFulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fbFulfilledByStaffProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bookingStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "CREATED"),
                    totalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    compensationDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    expiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    clientRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    requestFingerprint = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    createdByStaffProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bookingChannel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ONLINE"),
                    guestName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    guestPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    guestEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BOOKING__C6D03BCD1EDCF929", x => x.bookingId);
                    table.ForeignKey(
                        name: "FK_BOOKING_CREATED_BY_STAFF",
                        column: x => x.createdByStaffProfileId,
                        principalTable: "STAFF_PROFILE",
                        principalColumn: "staffProfileId");
                    table.ForeignKey(
                        name: "FK_BOOKING_CUSTOMER_PROFILE",
                        column: x => x.customerProfileId,
                        principalTable: "CUSTOMER_PROFILE",
                        principalColumn: "customerProfileId");
                    table.ForeignKey(
                        name: "FK_BOOKING_FB_FULFILLED_BY_STAFF",
                        column: x => x.fbFulfilledByStaffProfileId,
                        principalTable: "STAFF_PROFILE",
                        principalColumn: "staffProfileId");
                    table.ForeignKey(
                        name: "FK_BOOKING_SHOWTIME",
                        column: x => x.showtimeId,
                        principalTable: "SHOWTIME",
                        principalColumn: "showtimeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SHOWTIME_CANCELLATION",
                columns: table => new
                {
                    showtimeCancellationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    showtimeId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cancelledByStaffId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    cancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    cancelledByUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__SHOWTIME__653AA5AFEB70BC94", x => x.showtimeCancellationId);
                    table.ForeignKey(
                        name: "FK_SHOWTIME_CANCELLATION_SHOWTIME",
                        column: x => x.showtimeId,
                        principalTable: "SHOWTIME",
                        principalColumn: "showtimeId");
                    table.ForeignKey(
                        name: "FK_SHOWTIME_CANCELLATION_STAFF_PROFILE",
                        column: x => x.cancelledByStaffId,
                        principalTable: "STAFF_PROFILE",
                        principalColumn: "staffProfileId");
                    table.ForeignKey(
                        name: "FK_SHOWTIME_CANCELLATION_USER",
                        column: x => x.cancelledByUserId,
                        principalTable: "USER",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "BOOKING_FB_ITEM",
                columns: table => new
                {
                    bookingFBItemId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fbItemId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BOOKING___57F09C0846290D54", x => x.bookingFBItemId);
                    table.ForeignKey(
                        name: "FK_BOOKING_FB_ITEM_BOOKING",
                        column: x => x.bookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId");
                    table.ForeignKey(
                        name: "FK_BOOKING_FB_ITEM_FB_ITEM",
                        column: x => x.fbItemId,
                        principalTable: "FB_ITEM",
                        principalColumn: "fbItemId");
                });

            migrationBuilder.CreateTable(
                name: "BOOKING_SEAT",
                columns: table => new
                {
                    bookingSeatId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    showtimeSeatId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    seatPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BOOKING___0F3B47D674E665BF", x => x.bookingSeatId);
                    table.ForeignKey(
                        name: "FK_BOOKING_SEAT_BOOKING",
                        column: x => x.bookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId");
                    table.ForeignKey(
                        name: "FK_BOOKING_SEAT_SHOWTIME_SEAT",
                        column: x => x.showtimeSeatId,
                        principalTable: "SHOWTIME_SEAT",
                        principalColumn: "showtimeSeatId");
                });

            migrationBuilder.CreateTable(
                name: "NOTIFICATION",
                columns: table => new
                {
                    notificationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    userId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    isRead = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__NOTIFICA__4BA5CEA9CB086DEB", x => x.notificationId);
                    table.ForeignKey(
                        name: "FK_NOTIFICATION_BOOKING",
                        column: x => x.bookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId");
                    table.ForeignKey(
                        name: "FK_NOTIFICATION_USER",
                        column: x => x.userId,
                        principalTable: "USER",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "PAYMENT",
                columns: table => new
                {
                    paymentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    paymentProviderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    transactionCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    paymentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    paidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    providerTransactionCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    failureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    rawCallbackPayload = table.Column<string>(type: "text", nullable: true),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PAYMENT__A0D9EFC6CE4D9F45", x => x.paymentId);
                    table.ForeignKey(
                        name: "FK_PAYMENT_BOOKING",
                        column: x => x.bookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId");
                    table.ForeignKey(
                        name: "FK_PAYMENT_PAYMENT_PROVIDER",
                        column: x => x.paymentProviderId,
                        principalTable: "PAYMENT_PROVIDER",
                        principalColumn: "paymentProviderId");
                });

            migrationBuilder.CreateTable(
                name: "REVIEW",
                columns: table => new
                {
                    reviewId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    movieId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "('Pending')"),
                    editCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rejectedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    moderatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__REVIEW__2ECD6E044225F0E6", x => x.reviewId);
                    table.ForeignKey(
                        name: "FK_REVIEW_BOOKING",
                        column: x => x.bookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId");
                    table.ForeignKey(
                        name: "FK_REVIEW_CUSTOMER_PROFILE",
                        column: x => x.customerProfileId,
                        principalTable: "CUSTOMER_PROFILE",
                        principalColumn: "customerProfileId");
                    table.ForeignKey(
                        name: "FK_REVIEW_MOVIE",
                        column: x => x.movieId,
                        principalTable: "MOVIE",
                        principalColumn: "movieId");
                });

            migrationBuilder.CreateTable(
                name: "REWARD_POINT_TRANSACTION",
                columns: table => new
                {
                    rewardTransactionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    transactionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__REWARD_P__F1F5882BD2566DA5", x => x.rewardTransactionId);
                    table.ForeignKey(
                        name: "FK_REWARD_POINT_TRANSACTION_BOOKING",
                        column: x => x.bookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId");
                    table.ForeignKey(
                        name: "FK_REWARD_POINT_TRANSACTION_CUSTOMER_PROFILE",
                        column: x => x.customerProfileId,
                        principalTable: "CUSTOMER_PROFILE",
                        principalColumn: "customerProfileId");
                });

            migrationBuilder.CreateTable(
                name: "VOUCHER_USAGE",
                columns: table => new
                {
                    voucherUsageId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucherId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerVoucherId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    usageStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "APPLIED"),
                    usedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    discountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__VOUCHER___043EFB915245BCD0", x => x.voucherUsageId);
                    table.ForeignKey(
                        name: "FK_VOUCHER_USAGE_BOOKING",
                        column: x => x.bookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId");
                    table.ForeignKey(
                        name: "FK_VOUCHER_USAGE_CUSTOMER_PROFILE",
                        column: x => x.customerProfileId,
                        principalTable: "CUSTOMER_PROFILE",
                        principalColumn: "customerProfileId");
                    table.ForeignKey(
                        name: "FK_VOUCHER_USAGE_CUSTOMER_VOUCHER",
                        column: x => x.customerVoucherId,
                        principalTable: "CUSTOMER_VOUCHER",
                        principalColumn: "customerVoucherId");
                    table.ForeignKey(
                        name: "FK_VOUCHER_USAGE_VOUCHER",
                        column: x => x.voucherId,
                        principalTable: "VOUCHER",
                        principalColumn: "voucherId");
                });

            migrationBuilder.CreateTable(
                name: "CANCELLATION_COMPENSATION",
                columns: table => new
                {
                    cancellationCompensationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sourceBookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    showtimeCancellationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ISSUED"),
                    policyVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    expiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CANCELLATION_COMPENSATION", x => x.cancellationCompensationId);
                    table.ForeignKey(
                        name: "FK_CANCELLATION_COMPENSATION_BOOKING",
                        column: x => x.sourceBookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CANCELLATION_COMPENSATION_CUSTOMER_PROFILE",
                        column: x => x.customerProfileId,
                        principalTable: "CUSTOMER_PROFILE",
                        principalColumn: "customerProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION",
                        column: x => x.showtimeCancellationId,
                        principalTable: "SHOWTIME_CANCELLATION",
                        principalColumn: "showtimeCancellationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TICKET",
                columns: table => new
                {
                    ticketId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingSeatId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    qrCode = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ticketStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "UNUSED"),
                    generatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__TICKET__3333C61084CF344A", x => x.ticketId);
                    table.ForeignKey(
                        name: "FK_TICKET_BOOKING_SEAT",
                        column: x => x.bookingSeatId,
                        principalTable: "BOOKING_SEAT",
                        principalColumn: "bookingSeatId");
                });

            migrationBuilder.CreateTable(
                name: "REFUND",
                columns: table => new
                {
                    refundId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    paymentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    paymentProviderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    showtimeCancellationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    refundAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    refundStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    refundReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    providerRefundCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    failureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    requestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    refundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__REFUND__B219848F090ED840", x => x.refundId);
                    table.ForeignKey(
                        name: "FK_REFUND_BOOKING",
                        column: x => x.bookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId");
                    table.ForeignKey(
                        name: "FK_REFUND_PAYMENT",
                        column: x => x.paymentId,
                        principalTable: "PAYMENT",
                        principalColumn: "paymentId");
                    table.ForeignKey(
                        name: "FK_REFUND_PAYMENT_PROVIDER",
                        column: x => x.paymentProviderId,
                        principalTable: "PAYMENT_PROVIDER",
                        principalColumn: "paymentProviderId");
                    table.ForeignKey(
                        name: "FK_REFUND_SHOWTIME_CANCELLATION",
                        column: x => x.showtimeCancellationId,
                        principalTable: "SHOWTIME_CANCELLATION",
                        principalColumn: "showtimeCancellationId");
                });

            migrationBuilder.CreateTable(
                name: "REVIEW_EDIT_HISTORY",
                columns: table => new
                {
                    reviewEditHistoryId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    oldRating = table.Column<int>(type: "integer", nullable: false),
                    newRating = table.Column<int>(type: "integer", nullable: false),
                    oldComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    newComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    editedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REVIEW_EDIT_HISTORY", x => x.reviewEditHistoryId);
                    table.ForeignKey(
                        name: "FK_REVIEW_EDIT_HISTORY_REVIEW",
                        column: x => x.reviewId,
                        principalTable: "REVIEW",
                        principalColumn: "reviewId");
                });

            migrationBuilder.CreateTable(
                name: "REVIEW_MODERATION_HISTORY",
                columns: table => new
                {
                    moderationHistoryId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reviewId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    oldStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    newStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    moderatorId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rejectedReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    moderatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REVIEW_MODERATION_HISTORY", x => x.moderationHistoryId);
                    table.ForeignKey(
                        name: "FK_REVIEW_MODERATION_HISTORY_REVIEW",
                        column: x => x.reviewId,
                        principalTable: "REVIEW",
                        principalColumn: "reviewId");
                });

            migrationBuilder.CreateTable(
                name: "COMPENSATION_COMBO",
                columns: table => new
                {
                    compensationComboId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cancellationCompensationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucherCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    displayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ISSUED"),
                    redeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemedAtCinemaId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    redeemedByStaffProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPENSATION_COMBO", x => x.compensationComboId);
                    table.ForeignKey(
                        name: "FK_COMPENSATION_COMBO_CINEMA",
                        column: x => x.redeemedAtCinemaId,
                        principalTable: "CINEMA",
                        principalColumn: "cinemaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPENSATION_COMBO_COMPENSATION",
                        column: x => x.cancellationCompensationId,
                        principalTable: "CANCELLATION_COMPENSATION",
                        principalColumn: "cancellationCompensationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_COMPENSATION_COMBO_STAFF_PROFILE",
                        column: x => x.redeemedByStaffProfileId,
                        principalTable: "STAFF_PROFILE",
                        principalColumn: "staffProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COMPENSATION_TICKET",
                columns: table => new
                {
                    compensationTicketId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cancellationCompensationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucherCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "ISSUED"),
                    reservedBookingId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reservedBookingSeatId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPENSATION_TICKET", x => x.compensationTicketId);
                    table.ForeignKey(
                        name: "FK_COMPENSATION_TICKET_COMPENSATION",
                        column: x => x.cancellationCompensationId,
                        principalTable: "CANCELLATION_COMPENSATION",
                        principalColumn: "cancellationCompensationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_COMPENSATION_TICKET_RESERVED_BOOKING",
                        column: x => x.reservedBookingId,
                        principalTable: "BOOKING",
                        principalColumn: "bookingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT",
                        column: x => x.reservedBookingSeatId,
                        principalTable: "BOOKING_SEAT",
                        principalColumn: "bookingSeatId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CHECKIN_LOG",
                columns: table => new
                {
                    checkInLogId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ticketId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    staffProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    scannedByUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scanTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    failureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rawQrCode = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CHECKIN___2243820849225C05", x => x.checkInLogId);
                    table.ForeignKey(
                        name: "FK_CHECKIN_LOG_SCANNED_BY_USER",
                        column: x => x.scannedByUserId,
                        principalTable: "USER",
                        principalColumn: "userId");
                    table.ForeignKey(
                        name: "FK_CHECKIN_LOG_STAFF_PROFILE",
                        column: x => x.staffProfileId,
                        principalTable: "STAFF_PROFILE",
                        principalColumn: "staffProfileId");
                    table.ForeignKey(
                        name: "FK_CHECKIN_LOG_TICKET",
                        column: x => x.ticketId,
                        principalTable: "TICKET",
                        principalColumn: "ticketId");
                });

            migrationBuilder.CreateTable(
                name: "CUSTOMER_REFUND_REQUEST",
                columns: table => new
                {
                    customerRefundRequestId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    refundId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ticketId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    requestReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requestStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    processedByUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    processedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUSTOMER_REFUND_REQUEST", x => x.customerRefundRequestId);
                    table.ForeignKey(
                        name: "FK_CUSTOMER_REFUND_REQUEST_CUSTOMER_PROFILE",
                        column: x => x.customerProfileId,
                        principalTable: "CUSTOMER_PROFILE",
                        principalColumn: "customerProfileId");
                    table.ForeignKey(
                        name: "FK_CUSTOMER_REFUND_REQUEST_PROCESSED_BY_USER",
                        column: x => x.processedByUserId,
                        principalTable: "USER",
                        principalColumn: "userId");
                    table.ForeignKey(
                        name: "FK_CUSTOMER_REFUND_REQUEST_REFUND",
                        column: x => x.refundId,
                        principalTable: "REFUND",
                        principalColumn: "refundId");
                    table.ForeignKey(
                        name: "FK_CUSTOMER_REFUND_REQUEST_TICKET",
                        column: x => x.ticketId,
                        principalTable: "TICKET",
                        principalColumn: "ticketId");
                });

            migrationBuilder.CreateTable(
                name: "REFUND_CLAIM",
                columns: table => new
                {
                    refundClaimId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    refundId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customerProfileId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bankCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    claimStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    accountValidationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    bankAccountEncrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    bankAccountLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    accountHolderNameEncrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    verifiedAccountHolderNameEncrypted = table.Column<byte[]>(type: "bytea", nullable: true),
                    verificationProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verificationReferenceCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    verificationFailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    expiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REFUND_CLAIM", x => x.refundClaimId);
                    table.ForeignKey(
                        name: "FK_REFUND_CLAIM_BANK_DIRECTORY",
                        column: x => x.bankCode,
                        principalTable: "BANK_DIRECTORY",
                        principalColumn: "bankCode");
                    table.ForeignKey(
                        name: "FK_REFUND_CLAIM_CUSTOMER_PROFILE",
                        column: x => x.customerProfileId,
                        principalTable: "CUSTOMER_PROFILE",
                        principalColumn: "customerProfileId");
                    table.ForeignKey(
                        name: "FK_REFUND_CLAIM_REFUND",
                        column: x => x.refundId,
                        principalTable: "REFUND",
                        principalColumn: "refundId");
                });

            migrationBuilder.CreateTable(
                name: "MANUAL_REFUND_PROCESS",
                columns: table => new
                {
                    manualRefundProcessId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    refundId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    refundClaimId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assignedToUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    processStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    bankTransactionCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    transferredAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    proofUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    adminNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    assignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MANUAL_REFUND_PROCESS", x => x.manualRefundProcessId);
                    table.ForeignKey(
                        name: "FK_MANUAL_REFUND_PROCESS_ASSIGNED_USER",
                        column: x => x.assignedToUserId,
                        principalTable: "USER",
                        principalColumn: "userId");
                    table.ForeignKey(
                        name: "FK_MANUAL_REFUND_PROCESS_CLAIM",
                        column: x => x.refundClaimId,
                        principalTable: "REFUND_CLAIM",
                        principalColumn: "refundClaimId");
                    table.ForeignKey(
                        name: "FK_MANUAL_REFUND_PROCESS_REFUND",
                        column: x => x.refundId,
                        principalTable: "REFUND",
                        principalColumn: "refundId");
                });

            migrationBuilder.CreateTable(
                name: "REFUND_CLAIM_TOKEN",
                columns: table => new
                {
                    refundClaimTokenId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    refundClaimId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    expiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REFUND_CLAIM_TOKEN", x => x.refundClaimTokenId);
                    table.ForeignKey(
                        name: "FK_REFUND_CLAIM_TOKEN_CLAIM",
                        column: x => x.refundClaimId,
                        principalTable: "REFUND_CLAIM",
                        principalColumn: "refundClaimId");
                });

            migrationBuilder.CreateTable(
                name: "REFUND_CUSTOMER_CONFIRMATION",
                columns: table => new
                {
                    refundCustomerConfirmationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    manualRefundProcessId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REFUND_CUSTOMER_CONFIRMATION", x => x.refundCustomerConfirmationId);
                    table.ForeignKey(
                        name: "FK_REFUND_CUSTOMER_CONFIRMATION_PROCESS",
                        column: x => x.manualRefundProcessId,
                        principalTable: "MANUAL_REFUND_PROCESS",
                        principalColumn: "manualRefundProcessId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOG_USER_CREATED_AT",
                table: "AUDIT_LOG",
                columns: new[] { "userId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_BANK_DIRECTORY_BIN",
                table: "BANK_DIRECTORY",
                column: "bankBin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_CHANNEL",
                table: "BOOKING",
                column: "bookingChannel");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_CREATED_BY_STAFF_PROFILE_ID",
                table: "BOOKING",
                column: "createdByStaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_CUSTOMER_PROFILE_ID",
                table: "BOOKING",
                column: "customerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_FB_FULFILLED_BY_STAFF_PROFILE_ID",
                table: "BOOKING",
                column: "fbFulfilledByStaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_SHOWTIME_ID",
                table: "BOOKING",
                column: "showtimeId");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_STATUS",
                table: "BOOKING",
                column: "bookingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_STATUS_EXPIRED_AT",
                table: "BOOKING",
                columns: new[] { "bookingStatus", "expiredAt" });

            migrationBuilder.CreateIndex(
                name: "UX_BOOKING_CUSTOMER_CLIENT_REQUEST",
                table: "BOOKING",
                columns: new[] { "customerProfileId", "clientRequestId" },
                unique: true,
                filter: "[clientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_FB_ITEM_bookingId",
                table: "BOOKING_FB_ITEM",
                column: "bookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_FB_ITEM_fbItemId",
                table: "BOOKING_FB_ITEM",
                column: "fbItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BOOKING_SEAT_bookingId",
                table: "BOOKING_SEAT",
                column: "bookingId");

            migrationBuilder.CreateIndex(
                name: "UQ_BOOKING_SEAT_SHOWTIME_SEAT",
                table: "BOOKING_SEAT",
                column: "showtimeSeatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CANCELLATION_COMPENSATION_CUSTOMER_STATUS",
                table: "CANCELLATION_COMPENSATION",
                columns: new[] { "customerProfileId", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_CANCELLATION_COMPENSATION_SHOWTIME_CANCELLATION",
                table: "CANCELLATION_COMPENSATION",
                column: "showtimeCancellationId");

            migrationBuilder.CreateIndex(
                name: "UQ_CANCELLATION_COMPENSATION_BOOKING",
                table: "CANCELLATION_COMPENSATION",
                column: "sourceBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CHAT_HISTORY_userId",
                table: "CHAT_HISTORY",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_CHECKIN_LOG_RAW_QR_CODE",
                table: "CHECKIN_LOG",
                column: "rawQrCode",
                filter: "([rawQrCode] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CHECKIN_LOG_SCANNED_BY_USER_TIME",
                table: "CHECKIN_LOG",
                columns: new[] { "scannedByUserId", "scanTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CHECKIN_LOG_staffProfileId",
                table: "CHECKIN_LOG",
                column: "staffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CHECKIN_LOG_TICKET_ID",
                table: "CHECKIN_LOG",
                column: "ticketId");

            migrationBuilder.CreateIndex(
                name: "IX_CINEMA_FB_INVENTORY_fbItemId",
                table: "CINEMA_FB_INVENTORY",
                column: "fbItemId");

            migrationBuilder.CreateIndex(
                name: "UQ_CINEMA_FB_INVENTORY",
                table: "CINEMA_FB_INVENTORY",
                columns: new[] { "cinemaId", "fbItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COMPENSATION_COMBO_redeemedAtCinemaId",
                table: "COMPENSATION_COMBO",
                column: "redeemedAtCinemaId");

            migrationBuilder.CreateIndex(
                name: "IX_COMPENSATION_COMBO_redeemedByStaffProfileId",
                table: "COMPENSATION_COMBO",
                column: "redeemedByStaffProfileId");

            migrationBuilder.CreateIndex(
                name: "UQ_COMPENSATION_COMBO_CODE",
                table: "COMPENSATION_COMBO",
                column: "voucherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_COMPENSATION_COMBO_COMPENSATION",
                table: "COMPENSATION_COMBO",
                column: "cancellationCompensationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COMPENSATION_TICKET_COMPENSATION",
                table: "COMPENSATION_TICKET",
                column: "cancellationCompensationId");

            migrationBuilder.CreateIndex(
                name: "IX_COMPENSATION_TICKET_RESERVED_BOOKING",
                table: "COMPENSATION_TICKET",
                column: "reservedBookingId");

            migrationBuilder.CreateIndex(
                name: "UQ_COMPENSATION_TICKET_CODE",
                table: "COMPENSATION_TICKET",
                column: "voucherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_COMPENSATION_TICKET_RESERVED_BOOKING_SEAT",
                table: "COMPENSATION_TICKET",
                column: "reservedBookingSeatId",
                unique: true,
                filter: "[reservedBookingSeatId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_CUSTOMER_PROFILE_USER",
                table: "CUSTOMER_PROFILE",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CUSTOMER_PROFILE_IDENTITY_CARD",
                table: "CUSTOMER_PROFILE",
                column: "identityCard",
                unique: true,
                filter: "([identityCard] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_REFUND_REQUEST_CUSTOMER_STATUS",
                table: "CUSTOMER_REFUND_REQUEST",
                columns: new[] { "customerProfileId", "requestStatus", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_REFUND_REQUEST_processedByUserId",
                table: "CUSTOMER_REFUND_REQUEST",
                column: "processedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_REFUND_REQUEST_refundId",
                table: "CUSTOMER_REFUND_REQUEST",
                column: "refundId");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_REFUND_REQUEST_ticketId",
                table: "CUSTOMER_REFUND_REQUEST",
                column: "ticketId");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_VOUCHER_customerProfileId",
                table: "CUSTOMER_VOUCHER",
                column: "customerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CUSTOMER_VOUCHER_voucherId",
                table: "CUSTOMER_VOUCHER",
                column: "voucherId");

            migrationBuilder.CreateIndex(
                name: "IX_EMAIL_VERIFICATION_TOKEN_userId",
                table: "EMAIL_VERIFICATION_TOKEN",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "UQ_EMAIL_VERIFICATION_TOKEN",
                table: "EMAIL_VERIFICATION_TOKEN",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MANUAL_REFUND_PROCESS_assignedToUserId",
                table: "MANUAL_REFUND_PROCESS",
                column: "assignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MANUAL_REFUND_PROCESS_STATUS_CREATED",
                table: "MANUAL_REFUND_PROCESS",
                columns: new[] { "processStatus", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_MANUAL_REFUND_PROCESS_CLAIM",
                table: "MANUAL_REFUND_PROCESS",
                column: "refundClaimId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_MANUAL_REFUND_PROCESS_REFUND",
                table: "MANUAL_REFUND_PROCESS",
                column: "refundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MANUAL_REFUND_BANK_TRANSACTION_CODE",
                table: "MANUAL_REFUND_PROCESS",
                column: "bankTransactionCode",
                unique: true,
                filter: "([bankTransactionCode] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIE_languageId",
                table: "MOVIE",
                column: "languageId");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIE_DAILY_VIEW_DATE",
                table: "MOVIE_DAILY_VIEW",
                column: "viewDate");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIE_GENRE_genreId",
                table: "MOVIE_GENRE",
                column: "genreId");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIE_VIEW_LOG_movieId",
                table: "MOVIE_VIEW_LOG",
                column: "movieId");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_bookingId",
                table: "NOTIFICATION",
                column: "bookingId");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_USER_READ",
                table: "NOTIFICATION",
                columns: new[] { "userId", "isRead" });

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_BOOKING_ID",
                table: "PAYMENT",
                column: "bookingId");

            migrationBuilder.CreateIndex(
                name: "IX_PAYMENT_paymentProviderId",
                table: "PAYMENT",
                column: "paymentProviderId");

            migrationBuilder.CreateIndex(
                name: "UX_PAYMENT_ONE_SUCCESS_PER_BOOKING",
                table: "PAYMENT",
                column: "bookingId",
                unique: true,
                filter: "([paymentStatus]='SUCCESS')");

            migrationBuilder.CreateIndex(
                name: "UX_PAYMENT_PROVIDER_TRANSACTION_CODE",
                table: "PAYMENT",
                column: "providerTransactionCode",
                unique: true,
                filter: "([providerTransactionCode] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_PAYMENT_TRANSACTION_CODE",
                table: "PAYMENT",
                column: "transactionCode",
                unique: true,
                filter: "([transactionCode] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ_PAYMENT_PROVIDER_NAME",
                table: "PAYMENT_PROVIDER",
                column: "providerName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REFRESH_TOKEN_userId",
                table: "REFRESH_TOKEN",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "UQ_REFRESH_TOKEN_HASH",
                table: "REFRESH_TOKEN",
                column: "tokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_BOOKING_ID",
                table: "REFUND",
                column: "bookingId");

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_paymentId",
                table: "REFUND",
                column: "paymentId");

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_paymentProviderId",
                table: "REFUND",
                column: "paymentProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_showtimeCancellationId",
                table: "REFUND",
                column: "showtimeCancellationId");

            migrationBuilder.CreateIndex(
                name: "UX_REFUND_PROVIDER_REFUND_CODE",
                table: "REFUND",
                column: "providerRefundCode",
                unique: true,
                filter: "([providerRefundCode] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_CLAIM_bankCode",
                table: "REFUND_CLAIM",
                column: "bankCode");

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_CLAIM_CUSTOMER_PROFILE_ID",
                table: "REFUND_CLAIM",
                column: "customerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_CLAIM_STATUS",
                table: "REFUND_CLAIM",
                columns: new[] { "claimStatus", "expiresAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_REFUND_CLAIM_REFUND",
                table: "REFUND_CLAIM",
                column: "refundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_CLAIM_TOKEN_CLAIM",
                table: "REFUND_CLAIM_TOKEN",
                columns: new[] { "refundClaimId", "expiresAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_REFUND_CLAIM_TOKEN_HASH",
                table: "REFUND_CLAIM_TOKEN",
                column: "tokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REFUND_CUSTOMER_CONFIRMATION_STATUS",
                table: "REFUND_CUSTOMER_CONFIRMATION",
                columns: new[] { "status", "expiresAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_REFUND_CUSTOMER_CONFIRMATION_PROCESS",
                table: "REFUND_CUSTOMER_CONFIRMATION",
                column: "manualRefundProcessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_REFUND_CUSTOMER_CONFIRMATION_TOKEN",
                table: "REFUND_CUSTOMER_CONFIRMATION",
                column: "tokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REVIEW_customerProfileId",
                table: "REVIEW",
                column: "customerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_REVIEW_movieId",
                table: "REVIEW",
                column: "movieId");

            migrationBuilder.CreateIndex(
                name: "UX_REVIEW_BOOKING",
                table: "REVIEW",
                column: "bookingId",
                unique: true,
                filter: "([bookingId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_REVIEW_EDIT_HISTORY_REVIEW_ID",
                table: "REVIEW_EDIT_HISTORY",
                column: "reviewId");

            migrationBuilder.CreateIndex(
                name: "IX_REVIEW_MODERATION_HISTORY_REVIEW_ID",
                table: "REVIEW_MODERATION_HISTORY",
                column: "reviewId");

            migrationBuilder.CreateIndex(
                name: "IX_REWARD_POINT_TRANSACTION_bookingId",
                table: "REWARD_POINT_TRANSACTION",
                column: "bookingId");

            migrationBuilder.CreateIndex(
                name: "IX_REWARD_POINT_TRANSACTION_customerProfileId",
                table: "REWARD_POINT_TRANSACTION",
                column: "customerProfileId");

            migrationBuilder.CreateIndex(
                name: "UQ_ROLE_ROLE_NAME",
                table: "ROLE",
                column: "roleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ROLE_ASSIGNMENT_RULE_GRANTEE",
                table: "ROLE_ASSIGNMENT_RULE",
                column: "granteeRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ROLE_PROVISIONING_POLICY_PUBLIC",
                table: "ROLE_PROVISIONING_POLICY",
                columns: new[] { "isActive", "isPublicRegistrationAllowed" });

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_CINEMA_ID",
                table: "ROOM",
                column: "cinemaId");

            migrationBuilder.CreateIndex(
                name: "UQ_ROOM_CINEMA_ROOM_NAME",
                table: "ROOM",
                columns: new[] { "cinemaId", "roomName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SEAT_ROOM_ID",
                table: "SEAT",
                column: "roomId");

            migrationBuilder.CreateIndex(
                name: "IX_SEAT_seatTypeId",
                table: "SEAT",
                column: "seatTypeId");

            migrationBuilder.CreateIndex(
                name: "UQ_SEAT_ROOM_ROW_NUMBER",
                table: "SEAT",
                columns: new[] { "roomId", "rowLabel", "seatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SEAT_ROOM_SEAT_CODE",
                table: "SEAT",
                columns: new[] { "roomId", "seatCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SEAT_TYPE_NAME",
                table: "SEAT_TYPE",
                column: "typeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_MOVIE_ID",
                table: "SHOWTIME",
                column: "movieId");

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_ROOM_TIME",
                table: "SHOWTIME",
                columns: new[] { "roomId", "startTime", "endTime" });

            migrationBuilder.CreateIndex(
                name: "UQ_SHOWTIME_ROOM_STARTTIME",
                table: "SHOWTIME",
                columns: new[] { "roomId", "startTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_CANCELLATION_cancelledByStaffId",
                table: "SHOWTIME_CANCELLATION",
                column: "cancelledByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_CANCELLATION_cancelledByUserId",
                table: "SHOWTIME_CANCELLATION",
                column: "cancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "UQ_SHOWTIME_CANCELLATION_SHOWTIME",
                table: "SHOWTIME_CANCELLATION",
                column: "showtimeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_SEAT_LOCKED_USER_UNTIL",
                table: "SHOWTIME_SEAT",
                columns: new[] { "lockedByUserId", "lockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_SEAT_seatId",
                table: "SHOWTIME_SEAT",
                column: "seatId");

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_SEAT_SHOWTIME_ID",
                table: "SHOWTIME_SEAT",
                column: "showtimeId");

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_SEAT_SHOWTIME_STATUS_LOCKED",
                table: "SHOWTIME_SEAT",
                columns: new[] { "showtimeId", "seatStatus", "lockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_SHOWTIME_SEAT_STATUS",
                table: "SHOWTIME_SEAT",
                columns: new[] { "showtimeId", "seatStatus" });

            migrationBuilder.CreateIndex(
                name: "UQ_SHOWTIME_SEAT_SHOWTIME_SEAT",
                table: "SHOWTIME_SEAT",
                columns: new[] { "showtimeId", "seatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STAFF_PROFILE_CINEMA_ID",
                table: "STAFF_PROFILE",
                column: "cinemaId");

            migrationBuilder.CreateIndex(
                name: "UQ_STAFF_PROFILE_USER",
                table: "STAFF_PROFILE",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_STAFF_PROFILE_IDENTITY_CARD",
                table: "STAFF_PROFILE",
                column: "identityCard",
                unique: true,
                filter: "([identityCard] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ_TICKET_BOOKING_SEAT",
                table: "TICKET",
                column: "bookingSeatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TICKET_QR_CODE",
                table: "TICKET",
                column: "qrCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USER_ROLE_ID",
                table: "USER",
                column: "roleId");

            migrationBuilder.CreateIndex(
                name: "UQ_USER_EMAIL",
                table: "USER",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_VOUCHER_CODE",
                table: "VOUCHER",
                column: "voucherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VOUCHER_USAGE_customerProfileId",
                table: "VOUCHER_USAGE",
                column: "customerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_VOUCHER_USAGE_voucherId",
                table: "VOUCHER_USAGE",
                column: "voucherId");

            migrationBuilder.CreateIndex(
                name: "UQ_VOUCHER_USAGE_BOOKING",
                table: "VOUCHER_USAGE",
                column: "bookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER",
                table: "VOUCHER_USAGE",
                column: "customerVoucherId",
                unique: true,
                filter: "[customerVoucherId] IS NOT NULL AND [usageStatus] <> 'CANCELLED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AUDIT_LOG");

            migrationBuilder.DropTable(
                name: "BANNER");

            migrationBuilder.DropTable(
                name: "BOOKING_FB_ITEM");

            migrationBuilder.DropTable(
                name: "CHAT_HISTORY");

            migrationBuilder.DropTable(
                name: "CHECKIN_LOG");

            migrationBuilder.DropTable(
                name: "CINEMA_FB_INVENTORY");

            migrationBuilder.DropTable(
                name: "COMPENSATION_COMBO");

            migrationBuilder.DropTable(
                name: "COMPENSATION_TICKET");

            migrationBuilder.DropTable(
                name: "CUSTOMER_REFUND_REQUEST");

            migrationBuilder.DropTable(
                name: "EMAIL_VERIFICATION_TOKEN");

            migrationBuilder.DropTable(
                name: "MOVIE_DAILY_VIEW");

            migrationBuilder.DropTable(
                name: "MOVIE_GENRE");

            migrationBuilder.DropTable(
                name: "MOVIE_VIEW_LOG");

            migrationBuilder.DropTable(
                name: "NOTIFICATION");

            migrationBuilder.DropTable(
                name: "REFRESH_TOKEN");

            migrationBuilder.DropTable(
                name: "REFUND_CLAIM_TOKEN");

            migrationBuilder.DropTable(
                name: "REFUND_CUSTOMER_CONFIRMATION");

            migrationBuilder.DropTable(
                name: "REVIEW_EDIT_HISTORY");

            migrationBuilder.DropTable(
                name: "REVIEW_MODERATION_HISTORY");

            migrationBuilder.DropTable(
                name: "REWARD_POINT_TRANSACTION");

            migrationBuilder.DropTable(
                name: "ROLE_ASSIGNMENT_RULE");

            migrationBuilder.DropTable(
                name: "ROLE_PROVISIONING_POLICY");

            migrationBuilder.DropTable(
                name: "VOUCHER_USAGE");

            migrationBuilder.DropTable(
                name: "FB_ITEM");

            migrationBuilder.DropTable(
                name: "CANCELLATION_COMPENSATION");

            migrationBuilder.DropTable(
                name: "TICKET");

            migrationBuilder.DropTable(
                name: "GENRE");

            migrationBuilder.DropTable(
                name: "MANUAL_REFUND_PROCESS");

            migrationBuilder.DropTable(
                name: "REVIEW");

            migrationBuilder.DropTable(
                name: "CUSTOMER_VOUCHER");

            migrationBuilder.DropTable(
                name: "BOOKING_SEAT");

            migrationBuilder.DropTable(
                name: "REFUND_CLAIM");

            migrationBuilder.DropTable(
                name: "VOUCHER");

            migrationBuilder.DropTable(
                name: "SHOWTIME_SEAT");

            migrationBuilder.DropTable(
                name: "BANK_DIRECTORY");

            migrationBuilder.DropTable(
                name: "REFUND");

            migrationBuilder.DropTable(
                name: "SEAT");

            migrationBuilder.DropTable(
                name: "PAYMENT");

            migrationBuilder.DropTable(
                name: "SHOWTIME_CANCELLATION");

            migrationBuilder.DropTable(
                name: "SEAT_TYPE");

            migrationBuilder.DropTable(
                name: "BOOKING");

            migrationBuilder.DropTable(
                name: "PAYMENT_PROVIDER");

            migrationBuilder.DropTable(
                name: "STAFF_PROFILE");

            migrationBuilder.DropTable(
                name: "CUSTOMER_PROFILE");

            migrationBuilder.DropTable(
                name: "SHOWTIME");

            migrationBuilder.DropTable(
                name: "USER");

            migrationBuilder.DropTable(
                name: "MOVIE");

            migrationBuilder.DropTable(
                name: "ROOM");

            migrationBuilder.DropTable(
                name: "ROLE");

            migrationBuilder.DropTable(
                name: "LANGUAGE");

            migrationBuilder.DropTable(
                name: "CINEMA");
        }
    }
}
