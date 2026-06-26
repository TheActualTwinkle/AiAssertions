using System.Security.Cryptography;

namespace AiAssertions.Sample.SampleCode.Security;

internal sealed class PasswordRegistrationService(UserRepository users)
{
    public Task RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPassword(password, salt);
        var user = new RegisteredUser
        {
            Email = email,
            PasswordHash = Convert.ToBase64String(hash),
            PasswordSalt = Convert.ToBase64String(salt)
        };

        return users.SaveAsync(user, cancellationToken);
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        using var hmac = new HMACSHA256(salt);
        
        return hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
    }
}
