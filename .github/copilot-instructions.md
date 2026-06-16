# Copilot Instructions

## Token-efficient source context via the `tokensaver` MCP server

This workspace has the `tokensaver` MCP server registered. It gives the model
a cheap **structural warm start** in an unfamiliar codebase and then gets out
of the way. It exposes **three** tools. Prefer them for the cases below; for
everything else, your own `Grep` plus a narrow `Read` (the relevant lines only)
is the leanest path — don't reach for a tool when a targeted read will do.

> **Visual Studio 2026 — use Copilot Chat.**  MCP tools fire in both Ask
> and Agent mode. Open the Copilot Chat panel and ask your question there;
> inline completions do not invoke MCP tools.

### Supported file types (via `MinifyFile`)

| Format | Extensions | Method |
|---|---|---|
| C# | `.cs`, `.razor.cs` | Roslyn syntax-tree minify |
| Razor | `.razor` | Markup (HTML) + @code (Roslyn), combined |
| JavaScript | `.js`, `.mjs`, `.cjs`, `.jsx` | Lexical strip + collapse |
| TypeScript | `.ts`, `.tsx`, `.mts`, `.cts` | Lexical strip + collapse |
| Python | `.py`, `.pyi` | `#` strip, indent preserved |
| HTML | `.html`, `.htm` | `<!-- -->` strip, whitespace collapse |
| CSS / SCSS / LESS | `.css`, `.scss`, `.less` | `/* */` strip, whitespace collapse |
| JSON / JSONC | `.json`, `.jsonc` | Whitespace collapse + comment strip |
| YAML | `.yaml`, `.yml` | `#` strip, indent preserved |
| XML / project files | `.xml`, `.csproj`, `.props`, `.targets`, `.config`, `.resx` | `<!-- -->` strip, blank-run collapse |
| C | `.c`, `.h` | `//` + `/* */` strip, whitespace collapse, `#directives` preserved |
| C++ | `.cpp`, `.cc`, `.cxx`, `.hpp`, `.hh`, `.hxx`, `.inl` | same as C |
| X++ | `.xpp` | `//` + `/* */` strip, whitespace collapse, `#macro` directives preserved |
| VB.NET | `.vb` | Roslyn comment strip (`'` and `REM`), blank-run collapse |

### Tool selection rules — follow these by default, no need to ask

1. **Orient in a C#/VB file** — "what's in this file?", "where would I add X?",
   or before editing any file ≥50 lines → call `OutlineCSharpFile`. Returns
   every type and member as a signature, NO bodies (typical 70-95% reduction),
   each tagged with its source line range (`// L31-44`). **To then read a body,
   `Read` that exact range** (offset+limit) — do not re-read the whole file.
   C#/VB only.

2. **Read or compress a whole file of any supported type** → call `MinifyFile`.
   Auto-dispatches by extension (see the table above) and strips
   comments/whitespace losslessly. Use it for non-C# files, or when you
   genuinely need a whole C# file rather than its skeleton. For C#, prefer
   `OutlineCSharpFile` — it saves far more.

3. **Where is a type wired in Dependency Injection, and to what / what
   lifetime?** ("where is `IFoo` registered?", "is `Foo` a singleton?"), or a
   constructor caller-trace for a DI-constructed type came back empty (the
   container builds it, no `new`) → call `TraceDiRegistrations` with the project
   directory or `.csproj` path and the type name (interface OR concrete).
   Returns a compact table of every `Add`/`TryAdd`/`AddKeyed` registration:
   `file:line`, method, `ServiceType -> ImplType`, lifetime, keyed key. **C#
   only** — this is the one thing `Grep` cannot answer cleanly.

**Everything else is `Grep` + a narrow `Read`.** After an outline you have each
member's line range — read exactly that range to see one body, or grep the
folder to find callers/implementors/usages. Don't reach for a tool when a
targeted read or grep already answers the question.

### Note on `#` references and Active Document (user-facing reminder)

VS Copilot's `#FileName.cs` syntax and the **Active Document** context button
both inline the entire file content into the prompt **before** Copilot sees
the message. The MCP tool can't intercept that — by the time the model decides
whether to call a tool, the file is already in context. Result: both bypass
our token-reduction tools entirely and send the full raw file to the model.

**To benefit from token reduction, type the file or method name as plain
text and remove any Active Document / `#` reference** — e.g.
`"Why does OnRunSql hang on the second call in SqlQuery.razor?"`.
Copilot will then invoke `outline_c_sharp_file` and you read only the relevant
slice. Reserve `#` references or Active Document for small files where
reduction doesn't matter.

### Important: the tool output is a summary view, not the source of truth

When `MinifyFile` or `OutlineCSharpFile`
return code, the result has been **transformed for token efficiency**:
- Comments and XML doc comments are stripped.
- `#region` / `#endregion` directives are stripped — pure organisation, no logic.
- Field signatures omit initializers (e.g. `private int _count;` not `= 0`).
- Indentation and blank lines are collapsed.

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
4. **Use the original symbol names** from the file when writing code the user
   will paste into their file.

If acting in agent / edit mode (directly modifying files), comprehension
still goes through a tokensaver tool **first** — never read a supported file
just to understand it before editing. Only after the tool has shown you the
target do you read from disk, and then only the lines containing the match
string (the insertion region ±5) — never the whole file, and never before
the tool. Tool output is a reasoning aid, not a representation of the file's
real content.

**Mid-edit-flow is the trap, not the first read.** The requirement is
per-file, every time — each new supported file you open for comprehension
resets it. Having used a tool earlier in the task, or having edited another
file already, does not license a raw read of the next file just to understand
it. That momentum hits hardest in the second half of a task; that is exactly
when to run the tool instead.

### When NOT to use these tools

- File type not in the supported table above (e.g. `.txt`, `.sql`, binary).
- The user explicitly asks you to read the raw file.
- The file is already small (< 50 lines).
- You need exact on-disk text for an `Edit` call — but only as a *second* step,
  after a tokensaver tool has located the target. Then read only the lines
  around the insertion point (±5), never the whole file, and never before the
  tool. Comprehension always goes through a tool first.

### Reporting

Each tool returns a token-comparison header. For the focused tools it has up to
three lines:
```
// [Focused Emitter] Tokens without tool: 7,083 → with tool: 3,133 (55% saved)
// vs a targeted read of just the relevant code (4,200 tokens): 25% saved
// session: 4 calls · saved 24,800 · net 22,700 after 2,100 overhead
```
The first line compares against reading the **whole file** (a best case); the
second, when present, compares against reading **only the relevant code** (a
careful reader's real alternative). **Mention the savings to the user** — it's
part of the value — but be honest: don't present the whole-file figure as if it
were guaranteed. If the second line is present, prefer it or give the range, e.g.
"Used the focused emitter — saved ~25-55% tokens vs. reading the file."

If you view the **same file more than once** in a session (a different method, or
outline-then-minify), the first line is replaced by `repeat view — whole-file
baseline already counted; adds N tokens ...`. A file only costs its
whole-file tokens once, so later views aren't credited that saving again and the
session total counts each file's baseline a single time. Don't re-report the
whole-file "% saved" for a repeat view — it was already counted on the first.
