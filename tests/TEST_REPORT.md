# TokenSaver — Test Report

_Generated: 2026-05-14 17:53_

**84/84 scenarios passed.**

## Results

| Scenario | Result | Tokens before | Tokens after | Saved | Notes |
|---|---|---:|---:|---:|---|
| Minify_PreservesLogicAndSavesTokens | PASS | 441 | 222 | 49,7% | 5 methods preserved; tokens 441→222 (49,7% saved) |
| Minify_OutputReparsesCleanly | PASS | 441 | 222 | 49,7% | minified output parses with zero errors |
| Minify_StripsAllComments | PASS | 441 | 222 | 49,7% | no //, ///, or /* survived in body |
| Focus_IncludesFocusMethodBody | PASS | 441 | 264 | 40,1% | focus body present verbatim |
| Focus_DropsUnrelatedMembers | PASS | 168 | 66 | 60,7% | unrelated 'Classify' arms not present; focus body present |
| Focus_Depth0_HelpersAreSignaturesOnly | PASS | 441 | 264 | 40,1% | helper bodies absent; signatures present |
| Focus_Depth1_IncludesPrivateHelperBodies | PASS | 441 | 355 | 19,4% | WeightedSum/Sum/ApplyBias bodies all present at depth=1 |
| Focus_NotFound_ReturnsNotFoundResult | PASS | — | — | — | returned NotFound with diagnostic comment |
| Alias_RenamesPrivateOnly | PASS | 441 | 246 | 44,3% | public API preserved; private helpers in ledger |
| Alias_PreservesNameofArgument | PASS | 79 | 72 | 8,9% | nameof(_counter) intact; field renamed to F1 elsewhere |
| Alias_LedgerDisambiguatesDuplicateNames | PASS | 131 | 128 | 1,9% | ledger qualifies _state by container in all three classes |
| Alias_OutputReparsesCleanly | PASS | 441 | 246 | 44,3% | aliased output parses without errors |
| Generics_And_Records_Survive_Minify | PASS | 168 | 149 | 11,3% | records, generic constraints, and switch arms survive |
| TaskRealism_FocusOutputContainsAnswerableLogic | PASS | 441 | 355 | 19,4% | focus output contains the zero-branch, bias, and clamp — enough to answer the task |
| RealWorld_Minify_LargeSourceFile | PASS | 9261 | 5525 | 40,3% | FocusedEmitter.cs minified losslessly; 40,3% saved |
| RealWorld_Focus_LargeSourceFile | PASS | 9261 | 2736 | 70,5% | Focus on Emit with depth=1: 70,5% reduction with helpers preserved |
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
| Outline_EmitsSignaturesOnly_NoBodies | PASS | 441 | 99 | 77,5% | all signatures present; no bodies; 77,5% saved |
| Outline_IncludesAllTopLevelTypes | PASS | 131 | 94 | 27,9% | outer, nested Inner, and sibling Other all present |
| EmitMultiple_BothMethodsPresent | PASS | 441 | 306 | 30,6% | both Run and WeightedSum present in single multi-method output |
| EmitMultiple_SharedSignaturesDeduped | PASS | 441 | 306 | 30,6% | multi (1225 chars) < original (1766 chars) — focused view confirmed |
| EmitMultiple_PartialNotFound_ReportsWhichAreAbsent | PASS | 441 | 271 | 38,4% | partial match: Run found, DoesNotExist reported in NOT FOUND comment |
| Razor_MultipleCodeBlocks_BothBlocksMerged | PASS | 120 | 63 | 47,4% | members from both @code blocks visible in outline |
| Razor_Focus_FindsMethodInFirstCodeBlock | PASS | 120 | 64 | 47,0% | focus_method found ExecSql inside the first @code block |
| Razor_BracesInStrings_DoNotCorruptExtraction | PASS | 120 | 63 | 47,4% | } inside string literal did not truncate first @code block |
| C_Registry_DispatchesByExtension | PASS | — | — | — | .c and .h dispatched to CEmitter |
| C_Minify_StripsComments | PASS | 145 | 73 | 49,5% | all // and /* */ comment forms stripped |
| C_Minify_PreservesPreprocessorDirectives | PASS | 145 | 73 | 49,5% | #include and #define directives preserved |
| C_Minify_BracesInStringsDoNotCorrupt | PASS | 145 | 73 | 49,5% | } inside string literal did not corrupt output |
| Cpp_Registry_DispatchesByExtension | PASS | — | — | — | .cpp/.cc/.cxx/.hpp/.hh/.inl dispatched to CppEmitter |
| Cpp_Minify_StripsComments | PASS | 189 | 98 | 47,8% | all // and /* */ comment forms stripped |
| Cpp_Minify_PreservesPreprocessorDirectives | PASS | 189 | 98 | 47,8% | #include and #define directives preserved |
| Cpp_Minify_BracesInStringsDoNotCorrupt | PASS | 189 | 98 | 47,8% | } inside string literal did not corrupt output |
| LazyModel_OutlineDoesNotLoadModel | PASS | 441 | 99 | 77,5% | EmitOutline completed; IsModelLoaded=false — no compilation triggered |
| LazyModel_MinifyDoesNotLoadModel | PASS | 441 | 222 | 49,7% | EmitMinified completed; IsModelLoaded=false — no compilation triggered |
| LazyModel_FocusLoadsModel | PASS | 441 | 264 | 40,1% | IsModelLoaded: false before Emit, true after — lazy build confirmed |
| LazyModel_AliasLoadsModel | PASS | 441 | 246 | 44,3% | IsModelLoaded: false before EmitAliased, true after — lazy build confirmed |
| LazyModel_Focus_OutputUnchanged | PASS | 441 | 355 | 19,4% | lazy model: Emit output unchanged — focus body and depth=1 helper both present |
| LazyModel_Outline_OutputUnchanged | PASS | 441 | 99 | 77,5% | lazy model: EmitOutline output unchanged — signatures present, bodies absent |
| Focus_Constructor_FoundByClassName | PASS | 441 | 62 | 85,9% | constructor found by class name — no longer returns NOT FOUND |
| FocusMultiple_Constructor_IncludedWithMethods | PASS | 441 | 307 | 30,4% | constructor and method both present in multi-focus output |
| Region_Minify_StripsRegionDirectives | PASS | 186 | 76 | 59,0% | #region/#endregion stripped; logic intact; 59,0% saved |
| Region_Focus_StripsRegionDirectivesWhenMinified | PASS | 186 | 26 | 85,6% | #region/#endregion absent after MinifyText; focus body intact |
| Region_LogicPreservedAfterStrip | PASS | 186 | 76 | 59,0% | fields, constructor, public methods, and private helpers all survived region strip |
| PropertySignature_GetOnly_NoSetInSignature | PASS | 179 | 133 | 25,5% | get-only property shows { get; } — no spurious set; |
| PropertySignature_InitOnly_ShowsInit | PASS | 179 | 133 | 25,5% | init-only property shows { get; init; } |
| PropertySignature_ExpressionBodied_ShowsGetOnly | PASS | 179 | 133 | 25,5% | expression-bodied property shows { get; } |
| PropertySignature_ReadWrite_ShowsBothAccessors | PASS | 179 | 133 | 25,5% | read-write property still shows { get; set; } |
| PropertySignature_PrivateSetter_ShowsModifier | PASS | 179 | 133 | 25,5% | private-setter property shows { get; private set; } |
| FieldSignature_InitializerStripped | PASS | 178 | 106 | 40,3% | all field initializers stripped from signatures |
| FieldSignature_TypeAndNamePreserved | PASS | 178 | 106 | 40,3% | type and name preserved after initializer strip |
| FieldSignature_MultipleDeclaratorsHandled | PASS | 178 | 106 | 40,3% | multi-declarator field collapsed to "type name1, name2;" with no initializers |
| Outline_Indexer_AppearsInSignature | PASS | 296 | 114 | 61,3% | expression-bodied indexer appears in outline |
| Outline_Operator_AppearsInSignature | PASS | 296 | 114 | 61,3% | binary operator overload appears in outline |
| Outline_ConversionOperator_AppearsInSignature | PASS | 296 | 114 | 61,3% | implicit and explicit conversion operators both appear in outline |
| Outline_IndexerWithAccessorList_ShowsAccessors | PASS | 296 | 114 | 61,3% | indexer with explicit get+set shows { get; set; } |

## What each scenario proves

- **Minify_***: lossless minify — same method count, same logic, output reparses, comments stripped.
- **Focus_***: focus mode — the named method's body is verbatim; unrelated members are dropped; private helpers at depth=0 are signatures only and at depth=1 have full bodies.
- **Alias_***: alias mode — only private symbols renamed, public API intact, `nameof(...)` argument preserved, ledger disambiguates duplicate names across nested classes (the bug we fixed today).
- **TaskRealism_***: confirms a focused output still contains enough information for an AI reader to answer a concrete behavioural question about the method.
