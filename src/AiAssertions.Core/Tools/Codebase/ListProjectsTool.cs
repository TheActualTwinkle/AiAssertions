using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class ListProjectsTool : JsonTool<ListProjectsToolArguments>
{
    public override string Name => "list_projects";

    public override string Description => "Lists known project manifest files under the project root. Searches only: *.csproj, *.fsproj, *.vbproj, package.json, pyproject.toml, go.mod, Cargo.toml, pom.xml, build.gradle, build.gradle.kts, composer.json, Gemfile.";

    public override string ParametersJsonSchema => """{"type":"object","properties":{"root":{"type":"string","description":"Project root returned by discover_project_root. This tool searches only known project manifest filenames. If the stack does not use these manifests, the result can be empty."}}}""";

    protected override ValueTask<object> ExecuteAsync(ListProjectsToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var root = PathSafety.ResolveRoot(context, arguments.Root);
        var projects = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var pattern in ProjectManifestPatterns)
            foreach (var path in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                if (!path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    projects.Add(Path.GetRelativePath(root, path));

        return ValueTask.FromResult<object>(new { projects });
    }

    private static readonly string[] ProjectManifestPatterns =
    [
        "*.csproj",
        "*.fsproj",
        "*.vbproj",
        "package.json",
        "pyproject.toml",
        "go.mod",
        "Cargo.toml",
        "pom.xml",
        "build.gradle",
        "build.gradle.kts",
        "composer.json",
        "Gemfile"
    ];
}
