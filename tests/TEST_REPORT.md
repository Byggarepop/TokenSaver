# TokenSaver — Test Report

_Generated: 2026-05-11 17:02_

**16/16 scenarios passed.**

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
| RealWorld_Minify_LargeSourceFile | PASS | 6493 | 3840 | 40,9% | FocusedEmitter.cs minified losslessly; 40,9% saved |
| RealWorld_Focus_LargeSourceFile | PASS | 6493 | 2544 | 60,8% | Focus on Emit with depth=1: 60,8% reduction with helpers preserved |

## What each scenario proves

- **Minify_***: lossless minify — same method count, same logic, output reparses, comments stripped.
- **Focus_***: focus mode — the named method's body is verbatim; unrelated members are dropped; private helpers at depth=0 are signatures only and at depth=1 have full bodies.
- **Alias_***: alias mode — only private symbols renamed, public API intact, `nameof(...)` argument preserved, ledger disambiguates duplicate names across nested classes (the bug we fixed today).
- **TaskRealism_***: confirms a focused output still contains enough information for an AI reader to answer a concrete behavioural question about the method.
