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
- `FocusedEmitter.cs` has grown since this benchmark was first run. The current
  file measures `without tool: 11,714` and outline `with tool: 1,089`. The
  numbers above are from the original session and remain valid as a record of
  that run.

---

## Case Study 2: Add a new method to an existing class

**Task:** Add a `Prune(int keepDays)` method to `ReportWriter.cs` — removes entries
older than N days from the JSON report file, following the same lock + load pattern
as the existing `Append` method.

This exercise was performed live. File: `Reporting/ReportWriter.cs` (88 lines, 682
raw tokens per MCP tool header).

---

### Agentic flow — With TokenSaver

The task already names the method to follow (`Append`), so `outline_c_sharp_file` is
skipped — go straight to `focus_method`. For the `Edit` call, only the exact lines
around the insertion point are read, not the whole file.

| Step | Tool | Token count | Source |
|---|---|---|---|
| Understand `Append` + `LoadOrRecover` | `focus_method` (depth=1, minify) | **288** | Tool header: `with tool: 288` |
| Get exact insertion text | `Read` 7 lines around insertion point | **~49** | 197 chars ÷ 4 (Read has no token header) |
| **Total** | | **~337** | |

---

### Agentic flow — Without TokenSaver

| Step | Tool | Token count | Source |
|---|---|---|---|
| Read the file | `Read` | **682** | Tool header: `without tool: 682` |
| **Total** | | **682** | |

One `Read` call gives everything: method bodies, class structure, and exact text for
the `Edit` call.

---

### Comparison

```
With TokenSaver:    ████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   337 tokens
Without TokenSaver: █████████████████████████░░░░░░░░░░░░░░░░░░   682 tokens
```

| Metric | Value |
|---|---|
| Tokens saved | **~345** |
| Reduction | **~51%** |
| Result quality | Identical — one correct `Edit`, zero errors |

The key insight: skip `outline_c_sharp_file` when the task already names the target
method. And replace a full file `Read` for Edit prep with a targeted partial `Read` of
only the lines around the insertion point — 7 lines instead of 88.

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
| Read the file | `Read` | **860** | Tool header: `without tool: 860` |
| **Total** | | **860** | |

Only ~30 lines across two methods are relevant to the question. The remaining ~75 lines
enter context regardless.

---

### Comparison

```
With TokenSaver:    ████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   288 tokens
Without TokenSaver: ████████████████████████████████░░░░░░░░░░░   860 tokens
```

| Metric | Value |
|---|---|
| Tokens saved | **572** |
| Reduction | **67%** |
| Result quality | Identical — same correct analysis |

Comprehension tasks have no unavoidable Edit-prep Read, so TokenSaver captures the
full saving. The smaller and more focused the question, the larger the proportional win.

---

## Case Study 4: Multiple edits at different locations in a large file

**Task:** Change the default value of the `depth` parameter from `0` to `1` in three
separate methods — `Emit`, `EmitMultiple`, and `EmitCallers` — in `FocusedEmitter.cs`.
This requires understanding all three methods and making edits at line 94, line 205,
and line 399 respectively.

This exercise was performed live. File: `Emitters/FocusedEmitter.cs` (11,714 raw tokens
per MCP tool header).

---

### Agentic flow — With TokenSaver

| Step | Tool | Token count | Source |
|---|---|---|---|
| Understand all three methods | `focus_multiple_methods` (minify) | **1,884** | Tool header: `with tool: 1,884` |
| Locate exact line numbers + get match text | `Grep` | **~152** | ~609 chars ÷ 4 (Grep has no token header) |
| **Total** | | **~2,036** | |

`Grep` returned the exact on-disk signature lines needed for all three `Edit` calls —
no `Read` required at all.

---

### Agentic flow — Without TokenSaver

| Step | Tool | Token count | Source |
|---|---|---|---|
| Read the file | `Read` | **11,714** | Tool header: `without tool: 11,714` |
| **Total** | | **11,714** | |

---

### Comparison

```
With TokenSaver:    ████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   2,036 tokens
Without TokenSaver: █████████████████████████████████████████████  11,714 tokens
```

| Metric | Value |
|---|---|
| Tokens saved | **~9,678** |
| Reduction | **~83%** |
| Result quality | Identical — three correct `Edit` calls, zero errors |

**Multi-edit tasks on large files show the largest savings.** `focus_multiple_methods`
reads all three method bodies in a single call, and `Grep` supplies the exact match
strings for all three `Edit` calls at near-zero cost — no partial `Read` needed.
The 83% reduction holds regardless of how many edits are needed, because `Grep` cost
grows slowly (one line per match) while a full `Read` is always the entire file.

---

## Case Study 5: Build a brand-new MCP tool end-to-end (implementation + tests)

**Task:** Design and add a brand-new tool to the TokenSaver MCP server itself —
`FocusRegion`, which returns the members declared inside a named `#region … #endregion`
block of a C# file — including the emitter logic, the MCP tool wrapper, and a suite of
tests. This is a far larger task than a single edit: it requires comprehending how
existing tools are wired (`FocusType`), how the emitter constructs a `FocusResult`
(`EmitType`), and how the test harness registers and runs cases.

This exercise was performed live on **version 1.13.6** (the latest release at the time).
The same end-to-end task was completed twice — once using TokenSaver to comprehend the
source, once using only the built-in file reader — and only the *comprehension* reads
were measured, since the implementation and test code written is identical either way.

Files that had to be understood to implement the tool:

| File | Size |
|---|---|
| `mcp/FocusedEmitterTools.cs` | 655 lines |
| `Emitters/FocusedEmitter.cs` | 980 lines |
| `Reporting/TokenReport.cs` | 116 lines |
| `tests/Program.cs` | 2,520 lines / 42,427 tokens |
| `tests/fixtures/RegionHeavy.cs` | 25 lines |

---

### Agentic flow — With TokenSaver

Eight targeted MCP calls — outlines for navigation, focused views for the exact methods
and types to mimic — instead of reading any whole file.

| Step | Tool | Tokens |
|---|---|---|
| Navigate tool registrations | `outline_c_sharp_file` (FocusedEmitterTools.cs) | **1,160** |
| Navigate emitter | `outline_c_sharp_file` (FocusedEmitter.cs) | **889** |
| Understand emission analog | `focus_method` `EmitType` (depth=1, minify) | **1,046** |
| Understand tool wiring | `focus_method` `FocusType` (depth=1, minify) | **1,534** |
| Read fixture | `minify_file` (RegionHeavy.cs) | **78** |
| Navigate test harness | `outline_c_sharp_file` (Program.cs) | **2,528** |
| Understand test patterns | `focus_multiple_methods` (6 test methods, depth=1) | **732** |
| Understand `TestOutcome` shape | `focus_type` (TestOutcome) | **46** |
| **Total** | | **~8,013 tokens** |

The 2,520-line / 42,427-token test file was navigated with one outline (2,528) plus a
single focused view of the six relevant test methods (732) — never read in full.

---

### Agentic flow — Without TokenSaver

| Step | Tool | Tokens |
|---|---|---|
| Read FocusedEmitterTools.cs | `Read` | **~11,855** |
| Read FocusedEmitter.cs | `Read` | **~14,903** |
| Read RegionHeavy.cs | `Read` | **~227** |
| Read Program.cs | `Read` (chunked — exceeds the 25K single-read cap) | **42,427** |
| **Total** | | **~69,412 tokens** |

The test file alone is 42,427 tokens and exceeds the built-in reader's 25,000-token
single-read limit, forcing it to be read in multiple chunks just to comprehend the
harness and find the insertion points.

---

### Comparison

```
With TokenSaver:    █████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   8,013 tokens
Without TokenSaver: █████████████████████████████████████████████  69,412 tokens
```

| Metric | Value |
|---|---|
| Tokens saved | **~61,399** |
| Reduction | **~88%** |
| Result quality | Identical — the same working tool, all 156 tests passing |

**Whole-feature tasks across many files compound the saving.** A new tool touches
several large files at once, and comprehension dominates the cost. TokenSaver brought
each file into context at the size the task actually needed — outlines for layout, one
focused view per relevant method — keeping the test file's 42K tokens out of context
entirely. The naive approach pays the full price of every file just to understand them.

---

## Test file

`Emitters/FocusedEmitter.cs` — 1,004 lines, part of the open-source
[TokenSaver](https://github.com/Byggarepop/TokenSaver) project.
The file implements the Roslyn-based focused emitter that powers the
TokenSaver MCP server itself.
