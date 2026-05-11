namespace TokenStats;

/// <summary>
/// A single, shared shape for "before/after" token reporting across every
/// tool in the toolkit (the prompt composer, the Roslyn focused emitter,
/// the lean-context MCP, the Blazor teaching app).
///
/// The point: when a user runs ANY of these tools, they should see the
/// same kind of message in the same kind of format. That consistency
/// is what turns a collection of tools into a teaching system.
/// </summary>
public sealed record TokenReport(
    string ToolName,            // "Focused Emitter", "Prompt Compressor", etc.
    int TokensWithoutTool,      // The naive baseline
    int TokensWithTool,         // After the tool ran
    string? Notes = null)       // Optional context shown alongside
{
    public int TokensSaved => Math.Max(0, TokensWithoutTool - TokensWithTool);
    public double ReductionPercent =>
        TokensWithoutTool == 0 ? 0 : (double)TokensSaved / TokensWithoutTool * 100;

    /// <summary>
    /// One-line summary suitable for any output — CLI stderr, MCP tool prefix,
    /// log message. The format is fixed so users see the same shape everywhere.
    /// </summary>
    public string OneLineSummary() =>
        $"[{ToolName}] Tokens without tool: {TokensWithoutTool:N0}  →  with tool: {TokensWithTool:N0}  ({ReductionPercent:F0}% saved)";

    /// <summary>
    /// Multi-line block with a visual bar. For CLI --stats output and for
    /// embedding at the top of MCP tool results.
    /// </summary>
    public string DetailedBlock(int barWidth = 40)
    {
        var withoutBar = MakeBar(1.0, barWidth);
        var withRatio = TokensWithoutTool == 0
            ? 0
            : (double)TokensWithTool / TokensWithoutTool;
        var withBar = MakeBar(withRatio, barWidth);

        var lines = new[]
        {
            $"┌─ {ToolName}",
            $"│  Without tool:  {withoutBar} {TokensWithoutTool:N0} tokens",
            $"│  With tool:     {withBar} {TokensWithTool:N0} tokens",
            $"│  Saved:         {TokensSaved:N0} tokens ({ReductionPercent:F0}%)",
            Notes is null ? "└─" : $"│  {Notes}\n└─",
        };
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Estimated cost framing. The price defaults to a representative
    /// 2026 input-token rate (~$3 per million for Sonnet-class). Real
    /// pricing varies by model and changes often, so this is illustrative,
    /// not authoritative.
    /// </summary>
    public string CostFraming(double pricePerMillionInputTokens = 3.0)
    {
        var costBefore = TokensWithoutTool / 1_000_000.0 * pricePerMillionInputTokens;
        var costAfter  = TokensWithTool    / 1_000_000.0 * pricePerMillionInputTokens;
        var saved      = costBefore - costAfter;
        // Only show fractions of a cent meaningfully
        return $"≈ ${costBefore:F4} → ${costAfter:F4} (saved ${saved:F4} per call at current rates)";
    }

    private static string MakeBar(double ratio, int width)
    {
        var filled = (int)Math.Round(ratio * width);
        filled = Math.Clamp(filled, 0, width);
        return "█".PadRight(filled, '█').PadRight(width, '░');
    }

    /// <summary>
    /// Rough char-to-token estimate. Real tokenizers (tiktoken, BPE) vary;
    /// 4 chars/token is the conventional rule of thumb for English+code.
    /// For more accuracy, plug in a real tokenizer here — the rest of
    /// the codebase doesn't change.
    /// </summary>
    public static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);

    /// <summary>
    /// Convenience builder when you have before/after strings.
    /// </summary>
    public static TokenReport FromTexts(
        string toolName,
        string textBefore,
        string textAfter,
        string? notes = null) =>
        new(toolName,
            TokensWithoutTool: EstimateTokens(textBefore),
            TokensWithTool: EstimateTokens(textAfter),
            Notes: notes);
}
