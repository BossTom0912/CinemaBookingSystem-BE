# Admin tạo tài khoản theo role cấu hình

## Mục tiêu và phạm vi

Admin tạo tài khoản cấp dưới qua **một API**. Quyền tạo và loại hồ sơ không còn
được quyết định bằng `if role == Staff` trong code:

- Admin chỉ tạo được role có rule active trong database. Seed mặc định cho phép
  `Customer`, `Staff`, `Manager`; không có rule `Admin -> Admin`.
- `Customer` tạo `CUSTOMER_PROFILE`, không nhận `cinemaId`.
- `Staff` và `Manager` đều tạo `STAFF_PROFILE`, bắt buộc nhận một rạp active;
  `position` lấy từ policy (`Staff` hoặc `Manager`).
- API không bao giờ trả password hoặc invitation OTP. Người được tạo nhận email
  và dùng API reset password hiện có để đặt mật khẩu đầu tiên.

Public register (`POST /api/auth/register`) cũng đọc policy: chỉ role có
`isPublicRegistrationAllowed = 1` được dùng. Seed chuẩn chỉ bật cờ này cho
`Customer`.

## SQL cần áp dụng

Khi deploy backend, dùng EF migration
`20260718020000_AddRoleProvisioningPolicies` hoặc một data migration được
review cho database hiện hữu. Không chạy full-reset script trên database cần
giữ dữ liệu. Với database local mới,
[`../database/cinema-booking-schema.sql`](../database/cinema-booking-schema.sql)
đã bao gồm schema và seed này.

### Bảng mới

| Bảng | Ý nghĩa |
|---|---|
| `ROLE_PROVISIONING_POLICY` | Cách provision từng role: `profileKind`, có bắt buộc rạp không, `defaultStaffPosition`, active/public-register. |
| `ROLE_ASSIGNMENT_RULE` | Rule `grantorRoleId -> granteeRoleId`: actor mang role nào được tạo target role nào. |

Seed mặc định:

| Target role | Profile | Cinema | Position | Public register | Admin được tạo? |
|---|---|---:|---|---:|---:|
| `ROLE_CUSTOMER` | `CUSTOMER` | Không | — | Có | Có |
| `ROLE_STAFF` | `STAFF` | Bắt buộc | `Staff` | Không | Có |
| `ROLE_MANAGER` | `STAFF` | Bắt buộc | `Manager` | Không | Có |
| `ROLE_ADMIN` | `NONE` | Không | — | Không | Không |

Muốn thêm role mới, DBA/admin cấu hình phải thêm `ROLE`, một policy hợp lệ và
rule assignment tương ứng. Không cần sửa endpoint hay switch-case trong service.
Script upgrade chỉ insert seed còn thiếu, không ghi đè policy/rule đã được cấu hình.

## API cho FE

Tất cả API dưới đây cần access token Admin có policy `CanManageUserAndRole`.
Response luôn theo envelope:

```json
{
  "success": true,
  "message": "...",
  "data": {},
  "errorCode": null,
  "errors": null
}
```

### 1. Lấy role Admin được quyền tạo

`GET /api/admin/account-provisioning/roles`

Ví dụ thành công `200`:

```json
{
  "success": true,
  "message": "Success",
  "data": [
    {
      "roleId": "ROLE_CUSTOMER",
      "roleName": "CUSTOMER",
      "description": "Customer account",
      "profileKind": "CUSTOMER",
      "requiresCinema": false
    },
    {
      "roleId": "ROLE_MANAGER",
      "roleName": "MANAGER",
      "description": "Cinema manager account",
      "profileKind": "STAFF",
      "requiresCinema": true
    },
    {
      "roleId": "ROLE_STAFF",
      "roleName": "STAFF",
      "description": "Cinema staff account",
      "profileKind": "STAFF",
      "requiresCinema": true
    }
  ],
  "errorCode": null,
  "errors": null
}
```

FE phải dùng mảng `data` này để render role select, không hardcode danh sách
role và không tự thêm Admin vào select.

### 2. Tạo tài khoản

`POST /api/admin/users`

Body tạo Manager:

```json
{
  "email": "manager.branch@example.com",
  "fullName": "Nguyen Van A",
  "phoneNumber": "0901234567",
  "roleId": "ROLE_MANAGER",
  "cinemaId": "CIN_01"
}
```

`cinemaId`:

- Bắt buộc khi role metadata trả `requiresCinema: true`.
- Không gửi khi `requiresCinema: false` (ví dụ Customer).
- Giá trị phải là một cinema `ACTIVE`.

Ví dụ thành công `201`:

```json
{
  "success": true,
  "message": "Account created. Invitation email queued.",
  "data": {
    "userId": "USR_...",
    "email": "manager.branch@example.com",
    "roleId": "ROLE_MANAGER",
    "roleName": "MANAGER",
    "cinemaId": "CIN_01",
    "invitationExpiresAt": "2026-07-18T10:30:00Z"
  },
  "errorCode": null,
  "errors": null
}
```

Lỗi FE cần hiển thị:

| HTTP | `errorCode` | Xử lý gợi ý |
|---:|---|---|
| 400 | `VALIDATION_ERROR` | Hiển thị `errors` theo từng field. |
| 400 | `CINEMA_REQUIRED` | Yêu cầu chọn rạp. |
| 400 | `CINEMA_NOT_ALLOWED` | Xoá lựa chọn rạp cho role không cần rạp. |
| 403 | `ROLE_ASSIGNMENT_NOT_ALLOWED` | Refresh role list; không cho submit role đó. |
| 404 | `CINEMA_NOT_FOUND` | Refresh danh sách rạp active. |
| 409 | `DUPLICATE_EMAIL` | Báo email đã tồn tại. |

## Flow FE đề xuất

1. Khi mở form, gọi `GET /api/admin/account-provisioning/roles` và lưu `data`.
2. Gọi `GET /api/cinemas`, chỉ cho chọn rạp có `cinemaStatus = "ACTIVE"` khi
   role đã chọn có `requiresCinema = true`; ẩn và xoá giá trị `cinemaId` ở role
   khác.
3. Submit body nguyên trạng tới `POST /api/admin/users`. Chỉ hiện “đã tạo” sau
   khi response `success = true`/HTTP `201`.
4. Hiển thị `data.email`, `data.roleName`, `data.cinemaId` và
   `data.invitationExpiresAt`; không cố hiển thị password/OTP vì backend không
   trả các giá trị đó.
5. Người dùng mới nhận invitation rồi dùng flow hiện có
   `POST /api/auth/reset-password` với email, OTP và mật khẩu mới.

## Kiểm soát và audit

- Service lấy role của actor từ `USER.roleId` trong database, không tin role do
  client gửi trong JWT/body.
- Mỗi lần provision ghi `AUDIT_LOG` action `ACCOUNT_PROVISIONED`, gồm actor và
  metadata account mới.
- Invitation token được hash trong `EMAIL_VERIFICATION_TOKEN`, purpose
  `PASSWORD_RESET`, có thời hạn từ `AuthSettings:InvitationTokenExpiryMinutes`.
