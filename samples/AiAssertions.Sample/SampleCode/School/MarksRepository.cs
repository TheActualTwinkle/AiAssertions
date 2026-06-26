namespace AiAssertions.Sample.SampleCode.School;

internal sealed class MarksRepository
{
    // ReSharper disable once CollectionNeverQueried.Local
    private readonly Dictionary<(Guid StudentId, string Subject), int> _marks = [];

    public Task UpdateAsync(Guid studentId, string subject, int value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        _marks[(studentId, subject)] = value;

        return Task.CompletedTask;
    }
}
