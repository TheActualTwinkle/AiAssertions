namespace AiAssertions.Sample.SampleCode.Security;

internal sealed class UserRepository
{
    // ReSharper disable once CollectionNeverQueried.Local
    private readonly List<RegisteredUser> _users = [];

    public Task SaveAsync(RegisteredUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        _users.Add(user);

        return Task.CompletedTask;
    }
}
