using System.Security.Cryptography;
using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Infrastructure.Security;

public sealed class CryptoOtpGenerator : IOtpGenerator
{
    public string GenerateSixDigitOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
