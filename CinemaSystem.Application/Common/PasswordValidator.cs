using CinemaSystem.Application.Settings;
using System.Linq;

namespace CinemaSystem.Application.Common;

public static class PasswordValidator
{
    public static string? Validate(string password, AuthSettings settings)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Mật khẩu không được để trống.";

        if (password.Length < settings.PasswordMinLength)
            return $"Mật khẩu phải chứa ít nhất {settings.PasswordMinLength} ký tự.";

        if (password.Length > settings.PasswordMaxLength)
            return $"Mật khẩu không được vượt quá {settings.PasswordMaxLength} ký tự.";

        if (settings.PasswordRequireUppercase && !password.Any(char.IsUpper))
            return "Mật khẩu phải chứa ít nhất một chữ hoa.";

        if (settings.PasswordRequireLowercase && !password.Any(char.IsLower))
            return "Mật khẩu phải chứa ít nhất một chữ thường.";

        if (settings.PasswordRequireDigit && !password.Any(char.IsDigit))
            return "Mật khẩu phải chứa ít nhất một chữ số.";

        return null; // Valid
    }
}
