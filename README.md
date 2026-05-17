# TokenStats

A tiny shared library that gives every tool in the toolkit the same
"tokens used without tool / with tool" reporting. Same numbers, same
visual, same wording, every time.

## Why this exists

You asked: "It would be great if the tools always show the tokens used
with or without the tool." Right now each tool reports differently —
the CLI has a `--stats` flag, the MCP wraps results in a comment, the
Blazor app shows numbers but no comparison. This unifies all of that.

## What's in here

- **`TokenReport.cs`** — the shared record type. One line per tool to
  build the report; built-in formatters for one-line summaries, detailed
  ASCII blocks, and cost framing.
- **`integrations/RoslynLean.Program.cs`** — replaces the existing
  `Program.cs` in the Roslyn focused emitter. Now always shows the
  comparison (to stderr, so it doesn't pollute pipes).
- **`integrations/FocusedEmissionTool.cs`** — replaces the MCP wrapper
  so every tool result starts with a `// [Focused Emitter] Tokens...`
  one-liner.
- **`integrations/TokenComparison.razor`** — Blazor component for the
  Prompt Coach playground. Drop in `Components/Shared/`.
- **`integrations/token-comparison.css`** — append to `wwwroot/app.css`.
- **`preview.html`** — open in any browser to see the visuals offline.
- **`preview.py`** — simulate the CLI output without .NET installed.

## Three formats, same numbers

**One-line** — for log lines, MCP tool result prefixes, anywhere short:
```
[Focused Emitter] Tokens without tool: 835  →  with tool: 415  (50% saved)
```

**Detailed block** — for CLI `--stats` output, MCP tool detail prefixes,
anywhere you have room for visual:
```
┌─ Focused Emitter
│  Without tool:  ████████████████████████████████████████ 835 tokens
│  With tool:     ████████████████████░░░░░░░░░░░░░░░░░░░░ 415 tokens
│  Saved:         420 tokens (50%)
│  Focus method: OnInitializedAsync. Other members: signatures only.
└─
```

**Blazor component** — for the Prompt Coach playground. Same shape as
the ASCII version but rendered with real bars, the editorial typography,
and the burnt-sienna accent for the "with tool" bar.

## Usage in C#

```csharp
using TokenStats;

// Build a report
var report = TokenReport.FromTexts(
    toolName: "Prompt Compressor",
    textBefore: originalText,
    textAfter: compressedText,
    notes: "5 filler phrases, 1 code block stripped.");

// Emit it however you need
Console.Error.WriteLine(report.OneLineSummary());
Console.Error.WriteLine(report.DetailedBlock());

// Or with explicit token counts (e.g. from the Roslyn emitter)
var report2 = new TokenReport(
    ToolName: "Focused Emitter",
    TokensWithoutTool: 835,
    TokensWithTool: 415,
    Notes: "Focus method: OnInitializedAsync");
```

## Usage in Blazor (Prompt Coach)

```razor
@using TokenStats

<TokenComparison Report="@_report" ShowCost="true" />

@code {
    private TokenReport _report = TokenReport.FromTexts(
        "Prompt Compressor", _input, _result.Compressed,
        notes: $"{_result.Stages.Count} rules applied.");
}
```

## The honest design choice

Cost framing is **off by default**. Showing "$0.0013 saved" per call sets
up the wrong expectation — that this is about money. It's not, mostly.
The savings are about discipline, audit-ability, and at scale, real
infrastructure load. The cost line is available when you want to make
a specific point, but not the headline.

The headline is the bars. Visual comparison teaches faster than numbers.

## Why this matters more than it sounds

The reason to put this in every tool is the same reason fitness apps show
calorie counts on every meal: **the user's behavior changes when the cost
is visible**. The first time someone sees their casual prompt was 2,400
tokens and the focused version was 280, they internalize something that
no amount of "you should write tighter prompts" advice would teach.

The tools we've built are all token-savers. This makes them token-savings
*teachers*. That's the whole point of the project, restated as a UI choice.

And the savings add up beyond the individual. Every token not processed is
a small reduction in GPU compute and energy drawn from the grid. The
community's collective impact is tracked at
**[tokensavermcp.com](https://tokensavermcp.com)** — a live dashboard where
you can see how much the ecosystem has saved in total. You are not only
getting faster answers; you are contributing to more efficient AI
infrastructure.
