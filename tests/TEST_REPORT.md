# TokenSaver — Test Report

_Generated: 2026-05-11 22:06_

**31/31 scenarios passed.**

## Results

| Scenario | Result | Tokens before | Tokens after | Saved | Notes |
|---|---|---:|---:|---:|---|
| Minify_PreservesLogicAndSavesTokens | PASS | 441 | 222 | 49,7% | 5 methods preserved; tokens 441→222 (49,7% saved) |
| Minify_OutputReparsesCleanly | PASS | 441 | 222 | 49,7% | minified output parses with zero errors |
| Minify_StripsAllComments | PASS | 441 | 222 | 49,7% | no //, ///, or /* survived in body |
| Focus_IncludesFocusMethodBody | PASS | 441 | 262 | 40,6% | focus body present verbatim |
| Focus_DropsUnrelatedMembers | PASS | 168 | 53 | 68,1% | unrelated 'Classify' arms not present; focus body present |
| Focus_Depth0_HelpersAreSignaturesOnly | PASS | 441 | 262 | 40,6% | helper bodies absent; signatures present |
| Focus_Depth1_IncludesPrivateHelperBodies | PASS | 441 | 353 | 19,9% | WeightedSum/Sum/ApplyBias bodies all present at depth=1 |
| Focus_NotFound_ReturnsNotFoundResult | PASS | — | — | — | returned NotFound with diagnostic comment |
| Alias_RenamesPrivateOnly | PASS | 441 | 246 | 44,3% | public API preserved; private helpers in ledger |
| Alias_PreservesNameofArgument | PASS | 79 | 72 | 8,9% | nameof(_counter) intact; field renamed to F1 elsewhere |
| Alias_LedgerDisambiguatesDuplicateNames | PASS | 131 | 128 | 1,9% | ledger qualifies _state by container in all three classes |
| Alias_OutputReparsesCleanly | PASS | 441 | 246 | 44,3% | aliased output parses without errors |
| Generics_And_Records_Survive_Minify | PASS | 168 | 149 | 11,3% | records, generic constraints, and switch arms survive |
| TaskRealism_FocusOutputContainsAnswerableLogic | PASS | 441 | 353 | 19,9% | focus output contains the zero-branch, bias, and clamp — enough to answer the task |
| RealWorld_Minify_LargeSourceFile | PASS | 6522 | 3865 | 40,7% | FocusedEmitter.cs minified losslessly; 40,7% saved |
| RealWorld_Focus_LargeSourceFile | PASS | 6522 | 2544 | 61,0% | Focus on Emit with depth=1: 61,0% reduction with helpers preserved |
| Js_Minify_StripsComments | PASS | 153 | 85 | 44,3% | all // and /* */ comment forms stripped |
| Js_Minify_PreservesStringContents | PASS | 153 | 85 | 44,3% | strings, escapes, and template literals preserved verbatim |
| Js_Minify_SavesTokens | PASS | 153 | 85 | 44,3% | sample.js minified; 44,3% saved |
| Js_Registry_DispatchesByExtension | PASS | — | — | — | .js, .mjs, .jsx all dispatched to JavaScriptEmitter |
| Registry_ReturnsNullForUnsupportedExtensions | PASS | — | — | — | registry returns null for .rs/.go/.txt |
| Cs_Registry_DispatchesByExtension | PASS | — | — | — | .cs/.razor/.razor.cs dispatched to CSharpEmitter |
| Cs_Minify_DelegatesToRoslyn | PASS | 441 | 222 | 49,7% | CSharpEmitter output matches FocusedEmitter.EmitMinified byte-for-byte |
| Ts_Registry_DispatchesByExtension | PASS | — | — | — | .ts/.tsx/.mts/.cts dispatched to TypeScriptEmitter (not JS) |
| Ts_Minify_PreservesTypeAnnotations | PASS | 144 | 106 | 26,3% | type annotations, generics, and interface decls preserved |
| Ts_Minify_StripsComments | PASS | 144 | 106 | 26,3% | TS comments stripped, types intact |
| Py_Registry_DispatchesByExtension | PASS | — | — | — | .py and .pyi dispatched to PythonEmitter |
| Py_Minify_StripsHashComments | PASS | 113 | 90 | 19,9% | all '#' comment forms stripped |
| Py_Minify_PreservesIndentation | PASS | 113 | 90 | 19,9% | leading indentation preserved verbatim (class+method) |
| Py_Minify_PreservesStringsWithHash | PASS | 113 | 90 | 19,9% | '#' inside strings and triple-quoted docstrings preserved |
| Py_Minify_CollapsesBlankRuns | PASS | 113 | 90 | 19,9% | blank-line runs collapsed to single blank |

## What each scenario proves

- **Minify_***: lossless minify — same method count, same logic, output reparses, comments stripped.
- **Focus_***: focus mode — the named method's body is verbatim; unrelated members are dropped; private helpers at depth=0 are signatures only and at depth=1 have full bodies.
- **Alias_***: alias mode — only private symbols renamed, public API intact, `nameof(...)` argument preserved, ledger disambiguates duplicate names across nested classes (the bug we fixed today).
- **TaskRealism_***: confirms a focused output still contains enough information for an AI reader to answer a concrete behavioural question about the method.
