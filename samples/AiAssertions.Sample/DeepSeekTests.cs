using AiAssertions.DeepSeek.Configuration;

namespace AiAssertions.Sample;

public class DeepSeekTests
{
    public DeepSeekTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                     ?? throw new InvalidOperationException("Set DEEPSEEK_API_KEY before running DeepSeek sample tests.");
        AiAssert
            .Configure(
                DeepSeekClientFactory.Create(
                    new DeepSeekOptions
                    {
                        ApiKey = apiKey,
                        Model = DeepSeekModel.V4Pro
                    }))
            .WithDefaultTimeout(TimeSpan.FromMinutes(3))
            .WithGlobalExecutionTrace()
            .WithGlobalConfidenceTolerance(0.75);
    }

    [Fact]
    public async Task AiAssertThatPasswordIsHashedAndNeverLogged_WhenPasswordIsHashedAndNeverLogged_ShouldSendTrueVerdict()
    {
        var result = await AiAssert
            .OnCodebase()
            .WithConfidenceTolerance(0.75)
            .AgainstRequirementFile("Requirements/password-registration.md");

        Assert.True(result.Verdict is CodebaseAssertionVerdict.Passed);
    }

    [Fact]
    public async Task AiAssertThatStudentsCannotModifyMarks_WhenStudentsCanModifyMarks_ShouldSendFalseVerdict()
    {
        var result = await AiAssert
            .OnCodebase()
            .WithTimeout(TimeSpan.FromMinutes(1))
            .That(
                """
                In the sample marks code,
                students must never be able to modify their own marks
                or marks of other students.
                Only teachers and administrators may update marks.
                """);

        Assert.True(result.Verdict is CodebaseAssertionVerdict.Failed);
    }
}
