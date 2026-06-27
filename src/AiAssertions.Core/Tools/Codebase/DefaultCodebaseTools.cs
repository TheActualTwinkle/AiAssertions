using AiAssertions.Core.Tools.Abstractions;

namespace AiAssertions.Core.Tools.Codebase;

internal static class DefaultCodebaseTools
{
    internal static IReadOnlyList<IAiTool> Create() =>
    [
        new ListProjectsTool(),
        new SearchFilesTool(),
        new FindFilesByNameTool(),
        new SearchTextTool(),
        new ReadFileTool()
    ];
}
