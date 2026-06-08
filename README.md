# TokenSaver

An MCP server for **.NET developers** that gives your AI assistant a token-efficient view of C#, Razor, and .NET project files using the Roslyn compiler platform. Typical reduction: **50–95%** on C# files with no loss of logic.

Works with **Visual Studio 2026** (GitHub Copilot Chat), **Claude Code**, VS Code Copilot, Claude Desktop, and any other MCP client that speaks stdio.

→ **Full docs and setup guide:** [mcp/README.md](mcp/README.md)  
→ **Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## Install

See **[tokensavermcp.com/install](https://tokensavermcp.com/install)** for one-click install buttons, per-client config snippets, upgrade/uninstall instructions, and troubleshooting.

---

## What the tools do

### Single-file tools

| Tool | What it does | Reduction |
|---|---|---|
| `OutlineCSharpFile` | Signatures of every type and member — no bodies. Best for "what's in this file?" | 70–95% |
| `FocusMethod` | Named method with full body + signatures of referenced members. `depth=1` includes private helpers. | 15–40%* |
| `FocusMultipleMethods` | Same as above but multiple methods in one parse pass — deduplicates shared signatures. | 15–40%* |
| `FocusType` | Non-private members with full bodies, private members as signatures only. | 15–40%* |
| `FocusCallers` | All methods in a file that call a given method — focused view. Answers "what calls X?" | 15–40%* |
| `MinifyCSharpFile` | Lossless minify of an entire C# file — strips comments and whitespace, logic unchanged. | 20–50% |
| `MinifyFile` | Auto-dispatch by extension. Covers C#, Razor, JS/TS, Python, HTML, CSS, JSON, YAML, XML, C, C++, X++, VB.NET. | varies |
| `AliasCSharpFile` | Minify + rename private symbols to short codes (`M1`, `P1`...). Best on files with very long private names. | 30–60% |

\* **Focus-tool figures are conservative** — measured against reading only the *relevant code* (the method plus the helpers it needs), not the whole file. Compared to reading the entire file the reduction is far higher (often 80–95%), but a careful reader wouldn't load the whole file to answer a single-method question. The whole-file tools (`OutlineCSharpFile`, `MinifyCSharpFile`) are measured against the whole file, since that *is* the real alternative.

### Cross-file traversal tools

These scan an entire project directory in one call — no need to know which file to look in first.

| Tool | What it does |
|---|---|
| `TraceCallers(projectPath, methodName)` | Finds every `.cs` file across the project where `methodName` is called, and returns a focused view of each caller method. Answers "what calls X across the whole codebase?" |
| `TraceImplementors(projectPath, interfaceName)` | Finds every type that implements or extends a named interface/base across the project, and returns a focused type view for each. Answers "what implements IFoo?" |
| `TraceDiRegistrations(projectPath, typeName)` | Finds every Dependency-Injection registration referencing a type (interface or concrete) and returns a compact table of `file:line`, method, `ServiceType -> ImplType`, and key. Answers "where is IFoo wired, and to what?" |
| `MapProject(projectPath, nameFilter?)` | Maps every type in the project to its `file:line`, kind, and base list — a compact index for locating a type when you don't know its file. Pass `nameFilter` to narrow. **Disabled by default** (an unfiltered map can be large); enable with the `TOKENSAVER_ENABLE_MAP_PROJECT=1` environment variable. |

Both accept a directory path or `.csproj` file — `obj/` and `bin/` are excluded automatically.

---

## Token savings in practice

Measured against this project's own `FocusedEmitter.cs` (9,261 tokens raw):

| Question type | Tool | Tokens sent | Reduction |
|---|---|---|---|
| "What's in this file?" | `OutlineCSharpFile` | 1,039 | **89%** |
| "Explain the `Emit` method" | `FocusMethod` (depth=1, minify) | 1,437 | **84%** |
| "Explain `EmitOutline` and `EmitMinified`" | `FocusMultipleMethods` | 424 | **95%** |
| "Audit the whole file" | `MinifyCSharpFile` | 5,525 | 40% |

---

## Collective impact

Every invocation is counted at **[tokensavermcp.com](https://tokensavermcp.com)** — a live dashboard showing total tokens saved by the community. Fewer tokens processed means less GPU compute and a smaller carbon footprint for AI-assisted development.

---

## Language support

| Tier | Languages |
|---|---|
| **Primary** (Roslyn, full support) | C# `.cs`, Razor `.razor`, VB.NET `.vb`, .NET project files `.csproj .props .config .xml` |
| **Basic** (comment-strip + whitespace collapse) | JavaScript, TypeScript, Python, HTML, CSS/SCSS/LESS, JSON/JSONC, YAML, C, C++, X++, Markdown |

Cross-file traversal tools (`TraceCallers`, `TraceImplementors`, `TraceDiRegistrations`, `MapProject`) are **C# only**.
