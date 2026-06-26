using AiAssertions.OpenAi.Configuration;

namespace AiAssertions.Sample;

public sealed class OpenAiTests
{
    public OpenAiTests()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? throw new InvalidOperationException("Set OPENAI_API_KEY before running OpenAI sample tests.");

        AiAssert
            .Configure(
                OpenAiClientFactory.Create(
                    new OpenAiOptions
                    {
                        ApiKey = apiKey,
                        Model = OpenAiModel.Gpt4O
                    }))
            .WithDefaultTimeout(TimeSpan.FromMinutes(3))
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
