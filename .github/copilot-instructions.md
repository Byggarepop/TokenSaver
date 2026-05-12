# Copilot Instructions

## Token-efficient source context via the `roslyn-lean` MCP server

This workspace has the `roslyn-lean` MCP server registered. It exposes
**five** tools that produce token-reduced views of source files. **Prefer
these tools over reading whole files** — they typically save 30-70% of
tokens with no loss of logic.

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

### Tool selection rules — follow these by default, no need to ask

1. **The user wants codebase navigation** ("what's in this file?", "where would
   I add X?", "list the methods on `Foo`") → call `OutlineCSharpFile`.
   Signatures only, no bodies, typical 70-95% reduction. C# only.

2. **The user references a specific C# method** ("look at `Foo` in `Bar.cs`",
   "speed up `OnInitializedAsync`", "translate this WinForms method to Razor")
   → call `FocusMethod` with `methodName` set, `depth=1`, and `minify=true`.
   Use `depth=1` so you see the bodies of private helpers the focus method
   calls — without those, your suggestions will hallucinate helper logic.

3. **The user wants you to read or analyze a whole file of any supported type**
   → call `MinifyFile`. It auto-dispatches by extension and works for every
   format in the table above. For C# specifically, `MinifyCSharpFile` is
   equivalent (back-compat).

4. **The user is working with a C# file dominated by long private symbol names**
   (repositories, validators, mappers with verbose internal naming)
   → consider `AliasCSharpFile` instead. The result has private members
   renamed to short codes (M1, P1, F1...) with a ledger at the top. Worth it
   only when private names are long; on small files the ledger overhead can
   wipe out the savings. C# only — no equivalent for other languages.

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

- File type not in the supported table above (e.g. `.md`, `.txt`, binary).
- The user explicitly asks you to read the raw file.
- The file is already small (< 50 lines).
- You need exact on-disk text for an `Edit` call — read raw so the diff matches.

### Reporting

Each tool returns a header like
`// [Focused Emitter] Tokens without tool: 7,083 → with tool: 3,133 (55% saved)`.
**Mention the savings to the user in your reply.** It's part of the value —
the user wants visibility into how much context was reduced. One short
sentence is enough, e.g. "Used the focused emitter — saved ~55% tokens vs.
reading the whole file."
