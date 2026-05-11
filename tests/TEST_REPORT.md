# TokenSaver — Test Report

_Generated: 2026-05-11 22:25_

**47/47 scenarios passed.**

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
| RealWorld_Minify_LargeSourceFile | PASS | 7135 | 4258 | 40,3% | FocusedEmitter.cs minified losslessly; 40,3% saved |
| RealWorld_Focus_LargeSourceFile | PASS | 7135 | 2544 | 64,3% | Focus on Emit with depth=1: 64,3% reduction with helpers preserved |
| Js_Minify_StripsComments | PASS | 153 | 85 | 44,3% | all // and /* */ comment forms stripped |
| Js_Minify_PreservesStringContents | PASS | 153 | 85 | 44,3% | strings, escapes, and template literals preserved verbatim |
| Js_Minify_SavesTokens | PASS | 153 | 85 | 44,3% | sample.js minified; 44,3% saved |
| Js_Registry_DispatchesByExtension | PASS | — | — | — | .js, .mjs, .jsx all dispatched to JavaScriptEmitter |
| Registry_ReturnsNullForUnsupportedExtensions | PASS | — | — | — | registry returns null for .rs/.go/.txt |
| Cs_Registry_DispatchesByExtension | PASS | — | — | — | .cs and .razor.cs dispatched to CSharpEmitter (.razor → RazorEmitter) |
| Cs_Minify_DelegatesToRoslyn | PASS | 441 | 222 | 49,7% | CSharpEmitter output matches FocusedEmitter.EmitMinified byte-for-byte |
| Ts_Registry_DispatchesByExtension | PASS | — | — | — | .ts/.tsx/.mts/.cts dispatched to TypeScriptEmitter (not JS) |
| Ts_Minify_PreservesTypeAnnotations | PASS | 144 | 106 | 26,3% | type annotations, generics, and interface decls preserved |
| Ts_Minify_StripsComments | PASS | 144 | 106 | 26,3% | TS comments stripped, types intact |
| Py_Registry_DispatchesByExtension | PASS | — | — | — | .py and .pyi dispatched to PythonEmitter |
| Py_Minify_StripsHashComments | PASS | 113 | 90 | 19,9% | all '#' comment forms stripped |
| Py_Minify_PreservesIndentation | PASS | 113 | 90 | 19,9% | leading indentation preserved verbatim (class+method) |
| Py_Minify_PreservesStringsWithHash | PASS | 113 | 90 | 19,9% | '#' inside strings and triple-quoted docstrings preserved |
| Py_Minify_CollapsesBlankRuns | PASS | 113 | 90 | 19,9% | blank-line runs collapsed to single blank |
| Json_Registry_DispatchesByExtension | PASS | — | — | — | .json and .jsonc dispatched to JsonEmitter |
| Json_Minify_CollapsesWhitespacePreservesStrings | PASS | 63 | 43 | 31,1% | structural whitespace collapsed; string contents and escapes intact |
| Jsonc_Minify_StripsComments | PASS | 58 | 24 | 57,7% | JSONC // and /* */ comments stripped, keys intact |
| Yaml_Registry_DispatchesByExtension | PASS | — | — | — | .yaml and .yml dispatched to YamlEmitter |
| Yaml_Minify_StripsHashCommentsKeepsIndent | PASS | 62 | 49 | 20,9% | comments stripped; indentation preserved; '#' in strings intact |
| Yaml_Minify_CollapsesBlankRuns | PASS | 62 | 49 | 20,9% | no 3+ consecutive newlines |
| Xml_Registry_DispatchesByExtension | PASS | — | — | — | .xml/.csproj/.props/.targets/.config dispatched to XmlEmitter |
| Xml_Minify_StripsCommentsKeepsElements | PASS | 55 | 43 | 22,9% | <!-- --> comments stripped; elements intact; blank runs collapsed |
| Html_Registry_DispatchesByExtension | PASS | — | — | — | .html and .htm dispatched to HtmlEmitter |
| Html_Minify_StripsCommentsCollapsesAttrs | PASS | 75 | 51 | 32,5% | <!-- --> comments stripped; attribute spacing collapsed; elements intact |
| Css_Registry_DispatchesByExtension | PASS | — | — | — | .css/.scss/.less dispatched to CssEmitter |
| Css_Minify_StripsCommentsPreservesStrings | PASS | 91 | 58 | 36,2% | comments stripped; strings and url() intact |
| Razor_Registry_DispatchesByExtension | PASS | — | — | — | .razor → RazorEmitter; .razor.cs → CSharpEmitter |
| Razor_Minify_CombinesMarkupAndCode | PASS | 98 | 97 | 0,8% | Razor output contains BOTH minified markup AND minified C# @code |
| Outline_EmitsSignaturesOnly_NoBodies | PASS | 441 | 97 | 78,0% | all signatures present; no bodies; 78,0% saved |
| Outline_IncludesAllTopLevelTypes | PASS | 131 | 94 | 27,9% | outer, nested Inner, and sibling Other all present |

## What each scenario proves

- **Minify_***: lossless minify — same method count, same logic, output reparses, comments stripped.
- **Focus_***: focus mode — the named method's body is verbatim; unrelated members are dropped; private helpers at depth=0 are signatures only and at depth=1 have full bodies.
- **Alias_***: alias mode — only private symbols renamed, public API intact, `nameof(...)` argument preserved, ledger disambiguates duplicate names across nested classes (the bug we fixed today).
- **TaskRealism_***: confirms a focused output still contains enough information for an AI reader to answer a concrete behavioural question about the method.
