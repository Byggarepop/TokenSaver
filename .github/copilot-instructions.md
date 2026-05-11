# Copilot Instructions

## Token-efficient C# context via the `roslyn-lean` MCP server

This workspace has the `roslyn-lean` MCP server registered. It exposes three
tools that produce a **token-reduced** view of C# files. **Prefer these tools
over reading whole files** whenever the task involves C# source — they save
50-70% of tokens on typical files with no loss of logic.

### Tool selection rules — follow these by default, no need to ask

1. **The user references a specific method** ("look at `Foo` in `Bar.cs`",
   "speed up `OnInitializedAsync`", "translate this WinForms method to Razor")
   → call `FocusMethod` with `methodName` set, `depth=1`, and `minify=true`.
   Use `depth=1` so you see the bodies of private helpers the focus method
   calls — without those, your suggestions will hallucinate helper logic.

2. **The user wants you to read or analyze a whole C# file** without naming a
   specific method
   → call `MinifyCSharpFile`. Lossless, ~20-50% reduction.

3. **The user is working with a file dominated by long private symbol names**
   (repositories, validators, mappers with verbose internal naming)
   → consider `AliasCSharpFile` instead of `MinifyCSharpFile`. The result has
   private members renamed to short codes (M1, P1, F1...) with a ledger at the
   top. Worth it only when private names are long; on small files the ledger
   overhead can wipe out the savings.

### Note on `#` references (user-facing reminder)

VS Copilot's `#FileName.cs` syntax inlines the entire file content into the
prompt **before** Copilot sees the message. The MCP tool can't intercept
that — by the time the model decides whether to call a tool, the file is
already in context. Result: `#FileName.cs` bypasses our token-reduction
tools entirely.

**To benefit from token reduction, type the file path as plain text** — e.g.
`Analyze InspectionReport.cs` or `Look at C:\path\to\Foo.cs`. Reserve `#`
references for small files where reduction doesn't matter.

### Important: the tool output is a summary view, not the source of truth

When `MinifyCSharpFile`, `AliasCSharpFile`, or `FocusMethod` (with `minify=true`)
return code, the result has been **transformed for token efficiency**:
- Comments and XML doc comments are stripped.
- Indentation and blank lines are collapsed.
- In `AliasCSharpFile`, private symbols are renamed to short codes.

The **actual file on disk** is conventionally formatted: standard 4-space
indentation, blank lines between members, XML doc comments on public APIs,
and original symbol names. The tool output is a *projection* of the file,
not its real shape.

**When suggesting code or making edits, always:**
1. **Format suggested code in conventional, idiomatic C# style** — proper
   indentation, blank lines between members, no minification.
2. **Preserve existing comments and XML docs** when modifying a method. If
   the tool output doesn't show them, they still exist in the real file —
   read the file from disk before editing if needed.
3. **Add XML doc comments to new public methods/properties** following the
   project's convention.
4. **Use original symbol names**, not the M1/P1/F1 aliases from the ledger,
   when writing code the user will paste into their file. Use the ledger
   only to understand the relationships.

If acting in agent / edit mode (directly modifying files), **always read
the actual file from disk before applying changes** rather than relying on
tool output as the basis for the edit. Tool output is a reasoning aid,
not a representation of the file's real content.

### When NOT to use these tools

- Non-C# files (`.razor` templates, `.json`, `.csproj`, etc.) — read normally.
- The user explicitly asks you to read the raw file.
- The file is already small (< 50 lines).

### Reporting

Each tool returns a header like
`// [Focused Emitter] Tokens without tool: 7,083 → with tool: 3,133 (55% saved)`.
**Mention the savings to the user in your reply.** It's part of the value —
the user wants visibility into how much context was reduced. One short
sentence is enough, e.g. "Used the focused emitter — saved ~55% tokens vs.
reading the whole file."
