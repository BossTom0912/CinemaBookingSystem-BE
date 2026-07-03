namespace CinemaSystem.Application.Interfaces;

public interface ISensitiveDataProtector
{
    byte[] Protect(string plaintext);
    string Unprotect(byte[] protectedData);
}
