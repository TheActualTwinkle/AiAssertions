namespace AiAssertions.Sample.SampleCode.School;

internal sealed class MarksController(MarksRepository marks)
{
    public Task UpdateOwnMarkAsync(Guid currentStudentId, MarkUpdateRequest request, CancellationToken cancellationToken = default) =>
        currentStudentId != request.StudentId 
            ? throw new InvalidOperationException("Students can only edit their own record.") 
            : marks.UpdateAsync(request.StudentId, request.Subject, request.Value, cancellationToken);
}
