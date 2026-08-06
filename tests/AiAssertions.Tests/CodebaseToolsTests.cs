using System.Text.Json;
using AiAssertions.Core.Tools.Abstractions;
using AiAssertions.Core.Tools.Codebase;
using FluentAssertions;
using Xunit;

namespace AiAssertions.Tests;

public sealed class CodebaseToolsTests
{
    [Fact]
    public async Task ReadFile_WhenFileContainsUnicode_ShouldReturnCompactPaginationMetadataWithoutEscapedUnicode()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        await File.WriteAllTextAsync(directory.File("README.md"), "Первая\nВторая\nТретья");
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var json = await new ReadFileTool().ExecuteAsync(
            """{"path":"README.md","start_line":2,"line_count":1}""",
            context);

        json.Should().Contain("Вторая");
        json.Should().NotContain("\\u0412");

        using var result = JsonDocument.Parse(json);
        result.RootElement.GetProperty("start_line").GetInt32().Should().Be(2);
        result.RootElement.GetProperty("end_line").GetInt32().Should().Be(2);
        result.RootElement.GetProperty("total_lines").GetInt32().Should().Be(3);
        result.RootElement.GetProperty("has_more").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("next_start_line").GetInt32().Should().Be(3);
        result.RootElement.GetProperty("content").GetString().Should().Be("2: Вторая");
        result.RootElement.TryGetProperty("lines", out _).Should().BeFalse();
    }

    [Fact]
    public async Task FileTools_WhenRepositoryContainsGitIgnoredFiles_ShouldRequireExplicitDiscoveryOptIn()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        Directory.CreateDirectory(directory.File("src"));
        Directory.CreateDirectory(directory.File("ignored"));
        Directory.CreateDirectory(directory.File(".idea"));
        await File.WriteAllTextAsync(directory.File(".gitignore"), "ignored/\n*.secret\n!important.secret\n");
        await File.WriteAllTextAsync(directory.File("src/App.csproj"), "<Project />");
        await File.WriteAllTextAsync(directory.File("src/keep.txt"), "keep");
        await File.WriteAllTextAsync(directory.File("ignored/skip.txt"), "ignored needle");
        await File.WriteAllTextAsync(directory.File(".idea/shelf.txt"), "shelf");
        await File.WriteAllTextAsync(directory.File("hidden.secret"), "hidden");
        await File.WriteAllTextAsync(directory.File("important.secret"), "important");
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var filesJson = await new SearchFilesTool().ExecuteAsync(
            """{"max_results":50}""",
            context);
        var filesIncludingIgnoredJson = await new SearchFilesTool().ExecuteAsync(
            """{"max_results":50,"include_ignored":true}""",
            context);
        var projectsJson = await new ListProjectsTool().ExecuteAsync("{}", context);
        var findJson = await new FindFilesByNameTool().ExecuteAsync(
            """{"name":"skip"}""",
            context);
        var findIncludingIgnoredJson = await new FindFilesByNameTool().ExecuteAsync(
            """{"name":"skip","include_ignored":true}""",
            context);
        var textJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"ignored needle"}""",
            context);
        var textIncludingIgnoredJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"ignored needle","include_ignored":true}""",
            context);
        var readJson = await new ReadFileTool().ExecuteAsync(
            """{"path":"ignored/skip.txt"}""",
            context);

        filesJson.Should().Contain("src/App.csproj");
        filesJson.Should().Contain("src/keep.txt");
        filesJson.Should().Contain("important.secret");
        filesJson.Should().NotContain("ignored/skip.txt");
        filesJson.Should().NotContain("hidden.secret");
        filesJson.Should().NotContain("shelf.txt");
        filesIncludingIgnoredJson.Should().Contain("ignored/skip.txt");
        filesIncludingIgnoredJson.Should().Contain("hidden.secret");
        filesIncludingIgnoredJson.Should().NotContain("shelf.txt");
        projectsJson.Should().Contain("src/App.csproj");
        findJson.Should().NotContain("ignored/skip.txt");
        findIncludingIgnoredJson.Should().Contain("ignored/skip.txt");
        textJson.Should().NotContain("ignored/skip.txt");
        textIncludingIgnoredJson.Should().Contain("ignored/skip.txt");
        readJson.Should().Contain("ignored needle");
    }

    [Fact]
    public async Task SearchText_WhenRegexIsNotRequested_ShouldTreatPipeAsLiteralAndRespectScope()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        Directory.CreateDirectory(directory.File("docs"));
        Directory.CreateDirectory(directory.File("other"));
        await File.WriteAllTextAsync(directory.File("docs/literal.txt"), "alpha|beta");
        await File.WriteAllTextAsync(directory.File("docs/alpha.txt"), "alpha");
        await File.WriteAllTextAsync(directory.File("other/literal.txt"), "alpha|beta");
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var literalJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"alpha|beta","path":"docs","glob":"*.txt"}""",
            context);
        var regexJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"alpha|beta","path":"docs","glob":"*.txt","use_regex":true}""",
            context);

        using var literal = JsonDocument.Parse(literalJson);
        using var regex = JsonDocument.Parse(regexJson);
        var literalMatches = literal.RootElement.GetProperty("matches");
        var regexMatches = regex.RootElement.GetProperty("matches");

        literalMatches.GetArrayLength().Should().Be(1);
        literalMatches[0].GetProperty("file").GetString().Should().Be("docs/literal.txt");
        regexMatches.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task FileTools_ShouldReturnPortablePaths()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        Directory.CreateDirectory(directory.File("nested"));
        await File.WriteAllTextAsync(directory.File("nested/file.txt"), "needle");
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var filesJson = await new SearchFilesTool().ExecuteAsync("{}", context);
        var namesJson = await new FindFilesByNameTool().ExecuteAsync(
            """{"name":"file"}""",
            context);
        var textJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"needle"}""",
            context);
        var readJson = await new ReadFileTool().ExecuteAsync(
            """{"path":"nested/file.txt"}""",
            context);

        PathSafety.ToPortablePath(@"nested\file.txt").Should().Be("nested/file.txt");
        filesJson.Should().Contain("nested/file.txt").And.NotContain("nested\\\\file.txt");
        namesJson.Should().Contain("nested/file.txt").And.NotContain("nested\\\\file.txt");
        textJson.Should().Contain("nested/file.txt").And.NotContain("nested\\\\file.txt");
        readJson.Should().Contain("nested/file.txt").And.NotContain("nested\\\\file.txt");
    }

    [Fact]
    public async Task SearchText_WhenMatchIsAfterTwoMegabytes_ShouldFindIt()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        var content = string.Concat(new string('x', (2 * 1024 * 1024) + 1), "\nneedle-after-two-megabytes\n");
        await File.WriteAllTextAsync(directory.File("large.txt"), content);
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var json = await new SearchTextTool().ExecuteAsync(
            """{"query":"needle-after-two-megabytes","extension":".txt"}""",
            context);

        using var result = JsonDocument.Parse(json);
        var matches = result.RootElement.GetProperty("matches");
        matches.GetArrayLength().Should().Be(1);
        matches[0].GetProperty("file").GetString().Should().Be("large.txt");
    }

    [Fact]
    public async Task FileIndex_WhenProjectRootIsUnderIgnoredNamedAncestor_ShouldNotHideProjectFiles()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        var project = directory.File(".cache/project");
        Directory.CreateDirectory(project);
        await File.WriteAllTextAsync(Path.Combine(project, "visible.txt"), "visible");
        var context = new ToolExecutionContext(project, project);

        var json = await new SearchFilesTool().ExecuteAsync("{}", context);

        json.Should().Contain("visible.txt");
    }

    [Fact]
    public async Task Ripgrep_WhenExecutableIsUnavailable_ShouldAllowFallback()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();

        var result = await RipgrepTextSearch.TrySearchAsync(
            directory.Path,
            "needle",
            extension: null,
            path: null,
            glob: null,
            maxResults: 10,
            CancellationToken.None,
            executable: $"missing-rg-{Guid.NewGuid():N}");

        result.Should().BeNull();
    }

    [Fact]
    public async Task FileTools_WhenGlobStarMatchesZeroOrMoreDirectories_ShouldUseStandardGlobSemantics()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        Directory.CreateDirectory(directory.File("Source/Nested"));
        await File.WriteAllTextAsync(directory.File("Source/Root.cs"), "needle");
        await File.WriteAllTextAsync(directory.File("Source/Nested/Child.cs"), "needle");
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var filesJson = await new SearchFilesTool().ExecuteAsync(
            """{"glob":"Source/**/*.cs"}""",
            context);
        var textJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"needle","glob":"Source/**/*.cs"}""",
            context);

        using var files = JsonDocument.Parse(filesJson);
        using var text = JsonDocument.Parse(textJson);
        files.RootElement.GetProperty("files").GetArrayLength().Should().Be(2);
        text.RootElement.GetProperty("matches").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task FileTools_WhenExtensionHasNoLeadingDot_ShouldMatchInFastAndFallbackPaths()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        await File.WriteAllTextAsync(directory.File("match.txt"), "needle");
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var filesJson = await new SearchFilesTool().ExecuteAsync(
            """{"extension":"txt"}""",
            context);
        var literalJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"needle","extension":"txt"}""",
            context);
        var regexJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"needle","extension":"txt","use_regex":true}""",
            context);

        using var files = JsonDocument.Parse(filesJson);
        using var literal = JsonDocument.Parse(literalJson);
        using var regex = JsonDocument.Parse(regexJson);
        files.RootElement.GetProperty("files").GetArrayLength().Should().Be(1);
        literal.RootElement.GetProperty("matches").GetArrayLength().Should().Be(1);
        regex.RootElement.GetProperty("matches").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task FileIndex_WhenNestedGitIgnoreExists_ShouldExposeMatchingFilesOnlyWithOptIn()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        Directory.CreateDirectory(directory.File("src/generated"));
        await File.WriteAllTextAsync(directory.File("src/.gitignore"), "generated/\ncache/\n*.tmp\n!important.tmp\nfile[0-9].log\n");
        await File.WriteAllTextAsync(directory.File("src/visible.txt"), "visible");
        await File.WriteAllTextAsync(directory.File("src/cache"), "a file, not an ignored directory");
        await File.WriteAllTextAsync(directory.File("src/generated/hidden.txt"), "hidden");
        await File.WriteAllTextAsync(directory.File("src/hidden.tmp"), "hidden");
        await File.WriteAllTextAsync(directory.File("src/important.tmp"), "important");
        await File.WriteAllTextAsync(directory.File("src/file7.log"), "hidden");
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var json = await new SearchFilesTool().ExecuteAsync(
            """{"max_results":50}""",
            context);
        var includingIgnoredJson = await new SearchFilesTool().ExecuteAsync(
            """{"max_results":50,"include_ignored":true}""",
            context);

        json.Should().Contain("src/visible.txt");
        json.Should().Contain("src/cache");
        json.Should().Contain("src/important.tmp");
        json.Should().NotContain("src/generated/hidden.txt");
        json.Should().NotContain("src/hidden.tmp");
        json.Should().NotContain("src/file7.log");
        includingIgnoredJson.Should().Contain("src/generated/hidden.txt");
        includingIgnoredJson.Should().Contain("src/hidden.tmp");
        includingIgnoredJson.Should().Contain("src/file7.log");
    }

    [Fact]
    public async Task ReadFile_WhenSymlinkEscapesProjectRoot_ShouldRejectIt()
    {
        if (OperatingSystem.IsWindows())
            return;

        using var directory = new CodebaseToolsTestTemporaryDirectory();
        using var outside = new CodebaseToolsTestTemporaryDirectory();
        await File.WriteAllTextAsync(outside.File("secret.txt"), "secret");
        Directory.CreateSymbolicLink(directory.File("outside-link"), outside.Path);
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var action = () => new ReadFileTool().ExecuteAsync(
            """{"path":"outside-link/secret.txt"}""",
            context).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outside the project root*");
    }

    [Fact]
    public async Task SearchTools_WhenMoreResultsExist_ShouldReturnStablePaginationMetadata()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        for (var index = 0; index < 5; index++)
            await File.WriteAllTextAsync(directory.File($"item-{index}.txt"), $"needle {index}");

        var context = new ToolExecutionContext(directory.Path, directory.Path);
        var filesFirstJson = await new SearchFilesTool().ExecuteAsync(
            """{"glob":"*.txt","max_results":2}""",
            context);
        var filesSecondJson = await new SearchFilesTool().ExecuteAsync(
            """{"glob":"*.txt","max_results":2,"offset":2}""",
            context);
        var filesLastJson = await new SearchFilesTool().ExecuteAsync(
            """{"glob":"*.txt","max_results":2,"offset":4}""",
            context);
        var namesJson = await new FindFilesByNameTool().ExecuteAsync(
            """{"name":"item-","max_results":2}""",
            context);
        var textFirstJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"needle","max_results":2}""",
            context);
        var textSecondJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"needle","max_results":2,"offset":2}""",
            context);

        using var filesFirst = JsonDocument.Parse(filesFirstJson);
        using var filesSecond = JsonDocument.Parse(filesSecondJson);
        using var filesLast = JsonDocument.Parse(filesLastJson);
        using var names = JsonDocument.Parse(namesJson);
        using var textFirst = JsonDocument.Parse(textFirstJson);
        using var textSecond = JsonDocument.Parse(textSecondJson);

        AssertPage(filesFirst.RootElement, "files", expectedOffset: 0, expectedNextOffset: 2);
        AssertPage(filesSecond.RootElement, "files", expectedOffset: 2, expectedNextOffset: 4);
        filesLast.RootElement.GetProperty("files").GetArrayLength().Should().Be(1);
        filesLast.RootElement.GetProperty("has_more").GetBoolean().Should().BeFalse();
        filesLast.RootElement.GetProperty("next_offset").ValueKind.Should().Be(JsonValueKind.Null);
        AssertPage(names.RootElement, "files", expectedOffset: 0, expectedNextOffset: 2);
        AssertPage(textFirst.RootElement, "matches", expectedOffset: 0, expectedNextOffset: 2);
        AssertPage(textSecond.RootElement, "matches", expectedOffset: 2, expectedNextOffset: 4);

        filesFirst.RootElement.GetProperty("files").EnumerateArray()
            .Select(item => item.GetString())
            .Should().NotIntersectWith(filesSecond.RootElement.GetProperty("files").EnumerateArray().Select(item => item.GetString()));
        textFirst.RootElement.GetProperty("matches").EnumerateArray()
            .Select(item => item.GetProperty("file").GetString())
            .Should().NotIntersectWith(textSecond.RootElement.GetProperty("matches").EnumerateArray().Select(item => item.GetProperty("file").GetString()));
    }

    [Fact]
    public async Task TextTools_WhenLineIsVeryLarge_ShouldReturnBoundedTruncatedContentAroundMatch()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        var line = string.Concat(new string('x', 1_000_000), "needle-at-the-end");
        await File.WriteAllTextAsync(directory.File("large-line.txt"), line);
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var searchJson = await new SearchTextTool().ExecuteAsync(
            """{"query":"needle-at-the-end"}""",
            context);
        var readJson = await new ReadFileTool().ExecuteAsync(
            """{"path":"large-line.txt"}""",
            context);

        using var search = JsonDocument.Parse(searchJson);
        using var read = JsonDocument.Parse(readJson);
        var match = search.RootElement.GetProperty("matches")[0];
        var matchText = match.GetProperty("text").GetString();
        matchText.Should().Contain("needle-at-the-end");
        matchText.Length.Should().BeLessThanOrEqualTo(500);
        match.GetProperty("text_truncated").GetBoolean().Should().BeTrue();
        read.RootElement.GetProperty("content").GetString()!.Length.Should().BeLessThan(1_100);
        read.RootElement.GetProperty("content_truncated").GetBoolean().Should().BeTrue();
        read.RootElement.GetProperty("truncated_line_count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ReadFile_WhenRequestedPageExceedsOutputBudget_ShouldExposeNextPage()
    {
        using var directory = new CodebaseToolsTestTemporaryDirectory();
        var lines = Enumerable.Range(1, 100).Select(index => $"{index} {new string('x', 900)}");
        await File.WriteAllLinesAsync(directory.File("many-lines.txt"), lines);
        var context = new ToolExecutionContext(directory.Path, directory.Path);

        var json = await new ReadFileTool().ExecuteAsync(
            """{"path":"many-lines.txt","line_count":100}""",
            context);

        using var result = JsonDocument.Parse(json);
        result.RootElement.GetProperty("content").GetString()!.Length.Should().BeLessThanOrEqualTo(30_000);
        result.RootElement.GetProperty("content_truncated").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("has_more").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("next_start_line").GetInt32().Should()
            .Be(result.RootElement.GetProperty("end_line").GetInt32() + 1);
    }

    private static void AssertPage(JsonElement result, string itemsProperty, int expectedOffset, int expectedNextOffset)
    {
        result.GetProperty(itemsProperty).GetArrayLength().Should().Be(2);
        result.GetProperty("returned_count").GetInt32().Should().Be(2);
        result.GetProperty("offset").GetInt32().Should().Be(expectedOffset);
        result.GetProperty("has_more").GetBoolean().Should().BeTrue();
        result.GetProperty("next_offset").GetInt32().Should().Be(expectedNextOffset);
    }

}
