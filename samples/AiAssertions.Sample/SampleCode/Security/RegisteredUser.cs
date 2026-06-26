namespace AiAssertions.Sample.SampleCode.Security;

internal sealed record RegisteredUser
{
    public required string Email { get; init; }

    public required string PasswordHash { get; init; }

    public required string PasswordSalt { get; init; }
}
