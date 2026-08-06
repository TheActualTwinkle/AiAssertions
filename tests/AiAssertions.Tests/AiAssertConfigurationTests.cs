using AiAssertions.Core.Models;
using FluentAssertions;
using Xunit;

namespace AiAssertions.Tests;

public sealed class AiAssertConfigurationTests
{
    [Fact]
    public void GlobalConfigurationMethods_ShouldUpdateDefaultsAndReturnSameBuilder()
    {
        var (configuration, getDefaults) = CreateConfiguration();
        Func<IReadOnlyList<AiChatMessage>, int> tokenEstimator = messages => messages.Count;

        var result = configuration
            .WithGlobalApproximateTokenLimit(4096, tokenEstimator)
            .WithGlobalSystemPrompt("system prompt")
            .WithGlobalAdditionalSystemPrompt("first")
            .WithGlobalAdditionalSystemPrompt("second")
            .WithoutGlobalConversationCompaction()
            .WithGlobalConversationCompaction(new ConversationCompactionOptions
            {
                RecentToolCallTurns = 1,
                MaxCheckpointChars = 8192
            })
            .WithGlobalExecutionTrace();

        var defaults = getDefaults();
        var separator = Environment.NewLine + Environment.NewLine;

        result.Should().BeSameAs(configuration);
        defaults.MaxRequestTokens.Should().Be(4096);
        defaults.RequestTokenEstimator.Should().BeSameAs(tokenEstimator);
        defaults.SystemPrompt.Should().Be("system prompt");
        defaults.AdditionalSystemPrompt.Should().Be($"first{separator}second");
        defaults.ConversationCompactionEnabled.Should().BeTrue();
        defaults.RecentToolCallTurns.Should().Be(1);
        defaults.MaxCompactedStateChars.Should().Be(8192);
        defaults.ExecutionTraceEnabled.Should().BeTrue();
    }

    [Fact]
    public void WithoutGlobalConversationCompaction_ShouldDisableCompaction()
    {
        var (configuration, getDefaults) = CreateConfiguration();

        configuration.WithoutGlobalConversationCompaction();

        var defaults = getDefaults();

        defaults.ConversationCompactionEnabled.Should().BeFalse();
    }

    [Fact]
    public void WithGlobalConversationCompaction_WhenConfiguredAfterDisabling_ShouldEnableCompaction()
    {
        var (configuration, getDefaults) = CreateConfiguration();

        configuration
            .WithoutGlobalConversationCompaction()
            .WithGlobalConversationCompaction(new ConversationCompactionOptions());

        var defaults = getDefaults();
        defaults.ConversationCompactionEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithGlobalApproximateTokenLimit_WhenValueIsNotPositive_ShouldThrow(int maxTokens)
    {
        var (configuration, _) = CreateConfiguration();

        var act = () => configuration.WithGlobalApproximateTokenLimit(maxTokens);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(maxTokens));
    }

    [Fact]
    public void WithGlobalApproximateTokenLimit_WhenEstimatorIsExplicitlyNull_ShouldUseBuiltInEstimator()
    {
        var (configuration, getDefaults) = CreateConfiguration();

        configuration.WithGlobalApproximateTokenLimit(4096, messages => messages.Count);
        configuration.WithGlobalApproximateTokenLimit(2048, null);

        getDefaults().MaxRequestTokens.Should().Be(2048);
        getDefaults().RequestTokenEstimator.Should().BeNull();
    }

    [Fact]
    public void WithGlobalApproximateTokenLimit_WhenOnlyLimitIsChanged_ShouldPreserveEstimator()
    {
        var (configuration, getDefaults) = CreateConfiguration();
        Func<IReadOnlyList<AiChatMessage>, int> estimator = messages => messages.Count;

        configuration.WithGlobalApproximateTokenLimit(4096, estimator);
        configuration.WithGlobalApproximateTokenLimit(2048);

        getDefaults().MaxRequestTokens.Should().Be(2048);
        getDefaults().RequestTokenEstimator.Should().BeSameAs(estimator);
    }

    [Fact]
    public void WithGlobalConversationCompaction_WhenOptionsAreNull_ShouldThrow()
    {
        var (configuration, _) = CreateConfiguration();

        var act = () => configuration.WithGlobalConversationCompaction(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(0, 1, nameof(ConversationCompactionOptions.RecentToolCallTurns))]
    [InlineData(1, 0, nameof(ConversationCompactionOptions.MaxCheckpointChars))]
    public void WithGlobalConversationCompaction_WhenOptionIsNotPositive_ShouldThrow(
        int recentToolCallTurns,
        int maxCheckpointChars,
        string parameterName)
    {
        var (configuration, _) = CreateConfiguration();
        var options = new ConversationCompactionOptions
        {
            RecentToolCallTurns = recentToolCallTurns,
            MaxCheckpointChars = maxCheckpointChars
        };

        var act = () => configuration.WithGlobalConversationCompaction(options);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameterName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GlobalSystemPromptConfigurationMethods_WhenValueIsBlank_ShouldThrow(string prompt)
    {
        var (configuration, _) = CreateConfiguration();

        var systemPrompt = () => configuration.WithGlobalSystemPrompt(prompt);
        var additionalSystemPrompt = () => configuration.WithGlobalAdditionalSystemPrompt(prompt);

        systemPrompt.Should().Throw<ArgumentException>()
            .WithParameterName("systemPrompt");
        additionalSystemPrompt.Should().Throw<ArgumentException>()
            .WithParameterName("additionalSystemPrompt");
    }

    private static (AiAssertConfiguration Configuration, Func<AiAssertDefaults> GetDefaults) CreateConfiguration()
    {
        var defaults = new AiAssertDefaults();
        var configuration = new AiAssertConfiguration(
            () => defaults,
            value => defaults = value);

        return (configuration, () => defaults);
    }
}
