using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal sealed class ListProjectsTool : JsonTool<ListProjectsToolArguments>
{
    public override string Name => "list_projects";

    public override string Description => "Lists known project manifest files under the project root. Searches only: *.csproj, *.fsproj, *.vbproj, package.json, pyproject.toml, go.mod, Cargo.toml, pom.xml, build.gradle, build.gradle.kts, composer.json, Gemfile.";

    public override string ParametersJsonSchema => """{"type":"object","properties":{"root":{"type":"string","description":"Codebase root from execution_context.codebase_root. This tool searches only known project manifest filenames. If the stack does not use these manifests, the result can be empty."}}}""";

    protected override async ValueTask<object> ExecuteAsync(ListProjectsToolArguments arguments, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var root = PathSafety.ResolveRoot(context, arguments.Root);
        var indexedFiles = await context.FileIndex.GetFilesAsync(root, cancellationToken).ConfigureAwait(false);
        var projects = indexedFiles
            .Where(IsProjectManifest)
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        return new { projects };
    }

    private static bool IsProjectManifest(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path);

        return ProjectExtensions.Contains(extension)
            || ProjectManifestNames.Contains(fileName);
    }

    private static readonly HashSet<string> ProjectExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".fsproj",
        ".vbproj"
    };

    private static readonly HashSet<string> ProjectManifestNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json",
        "pyproject.toml",
        "go.mod",
        "Cargo.toml",
        "pom.xml",
        "build.gradle",
        "build.gradle.kts",
        "composer.json",
        "Gemfile"
    };
}
