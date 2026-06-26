using System.Text;
using CinemaSystem.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace CinemaSystem.Infrastructure.Security;

public sealed class SensitiveDataProtector : ISensitiveDataProtector
{
    private readonly IDataProtector _protector;

    public SensitiveDataProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("CinemaSystem.RefundBankData.v1");
    }

    public byte[] Protect(string plaintext)
    {
        return _protector.Protect(Encoding.UTF8.GetBytes(plaintext));
    }

    public string Unprotect(byte[] protectedData)
    {
        return Encoding.UTF8.GetString(_protector.Unprotect(protectedData));
    }
}
