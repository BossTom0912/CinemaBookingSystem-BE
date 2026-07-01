# Database reset log - `tommy090305@gmail.com`

## 1. Phạm vi và nguyên tắc an toàn

- Chỉ xử lý email: `tommy090305@gmail.com`.
- Chỉ kiểm tra các bảng auth/profile và metadata khóa ngoại liên quan trực tiếp.
- Không quét hoặc xóa dữ liệu của user khác.
- Không xóa booking, review, refund, reward, voucher, audit hoặc dữ liệu nghiệp vụ.
- Cleanup chạy trong transaction với `XACT_ABORT ON`.
- Connection string và credential không được ghi vào log này.

## 2. Kiểm tra trước cleanup

### SQL đã chạy

```sql
SET NOCOUNT ON;
DECLARE @Email NVARCHAR(255) = N'tommy090305@gmail.com';
DECLARE @UserId NVARCHAR(50) = (
    SELECT userId
    FROM dbo.[USER]
    WHERE email = @Email
);
DECLARE @CustomerProfileId NVARCHAR(50) = (
    SELECT customerProfileId
    FROM dbo.CUSTOMER_PROFILE
    WHERE userId = @UserId
);

SELECT 'TARGET_USER' AS section,
       COUNT(*) AS matchingUsers,
       MAX(userId) AS userId,
       MAX(roleId) AS roleId,
       MAX(status) AS status,
       MAX(CONVERT(INT, emailVerified)) AS emailVerified
FROM dbo.[USER]
WHERE email = @Email;

SELECT 'DIRECT_AUTH_COUNTS' AS section,
       (SELECT COUNT(*)
        FROM dbo.EMAIL_VERIFICATION_TOKEN
        WHERE userId = @UserId) AS verificationTokens,
       (SELECT COUNT(*)
        FROM dbo.REFRESH_TOKEN
        WHERE userId = @UserId) AS refreshTokens,
       (SELECT COUNT(*)
        FROM dbo.CUSTOMER_PROFILE
        WHERE userId = @UserId) AS customerProfiles;

SELECT 'PROFILE_BUSINESS_DEPENDENCIES' AS section,
       (SELECT COUNT(*)
        FROM dbo.BOOKING
        WHERE customerProfileId = @CustomerProfileId) AS bookings,
       (SELECT COUNT(*)
        FROM dbo.REVIEW
        WHERE customerProfileId = @CustomerProfileId) AS reviews,
       (SELECT COUNT(*)
        FROM dbo.REFUND_CLAIM
        WHERE customerProfileId = @CustomerProfileId) AS refundClaims,
       (SELECT COUNT(*)
        FROM dbo.REWARD_POINT_TRANSACTION
        WHERE customerProfileId = @CustomerProfileId) AS rewardTransactions,
       (SELECT COUNT(*)
        FROM dbo.VOUCHER_USAGE
        WHERE customerProfileId = @CustomerProfileId) AS voucherUsages;

SELECT 'FK_TO_USER' AS section,
       OBJECT_SCHEMA_NAME(fkc.parent_object_id)
           + '.'
           + OBJECT_NAME(fkc.parent_object_id) AS referencingTable,
       COL_NAME(
           fkc.parent_object_id,
           fkc.parent_column_id
       ) AS referencingColumn
FROM sys.foreign_key_columns fkc
WHERE fkc.referenced_object_id = OBJECT_ID(N'dbo.USER')
ORDER BY referencingTable, referencingColumn;

SELECT 'FK_TO_CUSTOMER_PROFILE' AS section,
       OBJECT_SCHEMA_NAME(fkc.parent_object_id)
           + '.'
           + OBJECT_NAME(fkc.parent_object_id) AS referencingTable,
       COL_NAME(
           fkc.parent_object_id,
           fkc.parent_column_id
       ) AS referencingColumn
FROM sys.foreign_key_columns fkc
WHERE fkc.referenced_object_id = OBJECT_ID(N'dbo.CUSTOMER_PROFILE')
ORDER BY referencingTable, referencingColumn;
```

### Kết quả

```text
TARGET_USER
matchingUsers = 0
userId        = NULL
roleId        = NULL
status        = NULL
emailVerified = NULL

DIRECT_AUTH_COUNTS
verificationTokens = 0
refreshTokens      = 0
customerProfiles   = 0

PROFILE_BUSINESS_DEPENDENCIES
bookings           = 0
reviews            = 0
refundClaims       = 0
rewardTransactions = 0
voucherUsages      = 0
```

Email đã không còn trong `USER` trước khi cleanup. Vì không có `userId` tương ứng nên
không có profile hoặc auth token nào cần xóa theo email này.

### Foreign key tới `USER`

```text
dbo.AUDIT_LOG                   -> userId
dbo.CHAT_HISTORY                -> userId
dbo.CUSTOMER_PROFILE            -> userId
dbo.CUSTOMER_REFUND_REQUEST     -> processedByUserId
dbo.EMAIL_VERIFICATION_TOKEN    -> userId
dbo.MANUAL_REFUND_PROCESS       -> assignedToUserId
dbo.MOVIE_VIEW_LOG              -> userId
dbo.NOTIFICATION                -> userId
dbo.REFRESH_TOKEN               -> userId
dbo.REVIEW                      -> moderatedBy
dbo.REVIEW_MODERATION_HISTORY   -> moderatorId
dbo.SHOWTIME_CANCELLATION       -> cancelledByUserId
dbo.SHOWTIME_SEAT               -> lockedByUserId
dbo.STAFF_PROFILE               -> userId
```

### Foreign key tới `CUSTOMER_PROFILE`

```text
dbo.BOOKING                    -> customerProfileId
dbo.CUSTOMER_REFUND_REQUEST    -> customerProfileId
dbo.REFUND_CLAIM               -> customerProfileId
dbo.REVIEW                     -> customerProfileId
dbo.REWARD_POINT_TRANSACTION   -> customerProfileId
dbo.VOUCHER_USAGE              -> customerProfileId
```

## 3. Cleanup có điều kiện

Cleanup chỉ xóa:

1. Email verification token của đúng user theo email.
2. Refresh token của đúng user theo email.
3. Customer profile nếu profile không có bất kỳ lịch sử nghiệp vụ nào.
4. User nếu không còn profile, token hoặc foreign-key reference.

### Lần chạy đầu

Lần đầu SQL Server từ chối câu `DELETE` do session `sqlcmd` chưa bật
`QUOTED_IDENTIFIER`, là option bắt buộc khi database có filtered index.

```text
DELETE failed because the following SET options have incorrect settings:
'QUOTED_IDENTIFIER'.
```

`XACT_ABORT ON` đảm bảo transaction rollback toàn bộ. Không có dòng nào bị thay đổi.

### SQL chạy thành công sau khi bổ sung session options

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @Email NVARCHAR(255) = N'tommy090305@gmail.com';
DECLARE @DeletedVerificationTokens INT = 0;
DECLARE @DeletedRefreshTokens INT = 0;
DECLARE @DeletedCustomerProfiles INT = 0;
DECLARE @DeletedUsers INT = 0;

BEGIN TRANSACTION;

DELETE evt
FROM dbo.EMAIL_VERIFICATION_TOKEN evt
INNER JOIN dbo.[USER] u ON u.userId = evt.userId
WHERE u.email = @Email;
SET @DeletedVerificationTokens = @@ROWCOUNT;

DELETE rt
FROM dbo.REFRESH_TOKEN rt
INNER JOIN dbo.[USER] u ON u.userId = rt.userId
WHERE u.email = @Email;
SET @DeletedRefreshTokens = @@ROWCOUNT;

DELETE cp
FROM dbo.CUSTOMER_PROFILE cp
INNER JOIN dbo.[USER] u ON u.userId = cp.userId
WHERE u.email = @Email
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.BOOKING b
      WHERE b.customerProfileId = cp.customerProfileId
  )
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.REVIEW r
      WHERE r.customerProfileId = cp.customerProfileId
  )
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.REFUND_CLAIM rc
      WHERE rc.customerProfileId = cp.customerProfileId
  )
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.REWARD_POINT_TRANSACTION rpt
      WHERE rpt.customerProfileId = cp.customerProfileId
  )
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.VOUCHER_USAGE vu
      WHERE vu.customerProfileId = cp.customerProfileId
  )
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.CUSTOMER_REFUND_REQUEST crr
      WHERE crr.customerProfileId = cp.customerProfileId
  );
SET @DeletedCustomerProfiles = @@ROWCOUNT;

DELETE u
FROM dbo.[USER] u
WHERE u.email = @Email
  AND NOT EXISTS (
      SELECT 1 FROM dbo.CUSTOMER_PROFILE cp
      WHERE cp.userId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.STAFF_PROFILE sp
      WHERE sp.userId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.EMAIL_VERIFICATION_TOKEN evt
      WHERE evt.userId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.REFRESH_TOKEN rt
      WHERE rt.userId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.AUDIT_LOG al
      WHERE al.userId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.CHAT_HISTORY ch
      WHERE ch.userId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.MOVIE_VIEW_LOG mvl
      WHERE mvl.userId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.NOTIFICATION n
      WHERE n.userId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.CUSTOMER_REFUND_REQUEST crr
      WHERE crr.processedByUserId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.MANUAL_REFUND_PROCESS mrp
      WHERE mrp.assignedToUserId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.REVIEW r
      WHERE r.moderatedBy = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.REVIEW_MODERATION_HISTORY rmh
      WHERE rmh.moderatorId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.SHOWTIME_CANCELLATION sc
      WHERE sc.cancelledByUserId = u.userId
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.SHOWTIME_SEAT ss
      WHERE ss.lockedByUserId = u.userId
  );
SET @DeletedUsers = @@ROWCOUNT;

COMMIT TRANSACTION;

SELECT 'CLEANUP_RESULT' AS section,
       @DeletedVerificationTokens AS deletedVerificationTokens,
       @DeletedRefreshTokens AS deletedRefreshTokens,
       @DeletedCustomerProfiles AS deletedCustomerProfiles,
       @DeletedUsers AS deletedUsers;
```

### Kết quả cleanup

```text
deletedVerificationTokens = 0
deletedRefreshTokens      = 0
deletedCustomerProfiles   = 0
deletedUsers              = 0
```

Không có dòng bị xóa vì email mục tiêu đã không tồn tại. Đây là kết quả mong muốn và
không có dữ liệu ngoài phạm vi bị tác động.

## 4. Xác minh sau cleanup

### SQL đã chạy

```sql
SET NOCOUNT ON;
DECLARE @Email NVARCHAR(255) = N'tommy090305@gmail.com';
DECLARE @UserId NVARCHAR(50) = (
    SELECT userId
    FROM dbo.[USER]
    WHERE email = @Email
);

SELECT 'FINAL_AUTH_VERIFICATION' AS section,
       (SELECT COUNT(*)
        FROM dbo.[USER]
        WHERE email = @Email) AS userEmailCount,
       (SELECT COUNT(*)
        FROM dbo.CUSTOMER_PROFILE
        WHERE userId = @UserId) AS customerProfileCount,
       (SELECT COUNT(*)
        FROM dbo.EMAIL_VERIFICATION_TOKEN
        WHERE userId = @UserId) AS verificationTokenCount,
       (SELECT COUNT(*)
        FROM dbo.REFRESH_TOKEN
        WHERE userId = @UserId) AS refreshTokenCount;

SELECT 'REGISTRATION_READINESS' AS section,
       CASE
           WHEN NOT EXISTS (
                    SELECT 1
                    FROM dbo.[USER]
                    WHERE email = @Email
                )
                AND EXISTS (
                    SELECT 1
                    FROM dbo.[ROLE]
                    WHERE roleId = 'ROLE_CUSTOMER'
                )
                AND EXISTS (
                    SELECT 1
                    FROM sys.indexes i
                    INNER JOIN sys.index_columns ic
                        ON ic.object_id = i.object_id
                        AND ic.index_id = i.index_id
                    INNER JOIN sys.columns c
                        ON c.object_id = ic.object_id
                        AND c.column_id = ic.column_id
                    WHERE i.object_id = OBJECT_ID(N'dbo.USER')
                      AND i.is_unique = 1
                      AND i.is_disabled = 0
                      AND c.name = 'email'
                )
               THEN 1
           ELSE 0
       END AS databaseReadyForRegistration,
       (SELECT COUNT(*)
        FROM dbo.[ROLE]
        WHERE roleId = 'ROLE_CUSTOMER') AS customerRoleCount,
       (
           SELECT COUNT(*)
           FROM sys.indexes i
           INNER JOIN sys.index_columns ic
               ON ic.object_id = i.object_id
               AND ic.index_id = i.index_id
           INNER JOIN sys.columns c
               ON c.object_id = ic.object_id
               AND c.column_id = ic.column_id
           WHERE i.object_id = OBJECT_ID(N'dbo.USER')
             AND i.is_unique = 1
             AND i.is_disabled = 0
             AND c.name = 'email'
       ) AS activeUniqueEmailIndexes;
```

### Kết quả cuối

```text
FINAL_AUTH_VERIFICATION
userEmailCount         = 0
customerProfileCount   = 0
verificationTokenCount = 0
refreshTokenCount      = 0

REGISTRATION_READINESS
databaseReadyForRegistration = 1
customerRoleCount            = 1
activeUniqueEmailIndexes     = 1
```

## 5. Kết luận

1. `tommy090305@gmail.com` không còn tồn tại trong bảng `USER`.
2. Không có customer profile hoặc auth token nào cần cleanup cho email này.
3. Role Customer tồn tại.
4. Unique index email đang hoạt động và không bị chiếm bởi email mục tiêu.
5. Ở mức database, email đã sẵn sàng để đăng ký lại.
