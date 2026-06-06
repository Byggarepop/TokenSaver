# Copilot Instructions

## Token-efficient source context via the `tokensaver` MCP server

This workspace has the `tokensaver` MCP server registered. It exposes
**eight** tools that produce token-reduced views of source files, plus two
**cross-file traversal tools** for project-wide queries. **Prefer these
tools over reading whole files** — they typically save 30-95% of tokens
with no loss of logic.

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
| Markdown | `.md`, `.markdown` | HTML comments stripped, blank-run collapse |

### Tool selection rules — follow these by default, no need to ask

1. **The user wants codebase navigation** ("what's in this file?", "where would
   I add X?", "list the methods on `Foo`") → call `OutlineCSharpFile`.
   Signatures only, no bodies, typical 70-95% reduction. C# only.

2. **The user references a specific C# method** ("look at `Foo` in `Bar.cs`",
   "speed up `OnInitializedAsync`", "translate this WinForms method to Razor")
   → call `FocusMethod` with `methodName` set, `depth=1`, and `minify=true`.
   Use `depth=1` so you see the bodies of private helpers the focus method
   calls — without those, your suggestions will hallucinate helper logic.

   **`methodName` also accepts a class name to target a constructor** — e.g.
   `FocusMethod(filePath, "MyService")` returns the `MyService(...)` constructor body.

   **The user references two or more C# methods at once**, or you already know
   from a prior outline/NOT FOUND which methods are relevant
   → call `FocusMultipleMethods` with a comma-separated `methodNames` list
   (e.g. `"ExecSql,ClearGrid,SetBusy"`). The file is parsed once and shared
   signatures are deduplicated — smaller output than N separate `FocusMethod`
   calls and one round-trip instead of N. Class names (constructors) are
   accepted alongside method names.

   **On a `NOT FOUND`, act on any hint in the response.** When the file's type
   is `partial`, the member may be in a sibling file in the same namespace/folder
   — glob that folder for the type's other parts and focus the right one. When
   the type has a base list, the member may be inherited — focus the file that
   declares the base type. Don't give up or guess the body.

3. **The user wants you to read or analyze a whole file of any supported type**
   → call `MinifyFile`. It auto-dispatches by extension and works for every
   format in the table above. For C# specifically, `MinifyCSharpFile` is
   equivalent (back-compat).

4. **The user wants to understand a specific C# class** ("explain class X",
   "what does FooService do?", "show me the public API of Bar") and either the
   file has multiple types or you want to skip private implementation noise
   → call `FocusType` with the simple class/record/interface name. Shows all
   non-private members with full bodies and private members as signatures only.
   Cheaper than `MinifyCSharpFile` when private methods dominate file length.

5. **The user asks what calls a given method and you don't yet know which
   methods are the callers** ("where is X used?", "what calls BuildHeader?",
   "who invokes OnSave?")
   → call `FocusCallers` for **discovery only**. Once you know the caller
   names (e.g. from a prior outline or focus result), stop — use
   `FocusMultipleMethods` on the known names instead. Never call `FocusCallers`
   when the callers' bodies are already in context. Avoid it when callers are
   large methods: the tool emits their full bodies and savings drop to ~0%.

6. **The user is working with a C# file dominated by long private symbol names**
   (repositories, validators, mappers with verbose internal naming)
   → consider `AliasCSharpFile` instead. The result has private members
   renamed to short codes (M1, P1, F1...) with a ledger at the top. Worth it
   only when private names are long; on small files the ledger overhead can
   wipe out the savings. C# only — no equivalent for other languages.

7. **The user asks what calls a given method across the whole project**
   ("what calls X anywhere?", "find all callers of BuildHeader", "who calls
   this across the codebase?") → call `TraceCallers` with the project directory
   or `.csproj` path and the method name. Returns focused caller views from
   every file that calls it, in one call. Use instead of `FocusCallers` when
   you don't know which file to look in. **C# only.**
   **Exception — existence checks**: if the question is "is X used?", "is X
   called anywhere?", or "does anything reference X?", use `Grep` first. Only
   escalate to `TraceCallers` if you need to see HOW callers use the method,
   not just confirm it is called. A widely-used method can cost 100K+ tokens
   with TraceCallers.

8. **The user asks what implements an interface or extends a base type**
   ("what implements IFoo?", "what extends BaseBar?", "show me all emitters")
   → call `TraceImplementors` with the project directory or `.csproj` path
   and the interface/base type name. Returns a focused type view for each
   implementor found across the project. **C# only.**

### Note on `#` references and Active Document (user-facing reminder)

VS Copilot's `#FileName.cs` syntax and the **Active Document** context button
both inline the entire file content into the prompt **before** Copilot sees
the message. The MCP tool can't intercept that — by the time the model decides
whether to call a tool, the file is already in context. Result: both bypass
our token-reduction tools entirely and send the full raw file to the model.

**To benefit from token reduction, type the file or method name as plain
text and remove any Active Document / `#` reference** — e.g.
`"Why does OnRunSql hang on the second call in SqlQuery.razor?"`.
Copilot will then invoke `focus_method` and only pull in the relevant slice.
Reserve `#` references or Active Document for small files where reduction
doesn't matter.

### Important: the tool output is a summary view, not the source of truth

When `MinifyCSharpFile`, `AliasCSharpFile`, or `FocusMethod` (with `minify=true`)
return code, the result has been **transformed for token efficiency**:
- Comments and XML doc comments are stripped.
- `#region` / `#endregion` directives are stripped — pure organisation, no logic.
- Field signatures omit initializers (e.g. `private int _count;` not `= 0`).
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

If acting in agent / edit mode (directly modifying files), comprehension
still goes through a tokensaver tool **first** — never read a supported file
just to understand it before editing. Only after the tool has shown you the
target do you read from disk, and then only the lines containing the match
string (the insertion region ±5) — never the whole file, and never before
the tool. Tool output is a reasoning aid, not a representation of the file's
real content.

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
// session: 4 calls · raw saved 24,800 · net of 2,100 one-time MCP overhead = 22,700
```
The first line compares against reading the **whole file** (a best case); the
second, when present, compares against reading **only the relevant code** (a
careful reader's real alternative). **Mention the savings to the user** — it's
part of the value — but be honest: don't present the whole-file figure as if it
were guaranteed. If the second line is present, prefer it or give the range, e.g.
"Used the focused emitter — saved ~25-55% tokens vs. reading the file."

If you view the **same file more than once** in a session (a different method, or
outline-then-minify), the first line is replaced by `repeat view of this file
this session — whole-file baseline already counted ...`. A file only costs its
whole-file tokens once, so later views aren't credited that saving again and the
session total counts each file's baseline a single time. Don't re-report the
whole-file "% saved" for a repeat view — it was already counted on the first.
