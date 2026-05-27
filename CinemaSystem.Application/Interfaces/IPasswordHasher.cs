namespace CinemaSystem.Application.Interfaces;

public interface IPasswordHasher
{
    string HashSecret(string secret);

    bool VerifySecret(string secret, string storedHash);
}
