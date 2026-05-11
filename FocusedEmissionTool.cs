// Replacement for FocusedEmissionTool.cs.
// Every MCP tool now prefixes its output with a one-line token comparison,
// so the LLM (and the human reading the chat) sees the savings inline.

using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynLean;
using TokenStats;

namespace RoslynLean.Mcp;

[McpServerToolType]
public static class FocusedEmissionTool
{
    [McpServerTool, Description(
        "Returns a focused subset of a C# file containing only the named " +
        "method (full body) plus the SIGNATURES of anything it references. " +
        "Drops unrelated methods, fields, properties, and all comments on " +
        "non-focus members. Typically reduces token count by 60-90% vs. " +
        "reading the whole file.")]
    public static string FocusMethod(
        [Description("Absolute path to the .cs or .razor.cs file")] string filePath,
        [Description("The method name to focus on. Overloads are all included.")] string methodName)
    {
        try
        {
            var emitter = new FocusedEmitter(filePath);
            var result = emitter.Emit(methodName);

            if (!result.Found)
                return $"ERROR: {result.Output}";

            var report = new TokenReport(
                ToolName: "Focused Emitter",
                TokensWithoutTool: result.OriginalTokensEstimate,
                TokensWithTool: result.FocusedTokensEstimate,
                Notes: $"Focus method: {result.FocusMethodName}");

            // Token comparison is the FIRST thing the agent sees in the result.
            // The agent prompt can teach the model to mention this to the user
            // ("I used the focused emitter, saving ~X tokens vs reading the
            // whole file") so the savings become visible in the chat.
            return $"// {report.OneLineSummary()}\n{result.Notes}\n{result.Output}";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }
}
