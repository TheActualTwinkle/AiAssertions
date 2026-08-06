namespace AiAssertions.Core.Assertions;

internal enum AiAssertionExecutionTraceEntryKind
{
    ModelExchange,
    ConversationCompactionModelExchange,
    ConversationCompaction,
    ToolExecution,
    RunCompleted
}
