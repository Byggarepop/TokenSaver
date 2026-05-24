# TokenSaver MCP — Benchmark Case Study

## Task: Rename a private method across a 1,000-line C# file

A real refactor on a real file in the TokenSaver codebase itself:
rename `BuildNotes` → `FormatNotes` across `FocusedEmitter.cs` (1,004 lines, ~46 KB).

The same task was performed twice by Claude Sonnet 4.6 — once using TokenSaver tools,
once using only the built-in file reader — and the token cost of each file-read
step was recorded.

---

## Round 1 — With TokenSaver

| Step | Tool | Tokens consumed |
|---|---|---|
| Navigate: find the method | `outline_c_sharp_file` | **1,074** |
| Understand: read method body + helpers | `focus_method` (depth=1, minify) | **208** |
| Locate call sites | `Grep` + 3 targeted line reads | **~240** |
| **Total** | | **~1,522 tokens** |

The outline took 11 seconds. The focused method view was 12 lines of dense,
comment-stripped code. The three targeted reads fetched exactly the lines
needed for the `Edit` calls — nothing more.

---

## Round 2 — Without TokenSaver

| Step | Tool | Tokens consumed |
|---|---|---|
| Navigate + understand + locate call sites | `Read` (whole file) | **~11,568** |
| **Total** | | **~11,568 tokens** |

One `Read` call was sufficient because the entire file landed in context at once.
But the cost of that convenience is that 11,568 tokens now occupy the context
window for the rest of the session — regardless of how little of the file was
actually relevant.

---

## Comparison

```
Round 1 (TokenSaver):   ████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   1,522 tokens
Round 2 (Read):         ████████████████████████████████████████████  11,568 tokens
```

| Metric | Value |
|---|---|
| Tokens saved per task | **~10,046** |
| Reduction | **87%** |
| Result quality | Identical — same three edits, same correct outcome |

---

## Why it compounds

Context window consumption is permanent within a session. Every token read
into context stays there. On a 200,000-token context budget:

- **Without TokenSaver:** one whole-file read per task; after ~17 tasks on
  similarly-sized files, the context is full and the session must restart.
- **With TokenSaver:** the same budget supports **~130 tasks** — a 7× increase
  in how much work fits in a single session before compaction or restart.

For agentic workflows (automated PR review, codebase-wide refactors, CI
pipelines), this directly translates to fewer API calls, lower cost per task,
and longer uninterrupted runs.

---

## "But the file is already in context" — the caching caveat

A reasonable objection: once the whole file has been read once, subsequent tasks
on the same file cost nothing extra. Doesn't the whole-file approach win after
enough tasks?

**No — the blob is never free.** Every API call re-processes all tokens in
context through the attention mechanism, including the file content, whether
it's relevant to the current task or not. The 11,568 tokens don't sit idle;
they're re-charged as input on every subsequent turn.

The one real exception is **prompt caching**. When Anthropic's cache is warm,
cached tokens cost roughly 10% of normal input price. In that window a
previously-read file is genuinely cheaper to re-process than raw token counts
suggest. But this caveat is narrow:

- **The cache TTL is 5 minutes.** It expires quickly and any change to the
  message prefix (a tool call result, a user reply) can invalidate it.
- **Cost vs. context size are separate problems.** Caching reduces the dollar
  cost of re-processing, but the 11,568 tokens still occupy the context window
  in full — crowding out other relevant code, tests, or tool results.
- **It only applies to one file.** Real sessions span multiple files.
  The blob helps re-reads of that one file; TokenSaver brings each file in at
  the size it needs to be, every time.

In practice there is no crossover point where a whole-file read wins on a
per-session basis. Even with a warm cache, the opportunity cost — context
window space that cannot be used for anything else — offsets the per-token
price discount.

---

## Methodology notes

- Token counts for TokenSaver tool outputs are taken directly from the tool's
  own header: `// Tokens without tool: 11,568 → with tool: 1,074 (90% saved)`.
  These are computed by the server using a standard tokenizer estimate (chars ÷ 4).
- The `Read` token count is derived from the same formula applied to the raw
  file size reported by the tool.
- Both rounds were performed in the same session by the same model on the same
  file, with no caching or pre-loading advantage for either approach.
- The result of both rounds was verified to be identical: three correct `Edit`
  calls, zero errors.

---

---

## Case Study 2: Add a new method to an existing class

**Task:** Add a `Prune(int keepDays)` method to `ReportWriter.cs` — removes entries
older than N days from the JSON report file, following the same lock + load pattern
as the existing `Append` method.

This exercise was performed live. File: `Reporting/ReportWriter.cs` (88 lines).
Every token count below is taken directly from the tool's own header line, e.g.
`// Tokens without tool: 682 → with tool: 164 (75% saved)`. No estimates.

---

### Agentic flow — With TokenSaver

| Step | Tool | Token count | Source |
|---|---|---|---|
| Navigate the class | `outline_c_sharp_file` | **164** | Tool header: `with tool: 164` |
| Read `Append` + `LoadOrRecover` bodies | `focus_method` (depth=1, minify) | **288** | Tool header: `with tool: 288` |
| Read raw file for `Edit` | `Read` | **682** | Tool header: `without tool: 682` (same file, no MCP reduction possible here) |
| **Total** | | **1,134** | |

---

### Agentic flow — Without TokenSaver

| Step | Tool | Token count | Source |
|---|---|---|---|
| Read the file | `Read` | **682** | Tool header: `without tool: 682` |
| **Total** | | **682** | |

One Read call gives everything an agent needs: class structure, method bodies, and exact
text for the `Edit` call.

---

### Comparison

```
With TokenSaver:    ████████████████████████████████████░░░░░░░   1,134 tokens
Without TokenSaver: █████████████████████░░░░░░░░░░░░░░░░░░░░░     682 tokens
```

| Metric | Value |
|---|---|
| Overhead | **+452 tokens (+66%)** |
| Result quality | Identical — one correct `Edit`, zero errors |

**For small files, TokenSaver adds overhead on edit tasks.** The outline and focus steps
cost 452 tokens to produce understanding that a single `Read` would also provide — and
the Edit-prep `Read` (682 tokens) cannot be eliminated regardless. The overhead only
pays off when the file is large enough that the Edit-prep step can be a targeted partial
read rather than a full file read.

The numbers from Case Study 1 illustrate the crossover: `FocusedEmitter.cs` costs
11,568 tokens to `Read` in full, but only 1,074 to outline and 208 to focus on the
target method. On a file that size, outline + focus + a targeted partial read of the
insertion region comes in well under 2,000 tokens — an 83%+ reduction.

---

## Case Study 3: Answer a code-review question about a method

**Task:** A reviewer asks: "Can we avoid re-serializing the entire file on every
`Append` call?" Determine whether the concern is valid by reading the write path.

This is a pure comprehension task — no edit. File: `Reporting/ReportWriter.cs`.

---

All token counts below are taken directly from the tool's own header line. No estimates.

### Agentic flow — With TokenSaver

| Step | Tool | Token count | Source |
|---|---|---|---|
| Read `Append` + `LoadOrRecover` bodies | `focus_method` (depth=1, minify) | **288** | Tool header: `with tool: 288` |
| **Total** | | **288** | |

Result: every `Append` call deserializes the whole file, appends one entry, then
re-serializes it in full — confirmed O(n) write. The concern is valid.

---

### Agentic flow — Without TokenSaver

| Step | Tool | Token count | Source |
|---|---|---|---|
| Read the file | `Read` | **682** | Tool header: `without tool: 682` |
| **Total** | | **682** | |

Only ~30 lines across two methods are relevant to the question. The remaining 58 lines
enter context regardless.

---

### Comparison

```
With TokenSaver:    ████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   288 tokens
Without TokenSaver: ████████████████████████████░░░░░░░░░░░░░░░   682 tokens
```

| Metric | Value |
|---|---|
| Tokens saved | **394** |
| Reduction | **58%** |
| Result quality | Identical — same correct analysis |

Comprehension tasks have no unavoidable Edit-prep Read, so TokenSaver captures the
full saving. The smaller and more focused the question, the larger the proportional win.

---

## Test file

`Emitters/FocusedEmitter.cs` — 1,004 lines, part of the open-source
[TokenSaver](https://github.com/Byggarepop/TokenSaver) project.
The file implements the Roslyn-based focused emitter that powers the
TokenSaver MCP server itself.
