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
        Func<IReadOnlyList<AiChatMessage>, IReadOnlyList<AiChatMessage>> conversationCompactor =
            messages => messages;

        var result = configuration
            .WithGlobalApproximateTokenLimit(4096)
            .WithGlobalTokenEstimator(tokenEstimator)
            .WithGlobalSystemPrompt("system prompt")
            .WithGlobalAdditionalSystemPrompt("first")
            .WithGlobalAdditionalSystemPrompt("second")
            .WithoutGlobalConversationCompaction()
            .WithGlobalConversationCompactor(conversationCompactor);

        var defaults = getDefaults();
        var separator = Environment.NewLine + Environment.NewLine;

        result.Should().BeSameAs(configuration);
        defaults.MaxRequestTokens.Should().Be(4096);
        defaults.RequestTokenEstimator.Should().BeSameAs(tokenEstimator);
        defaults.SystemPrompt.Should().Be("system prompt");
        defaults.AdditionalSystemPrompt.Should().Be($"first{separator}second");
        defaults.ConversationCompactionEnabled.Should().BeTrue();
        defaults.ConversationCompactor.Should().BeSameAs(conversationCompactor);
    }

    [Fact]
    public void WithoutGlobalConversationCompaction_WhenCustomCompactorIsConfigured_ShouldClearIt()
    {
        var (configuration, getDefaults) = CreateConfiguration();

        configuration
            .WithGlobalConversationCompactor(messages => messages)
            .WithoutGlobalConversationCompaction();

        var defaults = getDefaults();

        defaults.ConversationCompactionEnabled.Should().BeFalse();
        defaults.ConversationCompactor.Should().BeNull();
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
    public void GlobalDelegateConfigurationMethods_WhenValueIsNull_ShouldThrow()
    {
        var (configuration, _) = CreateConfiguration();

        var tokenEstimator = () => configuration.WithGlobalTokenEstimator(null!);
        var conversationCompactor = () => configuration.WithGlobalConversationCompactor(null!);

        tokenEstimator.Should().Throw<ArgumentNullException>()
            .WithParameterName("tokenEstimator");
        conversationCompactor.Should().Throw<ArgumentNullException>()
            .WithParameterName("conversationCompactor");
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
