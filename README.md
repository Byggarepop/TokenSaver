# TokenSaver

A **structural warm start** for AI coding agents in .NET. Instead of loading whole files into your assistant, TokenSaver hands it a cheap map of your code — every type and member as a signature, each tagged with its line range — so the model reads only the slice it needs instead of slurping the file. Built on the Roslyn compiler platform.

**Where it pays off:** outlining a file costs **70–95% fewer tokens** than reading it — up to **90% on a large file**. The end-to-end win is biggest on **smaller / cheaper models** (which over-read the most) and on **large codebases**: on real tasks it trims a Haiku-class model's token use by ~8%, and the savings climb with file size. A top-tier model already reads tightly, so it sees less benefit — the leaner the model, the more a warm start helps.

Works with **Visual Studio 2026** (GitHub Copilot Chat), **Claude Code**, VS Code Copilot, Claude Desktop, and any other MCP client that speaks stdio.

→ **Full docs and setup guide:** [mcp/README.md](mcp/README.md)  
→ **Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## Install

See **[tokensavermcp.com/install](https://tokensavermcp.com/install)** for one-click install buttons, per-client config snippets, upgrade/uninstall instructions, and troubleshooting.

---

## What the tools do

<!-- BEGIN:generated:tools -->
### Single-file tools

| Tool | What it does | Reduction |
|---|---|---|
| `OutlineCSharpFile` | Signatures of every type and member — no bodies. Best for "what's in this file?" | 70–95% |
| `MinifyFile` | Lossless minify of an entire file — strips comments and whitespace, logic unchanged. Auto-dispatches by extension (C#, Razor, JS/TS, Python, HTML, CSS, JSON, YAML, XML, C, C++, X++, VB.NET). | 20–50% |

\* Reductions are measured against reading the **whole file** — the real alternative when you'd otherwise load it. The **end-to-end** saving on a task is smaller, because a capable model already reads somewhat selectively; it is largest on smaller/cheaper models and large files (see *Token savings in practice* below).

### Cross-file traversal tools

These scan an entire project directory in one call — no need to know which file to look in first.

| Tool | What it does |
|---|---|
| `TraceDiRegistrations` | Finds every Dependency-Injection registration referencing a type (interface or concrete) and returns a compact table of `file:line`, method, `ServiceType -> ImplType`, and key. Answers "where is IFoo wired, and to what?" |

Both accept a directory path or `.csproj` file — `obj/` and `bin/` are excluded automatically.
<!-- END:generated:tools -->

---

## Token savings in practice

Measured against this project's own `FocusedEmitter.cs` (9,261 tokens raw):

| Question type | Tool | Tokens sent | Reduction |
|---|---|---|---|
| "What's in this file?" | `OutlineCSharpFile` | 1,039 | **89%** |
| "Read one method body" | `OutlineCSharpFile` + narrow `Read` of the `// L..` range | ~300 | **~95%** |
| "Audit the whole file" | `MinifyFile` | 5,525 | 40% |

Those are per-read figures versus loading the whole file. The **end-to-end** saving on a real task is smaller, because a capable model already reads somewhat selectively — and that's the honest part of the story:

| Model running the agent | Real-task token saving vs no tool |
|---|---|
| Top-tier (reads tightly already) | ~1% — negligible |
| Mid-tier (Sonnet-class) | ~5–7% |
| **Smaller / cheaper (Haiku-class)** | **~8%** |

The leaner the model, the more it over-reads on its own — and the more a warm start saves. TokenSaver is most worth it for **agents running on small/cheap models**, **large files**, or any workflow that would otherwise read whole files. It won't make a top model meaningfully cheaper, and it won't make a model write better code — it curbs wasteful reading, which is where cheap-model token budgets actually leak.

---

## Collective impact

Every invocation is counted at **[tokensavermcp.com](https://tokensavermcp.com)** — a live dashboard showing total tokens saved by the community. Fewer tokens processed means less GPU compute and a smaller carbon footprint for AI-assisted development.

---

## Language support

| Tier | Languages |
|---|---|
| **Primary** (Roslyn, full support) | C# `.cs`, Razor `.razor`, VB.NET `.vb`, .NET project files `.csproj .props .config .xml` |
| **Basic** (comment-strip + whitespace collapse) | JavaScript, TypeScript, Python, HTML, CSS/SCSS/LESS, JSON/JSONC, YAML, C, C++, X++ |

`OutlineCSharpFile` and `TraceDiRegistrations` are **C# only**; `MinifyFile` works across every supported language.
