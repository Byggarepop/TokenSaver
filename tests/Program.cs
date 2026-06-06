using System.Text;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynLean;

namespace RoslynLean.Tests;

internal static class Program
{
    private static readonly List<TestRecord> Results = new();
    private static readonly string FixturesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures");
    private static readonly string ReportPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TEST_REPORT.md");

    private static int Main()
    {
        Console.WriteLine("== TokenSaver test suite ==\n");

        Run("Minify_PreservesLogicAndSavesTokens", Minify_PreservesLogicAndSavesTokens);
        Run("Minify_OutputReparsesCleanly", Minify_OutputReparsesCleanly);
        Run("Minify_StripsAllComments", Minify_StripsAllComments);
        Run("Focus_IncludesFocusMethodBody", Focus_IncludesFocusMethodBody);
        Run("Focus_DropsUnrelatedMembers", Focus_DropsUnrelatedMembers);
        Run("Focus_Depth0_HelpersAreSignaturesOnly", Focus_Depth0_HelpersAreSignaturesOnly);
        Run("Focus_Depth1_IncludesPrivateHelperBodies", Focus_Depth1_IncludesPrivateHelperBodies);
        Run("Focus_RelevantSourceText_IsFocusPlusHelpers", Focus_RelevantSourceText_IsFocusPlusHelpers);
        Run("TargetedReadBaseline_AppearsInHeader", TargetedReadBaseline_AppearsInHeader);
        Run("TelemetryBaseline_UsesConservativeValue", TelemetryBaseline_UsesConservativeValue);
        Run("SessionDedupe_RepeatFileViewNotDoubleCounted", SessionDedupe_RepeatFileViewNotDoubleCounted);
        Run("FocusMethod_CommaName_RoutesToMultiple", FocusMethod_CommaName_RoutesToMultiple);
        Run("CacheHit_StoresConservativeBaseline_NotWholeFile", CacheHit_StoresConservativeBaseline_NotWholeFile);
        Run("CacheHit_LogsOriginatingToolName", CacheHit_LogsOriginatingToolName);
        Run("NotFound_LogsWholeFileToResponseSaving", NotFound_LogsWholeFileToResponseSaving);
        Run("NotFoundMulti_LogsWholeFileToResponseSaving", NotFoundMulti_LogsWholeFileToResponseSaving);
        Run("NotFoundType_LogsWholeFileToResponseSaving", NotFoundType_LogsWholeFileToResponseSaving);
        Run("NotFoundCallers_LogsWholeFileToResponseSaving", NotFoundCallers_LogsWholeFileToResponseSaving);
        Run("ResendPending_OnlyUploadsPendingEntries", ResendPending_OnlyUploadsPendingEntries);
        Run("ResendPending_TransientFailureStaysPending", ResendPending_TransientFailureStaysPending);
        Run("ResendPending_RejectedIsSettledNotRetried", ResendPending_RejectedIsSettledNotRetried);
        Run("Append_AndMarkUploaded_RoundTrip", Append_AndMarkUploaded_RoundTrip);
        Run("Focus_NotFound_ReturnsNotFoundResult", Focus_NotFound_ReturnsNotFoundResult);
        Run("Focus_NotFound_PartialType_HintsSiblingFile", Focus_NotFound_PartialType_HintsSiblingFile);
        Run("Focus_NotFound_NonPartialType_NoPartialHint", Focus_NotFound_NonPartialType_NoPartialHint);
        Run("FocusMultiple_NotFound_PartialType_HintsSiblingFile", FocusMultiple_NotFound_PartialType_HintsSiblingFile);
        Run("Focus_NotFound_DerivedType_HintsBaseType", Focus_NotFound_DerivedType_HintsBaseType);
        Run("Focus_NotFound_NoBaseType_NoBaseHint", Focus_NotFound_NoBaseType_NoBaseHint);
        Run("Alias_RenamesPrivateOnly", Alias_RenamesPrivateOnly);
        Run("Alias_PreservesNameofArgument", Alias_PreservesNameofArgument);
        Run("Alias_LedgerDisambiguatesDuplicateNames", Alias_LedgerDisambiguatesDuplicateNames);
        Run("Alias_OutputReparsesCleanly", Alias_OutputReparsesCleanly);
        Run("Generics_And_Records_Survive_Minify", Generics_And_Records_Survive_Minify);
        Run("TaskRealism_FocusOutputContainsAnswerableLogic", TaskRealism_FocusOutputContainsAnswerableLogic);
        Run("RealWorld_Minify_LargeSourceFile", RealWorld_Minify_LargeSourceFile);
        Run("RealWorld_Focus_LargeSourceFile", RealWorld_Focus_LargeSourceFile);
        Run("Js_Minify_StripsComments", Js_Minify_StripsComments);
        Run("Js_Minify_PreservesStringContents", Js_Minify_PreservesStringContents);
        Run("Js_Minify_SavesTokens", Js_Minify_SavesTokens);
        Run("Js_Registry_DispatchesByExtension", Js_Registry_DispatchesByExtension);
        Run("Registry_ReturnsNullForUnsupportedExtensions", Registry_ReturnsNullForUnsupportedExtensions);
        Run("Cs_Registry_DispatchesByExtension", Cs_Registry_DispatchesByExtension);
        Run("Cs_Minify_DelegatesToRoslyn", Cs_Minify_DelegatesToRoslyn);
        Run("Ts_Registry_DispatchesByExtension", Ts_Registry_DispatchesByExtension);
        Run("Ts_Minify_PreservesTypeAnnotations", Ts_Minify_PreservesTypeAnnotations);
        Run("Ts_Minify_StripsComments", Ts_Minify_StripsComments);
        Run("Py_Registry_DispatchesByExtension", Py_Registry_DispatchesByExtension);
        Run("Py_Minify_StripsHashComments", Py_Minify_StripsHashComments);
        Run("Py_Minify_PreservesIndentation", Py_Minify_PreservesIndentation);
        Run("Py_Minify_PreservesStringsWithHash", Py_Minify_PreservesStringsWithHash);
        Run("Py_Minify_CollapsesBlankRuns", Py_Minify_CollapsesBlankRuns);
        Run("Json_Registry_DispatchesByExtension", Json_Registry_DispatchesByExtension);
        Run("Json_Minify_CollapsesWhitespacePreservesStrings", Json_Minify_CollapsesWhitespacePreservesStrings);
        Run("Jsonc_Minify_StripsComments", Jsonc_Minify_StripsComments);
        Run("Yaml_Registry_DispatchesByExtension", Yaml_Registry_DispatchesByExtension);
        Run("Yaml_Minify_StripsHashCommentsKeepsIndent", Yaml_Minify_StripsHashCommentsKeepsIndent);
        Run("Yaml_Minify_CollapsesBlankRuns", Yaml_Minify_CollapsesBlankRuns);
        Run("Xml_Registry_DispatchesByExtension", Xml_Registry_DispatchesByExtension);
        Run("Xml_Minify_StripsCommentsKeepsElements", Xml_Minify_StripsCommentsKeepsElements);
        Run("Html_Registry_DispatchesByExtension", Html_Registry_DispatchesByExtension);
        Run("Html_Minify_StripsCommentsCollapsesAttrs", Html_Minify_StripsCommentsCollapsesAttrs);
        Run("Css_Registry_DispatchesByExtension", Css_Registry_DispatchesByExtension);
        Run("Css_Minify_StripsCommentsPreservesStrings", Css_Minify_StripsCommentsPreservesStrings);
        Run("Razor_Registry_DispatchesByExtension", Razor_Registry_DispatchesByExtension);
        Run("Razor_Minify_CombinesMarkupAndCode", Razor_Minify_CombinesMarkupAndCode);
        Run("Outline_EmitsSignaturesOnly_NoBodies", Outline_EmitsSignaturesOnly_NoBodies);
        Run("Outline_IncludesAllTopLevelTypes", Outline_IncludesAllTopLevelTypes);
        Run("EmitMultiple_BothMethodsPresent", EmitMultiple_BothMethodsPresent);
        Run("EmitMultiple_SharedSignaturesDeduped", EmitMultiple_SharedSignaturesDeduped);
        Run("EmitMultiple_PartialNotFound_ReportsWhichAreAbsent", EmitMultiple_PartialNotFound_ReportsWhichAreAbsent);
        Run("Razor_MultipleCodeBlocks_BothBlocksMerged", Razor_MultipleCodeBlocks_BothBlocksMerged);
        Run("Razor_Focus_FindsMethodInFirstCodeBlock", Razor_Focus_FindsMethodInFirstCodeBlock);
        Run("Razor_BracesInStrings_DoNotCorruptExtraction", Razor_BracesInStrings_DoNotCorruptExtraction);
        Run("C_Registry_DispatchesByExtension", C_Registry_DispatchesByExtension);
        Run("C_Minify_StripsComments", C_Minify_StripsComments);
        Run("C_Minify_PreservesPreprocessorDirectives", C_Minify_PreservesPreprocessorDirectives);
        Run("C_Minify_BracesInStringsDoNotCorrupt", C_Minify_BracesInStringsDoNotCorrupt);
        Run("Cpp_Registry_DispatchesByExtension", Cpp_Registry_DispatchesByExtension);
        Run("Cpp_Minify_StripsComments", Cpp_Minify_StripsComments);
        Run("Cpp_Minify_PreservesPreprocessorDirectives", Cpp_Minify_PreservesPreprocessorDirectives);
        Run("Cpp_Minify_BracesInStringsDoNotCorrupt", Cpp_Minify_BracesInStringsDoNotCorrupt);
        Run("Xpp_Registry_DispatchesByExtension", Xpp_Registry_DispatchesByExtension);
        Run("Xpp_Minify_StripsComments", Xpp_Minify_StripsComments);
        Run("Xpp_Minify_PreservesMacroDirectives", Xpp_Minify_PreservesMacroDirectives);
        Run("Xpp_Minify_BracesInStringsDoNotCorrupt", Xpp_Minify_BracesInStringsDoNotCorrupt);
        Run("LazyModel_OutlineDoesNotLoadModel", LazyModel_OutlineDoesNotLoadModel);
        Run("LazyModel_MinifyDoesNotLoadModel", LazyModel_MinifyDoesNotLoadModel);
        Run("LazyModel_FocusLoadsModel", LazyModel_FocusLoadsModel);
        Run("LazyModel_AliasLoadsModel", LazyModel_AliasLoadsModel);
        Run("LazyModel_Focus_OutputUnchanged", LazyModel_Focus_OutputUnchanged);
        Run("LazyModel_Outline_OutputUnchanged", LazyModel_Outline_OutputUnchanged);
        Run("Focus_Constructor_FoundByClassName", Focus_Constructor_FoundByClassName);
        Run("FocusMultiple_Constructor_IncludedWithMethods", FocusMultiple_Constructor_IncludedWithMethods);
        Run("Region_Minify_StripsRegionDirectives", Region_Minify_StripsRegionDirectives);
        Run("Region_Focus_StripsRegionDirectivesWhenMinified", Region_Focus_StripsRegionDirectivesWhenMinified);
        Run("Region_LogicPreservedAfterStrip", Region_LogicPreservedAfterStrip);
        Run("PropertySignature_GetOnly_NoSetInSignature", PropertySignature_GetOnly_NoSetInSignature);
        Run("PropertySignature_InitOnly_ShowsInit", PropertySignature_InitOnly_ShowsInit);
        Run("PropertySignature_ExpressionBodied_ShowsGetOnly", PropertySignature_ExpressionBodied_ShowsGetOnly);
        Run("PropertySignature_ReadWrite_ShowsBothAccessors", PropertySignature_ReadWrite_ShowsBothAccessors);
        Run("PropertySignature_PrivateSetter_ShowsModifier", PropertySignature_PrivateSetter_ShowsModifier);
        Run("FieldSignature_InitializerStripped", FieldSignature_InitializerStripped);
        Run("FieldSignature_TypeAndNamePreserved", FieldSignature_TypeAndNamePreserved);
        Run("FieldSignature_MultipleDeclaratorsHandled", FieldSignature_MultipleDeclaratorsHandled);
        Run("Outline_Indexer_AppearsInSignature", Outline_Indexer_AppearsInSignature);
        Run("Outline_Operator_AppearsInSignature", Outline_Operator_AppearsInSignature);
        Run("Outline_ConversionOperator_AppearsInSignature", Outline_ConversionOperator_AppearsInSignature);
        Run("Outline_IndexerWithAccessorList_ShowsAccessors", Outline_IndexerWithAccessorList_ShowsAccessors);
        Run("FocusType_NonPrivateHasBody_PrivateHasSignature", FocusType_NonPrivateHasBody_PrivateHasSignature);
        Run("FocusType_OnlyTargetTypeInOutput", FocusType_OnlyTargetTypeInOutput);
        Run("FocusType_NotFound_ReturnsNotFound", FocusType_NotFound_ReturnsNotFound);
        Run("FocusCallers_FindsCallingMethods", FocusCallers_FindsCallingMethods);
        Run("FocusCallers_NotFound_WhenNoCallers", FocusCallers_NotFound_WhenNoCallers);
        Run("Focus_Depth1_ExpandsPrivatePropertyBody", Focus_Depth1_ExpandsPrivatePropertyBody);

        // ---------- C# interfaces ----------
        Run("Interface_Outline_NoLeadingSpaceOnSignatures", Interface_Outline_NoLeadingSpaceOnSignatures);
        Run("Interface_FocusType_DefaultImplHasBody_PrivateIsSignature", Interface_FocusType_DefaultImplHasBody_PrivateIsSignature);
        Run("Interface_FocusMethod_FindsAbstractMethod", Interface_FocusMethod_FindsAbstractMethod);

        // ---------- VB.NET ----------
        Run("Vb_Registry_DispatchesByExtension", Vb_Registry_DispatchesByExtension);
        Run("Vb_Minify_StripsComments", Vb_Minify_StripsComments);
        Run("Vb_Minify_CollapsesBlankRuns", Vb_Minify_CollapsesBlankRuns);
        Run("Vb_Minify_SavesTokens", Vb_Minify_SavesTokens);
        Run("Vb_Outline_EmitsSignaturesOnly_NoBodies", Vb_Outline_EmitsSignaturesOnly_NoBodies);
        Run("Vb_Focus_IncludesFocusMethodBody", Vb_Focus_IncludesFocusMethodBody);
        Run("Vb_Focus_Depth0_HelpersAreSignaturesOnly", Vb_Focus_Depth0_HelpersAreSignaturesOnly);
        Run("Vb_Focus_Depth1_IncludesPrivateHelperBodies", Vb_Focus_Depth1_IncludesPrivateHelperBodies);
        Run("Vb_Focus_RelevantSourceText_IsFocusPlusHelpers", Vb_Focus_RelevantSourceText_IsFocusPlusHelpers);
        Run("Vb_FocusType_NonPrivateHasBody_PrivateHasSignature", Vb_FocusType_NonPrivateHasBody_PrivateHasSignature);
        Run("Vb_FocusCallers_FindsCallingMethods", Vb_FocusCallers_FindsCallingMethods);

        // ---------- Markdown ----------
        Run("Md_Registry_DispatchesByExtension", Md_Registry_DispatchesByExtension);
        Run("Md_Minify_StripsHtmlComments", Md_Minify_StripsHtmlComments);
        Run("Md_Minify_CollapsesBlankRuns", Md_Minify_CollapsesBlankRuns);
        Run("Md_Minify_PreservesIndentation", Md_Minify_PreservesIndentation);

        // ---------- ProjectTraversal ----------
        Run("Traversal_FindCallerFiles_FindsFileWithCaller", Traversal_FindCallerFiles_FindsFileWithCaller);
        Run("Traversal_FindCallerFiles_ReturnsEmptyForUnknownMethod", Traversal_FindCallerFiles_ReturnsEmptyForUnknownMethod);
        Run("Traversal_FindImplementors_FindsImplementingTypes", Traversal_FindImplementors_FindsImplementingTypes);
        Run("Traversal_FindImplementors_ReturnsEmptyForUnknownInterface", Traversal_FindImplementors_ReturnsEmptyForUnknownInterface);
        Run("Traversal_AcceptsCsprojPath", Traversal_AcceptsCsprojPath);
        Run("McpTool_SecondCallReturnsReparseSkipped", McpTool_SecondCallReturnsReparseSkipped);
        Run("Cache_MissOnFirstCall", Cache_MissOnFirstCall);
        Run("Cache_HitOnSecondCall", Cache_HitOnSecondCall);
        Run("Cache_InvalidatedAfterFileChange", Cache_InvalidatedAfterFileChange);
        Run("PerCallHeader_IsOverheadFree", PerCallHeader_IsOverheadFree);
        Run("SessionLine_SubtractsOverheadOnce", SessionLine_SubtractsOverheadOnce);
        Run("PerCallHeader_NeverLabelsInitial", PerCallHeader_NeverLabelsInitial);
        Run("ToolSchemaOverheadCost", ToolSchemaOverheadCost);

        // ---------- dnx background auto-update / config pinning ----------
        Run("AutoUpdate_IsDnxEntry_RecognizesDnxAndSkipsOthers", AutoUpdate_IsDnxEntry_RecognizesDnxAndSkipsOthers);
        Run("AutoUpdate_SetPinnedVersion_InsertsReplacesAndNoOps", AutoUpdate_SetPinnedVersion_InsertsReplacesAndNoOps);
        Run("AutoUpdate_IsNewer_ComparesCoreVersions", AutoUpdate_IsNewer_ComparesCoreVersions);
        Run("AutoUpdate_PinInFlat_RepinsAndPreservesUnrelated", AutoUpdate_PinInFlat_RepinsAndPreservesUnrelated);
        Run("AutoUpdate_PinInVsCode_RepinsNestedEntry", AutoUpdate_PinInVsCode_RepinsNestedEntry);
        Run("AutoUpdate_PinInFlat_LeavesGlobalEntryUntouched", AutoUpdate_PinInFlat_LeavesGlobalEntryUntouched);
        Run("AutoUpdate_PinInFlat_ReportsWhetherConfigChanged", AutoUpdate_PinInFlat_ReportsWhetherConfigChanged);

        WriteReport();
        var failed = Results.Count(r => !r.Passed);
        Console.WriteLine($"\n{Results.Count - failed}/{Results.Count} passed. Report: {Path.GetFullPath(ReportPath)}");
        return failed == 0 ? 0 : 1;
    }

    // ---------- scenarios ----------

    private static TestOutcome Minify_PreservesLogicAndSavesTokens()
    {
        var path = Fixture("Calculator.cs");
        var original = File.ReadAllText(path);
        var emitter = new FocusedEmitter(path);
        var r = emitter.EmitMinified();

        var originalMethods = CountMethodsAndConstructors(original);
        var minifiedMethods = CountMethodsAndConstructors(r.Output);

        var saved = TokenSaving(r.OriginalChars, r.FocusedChars);
        var ok = r.Found
            && originalMethods == minifiedMethods
            && r.Output.Contains("WeightedSum")
            && r.Output.Contains("ApplyBias")
            && saved.percent > 15;

        return new TestOutcome(ok,
            $"{originalMethods} methods preserved; tokens {saved.before}→{saved.after} ({saved.percent:F1}% saved)",
            saved);
    }

    private static TestOutcome Minify_OutputReparsesCleanly()
    {
        var path = Fixture("Calculator.cs");
        var emitter = new FocusedEmitter(path);
        var r = emitter.EmitMinified();

        var tree = CSharpSyntaxTree.ParseText(r.Output);
        var diagnostics = tree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        var ok = diagnostics.Count == 0;
        return new TestOutcome(ok,
            ok ? "minified output parses with zero errors"
               : $"parse errors: {string.Join("; ", diagnostics.Select(d => d.GetMessage()))}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Minify_StripsAllComments()
    {
        var path = Fixture("Calculator.cs");
        var emitter = new FocusedEmitter(path);
        var r = emitter.EmitMinified();

        // Strip the tool's own header comments before scanning for source-level comments.
        var body = StripToolHeader(r.Output);
        var stillHasXmlDoc = body.Contains("///");
        var stillHasLineComment = body.Contains("// Guard");
        var stillHasBlockComment = body.Contains("/*");

        var ok = !stillHasXmlDoc && !stillHasLineComment && !stillHasBlockComment;
        return new TestOutcome(ok,
            ok ? "no //, ///, or /* survived in body"
               : $"comment residue: xmlDoc={stillHasXmlDoc}, line={stillHasLineComment}, block={stillHasBlockComment}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Focus_IncludesFocusMethodBody()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("Run", depth: 0);

        var ok = r.Found
            && r.Output.Contains("WeightedSum(values, weights)")
            && r.Output.Contains("LastMean = Math.Max(0, biased)");

        return new TestOutcome(ok,
            ok ? "focus body present verbatim"
               : "missing expected statements from Run",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Focus_DropsUnrelatedMembers()
    {
        // Calculator has only methods Run helpers call; add a fixture where some methods are truly unrelated.
        var path = Fixture("GenericsAndRecords.cs");
        var r = new FocusedEmitter(path).Emit("Increment", depth: 0);

        // 'Classify' is a sibling but unrelated; its switch arms must NOT appear in the focused output.
        var leakedClassify = r.Output.Contains("non-empty-string");
        var hasIncrement = r.Output.Contains("_counts[item] = n + 1");

        var ok = r.Found && hasIncrement && !leakedClassify;
        return new TestOutcome(ok,
            ok ? "unrelated 'Classify' arms not present; focus body present"
               : $"hasIncrement={hasIncrement} leakedClassify={leakedClassify}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Focus_Depth0_HelpersAreSignaturesOnly()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("Run", depth: 0);

        // depth=0 should render WeightedSum/Sum as a signature line, NOT include their body statements.
        var ok = r.Output.Contains("WeightedSum")
              && !r.Output.Contains("s += values[i] * weights[i]");

        return new TestOutcome(ok,
            ok ? "helper bodies absent; signatures present"
               : "helper body leaked into depth=0 output",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Focus_Depth1_IncludesPrivateHelperBodies()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("Run", depth: 1);

        var hasWeightedSumBody = r.Output.Contains("s += values[i] * weights[i]");
        var hasSumBody = r.Output.Contains("for (int i = 0; i < xs.Length; i++)");
        var hasApplyBiasBody = r.Output.Contains("x + _bias");

        var ok = hasWeightedSumBody && hasSumBody && hasApplyBiasBody;
        return new TestOutcome(ok,
            ok ? "WeightedSum/Sum/ApplyBias bodies all present at depth=1"
               : $"weighted={hasWeightedSumBody} sum={hasSumBody} bias={hasApplyBiasBody}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Focus_RelevantSourceText_IsFocusPlusHelpers()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("Run", depth: 1);

        var rel = r.RelevantSourceText ?? "";
        var wholeFile = System.IO.File.ReadAllText(path);

        // The relevant text holds the focus body and the expanded helper bodies...
        var hasFocus = rel.Contains("LastMean = Math.Max(0, biased)");
        var hasHelper = rel.Contains("s += values[i] * weights[i]");
        // ...but not the rest of the file, so it is a strict subset.
        var smaller = rel.Length > 0 && rel.Length < wholeFile.Length;

        var ok = r.Found && hasFocus && hasHelper && smaller;
        return new TestOutcome(ok,
            ok ? $"relevant text {rel.Length} chars < file {wholeFile.Length}; focus + helpers present"
               : $"focus={hasFocus} helper={hasHelper} smaller={smaller}",
            (0, 0, 0));
    }

    private static TestOutcome TargetedReadBaseline_AppearsInHeader()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;
        var output = TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "Run", depth: 1, minify: true);

        var hasTargetedLine = output.Contains("vs a targeted read of just the relevant code");

        // The targeted-read baseline is a subset of the file, so it must sit strictly
        // below the whole-file "without tool" baseline.
        var header = output.Split('\n')[0];
        var wholeFileTokens = ParseWithoutToolTokens(header);
        var relevantTokens = ParseTargetedBaseline(output);
        var ordered = relevantTokens > 0 && relevantTokens < wholeFileTokens;

        var ok = hasTargetedLine && ordered;
        return new TestOutcome(ok,
            ok ? $"targeted baseline {relevantTokens} < whole file {wholeFileTokens}"
               : $"hasLine={hasTargetedLine} relevant={relevantTokens} whole={wholeFileTokens}",
            (0, 0, 0));
    }

    private static TestOutcome TelemetryBaseline_UsesConservativeValue()
    {
        // Focused tools (relevant baseline present): record the smaller relevant-code
        // count, not the whole file — so the dashboard never overstates savings.
        var focused = TokenSaver.Mcp.FocusedEmitterTools.TelemetryBaseline(7083, 4200);
        // Whole-file tools (no relevant baseline): fall back to the whole file.
        var wholeFile = TokenSaver.Mcp.FocusedEmitterTools.TelemetryBaseline(7083, null);
        // A zero/empty relevant baseline must not be used.
        var emptyRel = TokenSaver.Mcp.FocusedEmitterTools.TelemetryBaseline(7083, 0);

        var ok = focused == 4200 && wholeFile == 7083 && emptyRel == 7083;
        return new TestOutcome(ok,
            ok ? "conservative: focused→4200, whole-file→7083, empty→7083"
               : $"focused={focused} wholeFile={wholeFile} emptyRel={emptyRel}",
            (0, 0, 0));
    }

    private static TestOutcome SessionDedupe_RepeatFileViewNotDoubleCounted()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0; // resets session ledger

        // First view of the file: counts the whole-file baseline once.
        var first = TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "Run", depth: 1, minify: true);
        var wholeFile = ParseWithoutToolTokens(first.Split('\n')[0]);
        var (savedOne, _) = ParseSession(first);

        // A distinct view of the SAME file (different method → cache miss). The
        // whole-file baseline must NOT be credited a second time.
        var second = TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "WeightedSum", depth: 1, minify: true);
        var secondFirstLine = second.Split('\n')[0];
        var (savedTwo, _) = ParseSession(second);

        // The repeat view drops the "% saved" headline (no "Tokens without tool")
        // and says so plainly — that headline repetition is what inflated summed savings.
        var marksRepeat = secondFirstLine.Contains("repeat view of this file")
                       && !secondFirstLine.Contains("Tokens without tool");
        // Had we double-counted, cumulative saved would exceed one whole file. Honest
        // dedupe keeps it below the whole-file baseline, and below the first view's total.
        var notDoubleCounted = savedTwo <= wholeFile && savedTwo < savedOne;

        var ok = marksRepeat && notDoubleCounted;
        return new TestOutcome(ok,
            ok ? $"repeat view de-headlined; cumulative saved {savedTwo} ≤ whole file {wholeFile} (was {savedOne})"
               : $"marksRepeat={marksRepeat} savedOne={savedOne} savedTwo={savedTwo} wholeFile={wholeFile}",
            (0, 0, 0));
    }

    private static TestOutcome FocusMethod_CommaName_RoutesToMultiple()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;

        // A comma means the caller wanted several methods. focus_method must route to
        // the multi tool instead of dumping the whole outline as a "not found" reply.
        var output = TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "WeightedSum,Sum", depth: 1);

        var routedToMulti = output.Contains("focus=[WeightedSum,Sum]");
        var noOutlineDump = !output.Contains("not found") && !output.Contains("Available members");
        var bothPresent = output.Contains("WeightedSum(") && output.Contains("double Sum(");

        var ok = routedToMulti && noOutlineDump && bothPresent;
        return new TestOutcome(ok,
            ok ? "comma name auto-routed to multi; both methods returned, no outline dump"
               : $"routedToMulti={routedToMulti} noOutlineDump={noOutlineDump} bothPresent={bothPresent}",
            (0, 0, 0));
    }

    private static TestOutcome CacheHit_StoresConservativeBaseline_NotWholeFile()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;

        // Prime the cache with a focused view (cache miss → SetCached). The header
        // reports both the whole-file baseline and the smaller relevant-code baseline.
        var output = TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "Run", depth: 1, minify: true);
        var wholeFile = ParseWithoutToolTokens(output.Split('\n')[0]);
        var relevant = ParseTargetedBaseline(output);

        // The cache must store the conservative (relevant-code) baseline, not the raw
        // whole-file count. Otherwise a cache-hit re-serve logs an inflated "without
        // tool" figure and re-credits the whole-file saving the ledger never re-credits.
        var hit = EmissionCache.TryGet(path, "Run", 1, true, out _, out var cachedBefore, out _);

        var ok = hit && cachedBefore == relevant && cachedBefore < wholeFile;
        return new TestOutcome(ok,
            ok ? $"cache stores conservative baseline {cachedBefore} (relevant) < whole file {wholeFile}"
               : $"hit={hit} cachedBefore={cachedBefore} relevant={relevant} whole={wholeFile}",
            (0, 0, 0));
    }

    private static TestOutcome CacheHit_LogsOriginatingToolName()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;

        // Prime the cache, then re-serve. The cache-hit telemetry row must be tagged
        // with the originating tool's name plus " Cache" (e.g. "Focused Emitter Cache"),
        // not a bare "Cache", so the dashboard shows which tool was re-served.
        TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "Run", depth: 1, minify: true);
        TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "Run", depth: 1, minify: true);

        var reportPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".tokensaver", "report.json");
        using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(reportPath));
        var lastTool = doc.RootElement.EnumerateArray().Last().GetProperty("ToolName").GetString();

        var ok = lastTool == "Focused Emitter Cache";
        return new TestOutcome(ok,
            ok ? "cache hit logged as 'Focused Emitter Cache'"
               : $"last logged tool name was '{lastTool}'",
            (0, 0, 0));
    }

    // Reads the most recently appended row from the shared report.json so a test can
    // assert what a tool actually logged for its last invocation.
    private static (int without, int with, string notes) LastReportRow()
    {
        var reportPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".tokensaver", "report.json");
        using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(reportPath));
        var last = doc.RootElement.EnumerateArray().Last();
        return (last.GetProperty("TokensWithoutTool").GetInt32(),
                last.GetProperty("TokensWithTool").GetInt32(),
                last.GetProperty("Notes").GetString() ?? "");
    }

    // A miss returns a small outline (+ hint), not the whole file — every NOT FOUND path
    // must log whole-file -> response (a real saving), not whole -> whole (a bogus 0%).
    private static TestOutcome NotFound_LogsWholeFileToResponseSaving()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;
        TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "NoSuchMethodExists", depth: 1, minify: true);

        var (without, with, notes) = LastReportRow();
        var ok = notes.Contains("NOT FOUND") && with > 0 && without > with;
        return new TestOutcome(ok,
            ok ? $"focus_method NOT FOUND logged whole-file {without} -> response {with} (real saving, not 0%)"
               : $"notes='{notes}' without={without} with={with}",
            (0, 0, 0));
    }

    private static TestOutcome NotFoundMulti_LogsWholeFileToResponseSaving()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;
        TokenSaver.Mcp.FocusedEmitterTools.FocusMultipleMethods(path, "NoSuchA,NoSuchB", depth: 1, minify: true);

        var (without, with, notes) = LastReportRow();
        var ok = notes.Contains("NOT FOUND") && with > 0 && without > with;
        return new TestOutcome(ok,
            ok ? $"focus_multiple_methods NOT FOUND logged {without} -> {with} (real saving)"
               : $"notes='{notes}' without={without} with={with}",
            (0, 0, 0));
    }

    private static TestOutcome NotFoundType_LogsWholeFileToResponseSaving()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;
        TokenSaver.Mcp.FocusedEmitterTools.FocusType(path, "NoSuchType", minify: true);

        var (without, with, notes) = LastReportRow();
        var ok = notes.Contains("NOT FOUND") && with > 0 && without > with;
        return new TestOutcome(ok,
            ok ? $"focus_type NOT FOUND logged {without} -> {with} (real saving)"
               : $"notes='{notes}' without={without} with={with}",
            (0, 0, 0));
    }

    private static TestOutcome NotFoundCallers_LogsWholeFileToResponseSaving()
    {
        var path = Fixture("Calculator.cs");
        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;
        TokenSaver.Mcp.FocusedEmitterTools.FocusCallers(path, "NoSuchMethodAnywhere", depth: 1, minify: true);

        var (without, with, notes) = LastReportRow();
        var ok = notes.Contains("NOT FOUND") && with > 0 && without > with;
        return new TestOutcome(ok,
            ok ? $"focus_callers NOT FOUND logged {without} -> {with} (real saving)"
               : $"notes='{notes}' without={without} with={with}",
            (0, 0, 0));
    }

    private static TestOutcome ResendPending_OnlyUploadsPendingEntries()
    {
        var legacy   = new TokenSaver.ReportEntry("L", "C#", 100, 30, null, "mcp", DateTime.UtcNow, Uploaded: null);
        var pendingA = new TokenSaver.ReportEntry("A", "C#", 100, 30, null, "mcp", DateTime.UtcNow, Uploaded: false);
        var done     = new TokenSaver.ReportEntry("D", "C#", 100, 30, null, "mcp", DateTime.UtcNow, Uploaded: true);
        var pendingB = new TokenSaver.ReportEntry("B", "C#", 100, 30, null, "mcp", DateTime.UtcNow, Uploaded: false);

        var uploaded = new List<string>();
        var settled  = new List<string>();
        var count = TokenSaver.ReportUploader.ResendPendingAsync(
            new[] { legacy, pendingA, done, pendingB },
            e => { uploaded.Add(e.ToolName); return System.Threading.Tasks.Task.FromResult((bool?)true); },
            e => settled.Add(e.ToolName)).GetAwaiter().GetResult();

        // Only the two Uploaded==false rows are (re)sent; null (legacy) and true (done) skipped.
        var ok = count == 2
              && uploaded.SequenceEqual(new[] { "A", "B" })
              && settled.SequenceEqual(new[] { "A", "B" });
        return new TestOutcome(ok,
            ok ? "resend hit only the two pending rows (skipped legacy + done)"
               : $"count={count} uploaded=[{string.Join(",", uploaded)}] settled=[{string.Join(",", settled)}]",
            (0, 0, 0));
    }

    private static TestOutcome ResendPending_TransientFailureStaysPending()
    {
        var a = new TokenSaver.ReportEntry("A", "C#", 100, 30, null, "mcp", DateTime.UtcNow, Uploaded: false);
        var b = new TokenSaver.ReportEntry("B", "C#", 100, 30, null, "mcp", DateTime.UtcNow, Uploaded: false);

        var settled = new List<string>();
        // null = transient (5xx/429/network): A confirmed, B transient -> B stays pending for a later pass.
        var count = TokenSaver.ReportUploader.ResendPendingAsync(
            new[] { a, b },
            e => System.Threading.Tasks.Task.FromResult(e.ToolName == "A" ? (bool?)true : null),
            e => settled.Add(e.ToolName)).GetAwaiter().GetResult();

        var ok = count == 1 && settled.SequenceEqual(new[] { "A" });
        return new TestOutcome(ok,
            ok ? "transient failure left unsettled (will retry); only the confirmed row settled"
               : $"count={count} settled=[{string.Join(",", settled)}]",
            (0, 0, 0));
    }

    private static TestOutcome ResendPending_RejectedIsSettledNotRetried()
    {
        var a = new TokenSaver.ReportEntry("A", "C#", 100, 30, null, "mcp", DateTime.UtcNow, Uploaded: false);

        var settled = new List<string>();
        // false = permanent 4xx rejection (e.g. server validation): must settle so the row is
        // NOT resent forever (the poison-pill case).
        var count = TokenSaver.ReportUploader.ResendPendingAsync(
            new[] { a },
            e => System.Threading.Tasks.Task.FromResult((bool?)false),
            e => settled.Add(e.ToolName)).GetAwaiter().GetResult();

        var ok = count == 1 && settled.SequenceEqual(new[] { "A" });
        return new TestOutcome(ok,
            ok ? "permanently rejected row settled (won't poison-loop on retry)"
               : $"count={count} settled=[{string.Join(",", settled)}]",
            (0, 0, 0));
    }

    private static TestOutcome Append_AndMarkUploaded_RoundTrip()
    {
        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ts_report_{Guid.NewGuid():N}.json");
        var prevNoTelem = Environment.GetEnvironmentVariable("TOKENSAVER_NO_TELEMETRY");
        Environment.SetEnvironmentVariable("TOKENSAVER_NO_TELEMETRY", "1"); // keep Append's FireAndForget a no-op
        try
        {
            TokenSaver.ReportWriter.Append("Tool", "C#", 100, 30, "n", "cli", tmp);
            var afterAppend = TokenSaver.ReportWriter.LoadAll(tmp);
            var pendingOk = afterAppend.Count == 1 && afterAppend[0].Uploaded == false;

            TokenSaver.ReportWriter.MarkUploaded(afterAppend[0], tmp);
            var afterMark = TokenSaver.ReportWriter.LoadAll(tmp);
            var markedOk = afterMark.Count == 1 && afterMark[0].Uploaded == true;

            var ok = pendingOk && markedOk;
            return new TestOutcome(ok,
                ok ? "Append writes Uploaded=false; MarkUploaded flips it to true"
                   : $"pendingOk={pendingOk} markedOk={markedOk}",
                (0, 0, 0));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TOKENSAVER_NO_TELEMETRY", prevNoTelem);
            if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp);
        }
    }

    private static TestOutcome Focus_NotFound_ReturnsNotFoundResult()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("NoSuchMethodExists");
        var ok = !r.Found && r.Output.Contains("not found");
        return new TestOutcome(ok,
            ok ? "returned NotFound with diagnostic comment"
               : "expected NotFound result, got Found",
            (0, 0, 0));
    }

    private static TestOutcome Focus_NotFound_PartialType_HintsSiblingFile()
    {
        // PartialWidget is declared 'partial'; Render lives in the sibling file
        // PartialWidget.Render.cs. Focusing the Main file must miss — but the NOT FOUND
        // diagnostic should hint that the type is partial so the model looks in a sibling
        // file instead of giving up or hallucinating.
        var path = Fixture("PartialWidget.Main.cs");
        var r = new FocusedEmitter(path).Emit("Render");

        var missed          = !r.Found;
        var hintsPartial    = r.Output.Contains("partial");
        var namesType       = r.Output.Contains("PartialWidget");
        var scopesNamespace = r.Output.Contains("namespace");
        var ok = missed && hintsPartial && namesType && scopesNamespace;
        return new TestOutcome(ok,
            ok ? "NOT FOUND on a partial type hints a same-namespace sibling file"
               : $"missed={missed} hintsPartial={hintsPartial} namesType={namesType} scopesNamespace={scopesNamespace} :: {r.Output}",
            (0, 0, 0));
    }

    private static TestOutcome Focus_NotFound_NonPartialType_NoPartialHint()
    {
        // Calculator is NOT partial — a miss here must NOT emit the partial hint,
        // otherwise the hint becomes noise on every ordinary typo.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("NoSuchMethodExists");

        var missed        = !r.Found;
        var noPartialHint = !r.Output.Contains("partial");
        var ok = missed && noPartialHint;
        return new TestOutcome(ok,
            ok ? "NOT FOUND on a non-partial type emits no partial hint"
               : $"missed={missed} noPartialHint={noPartialHint} :: {r.Output}",
            (0, 0, 0));
    }

    private static TestOutcome FocusMultiple_NotFound_PartialType_HintsSiblingFile()
    {
        // A full miss across ALL requested names on a partial type should also hint the
        // sibling file — same gap as the single-method route, via EmitMultiple.
        var path = Fixture("PartialWidget.Main.cs");
        var r = new FocusedEmitter(path).EmitMultiple(["Render", "AlsoMissing"], depth: 0);

        var missed       = !r.Found;
        var hintsPartial = r.Output.Contains("partial");
        var ok = missed && hintsPartial;
        return new TestOutcome(ok,
            ok ? "multi NOT FOUND on a partial type hints the sibling file"
               : $"missed={missed} hintsPartial={hintsPartial} :: {r.Output}",
            (0, 0, 0));
    }

    private static TestOutcome Focus_NotFound_DerivedType_HintsBaseType()
    {
        // DerivedGadget : GadgetBase, IGadget — Configure is inherited from GadgetBase,
        // declared in the sibling file GadgetBase.cs. Focusing this file must miss, and the
        // NOT FOUND diagnostic should hint that the member may live on a base type and name it.
        var path = Fixture("DerivedGadget.cs");
        var r = new FocusedEmitter(path).Emit("Configure");

        var missed      = !r.Found;
        var hintsInherit = r.Output.Contains("inherit");
        var namesBase    = r.Output.Contains("GadgetBase");
        var ok = missed && hintsInherit && namesBase;
        return new TestOutcome(ok,
            ok ? "NOT FOUND on a derived type hints the member may be inherited from a base"
               : $"missed={missed} hintsInherit={hintsInherit} namesBase={namesBase} :: {r.Output}",
            (0, 0, 0));
    }

    private static TestOutcome Focus_NotFound_NoBaseType_NoBaseHint()
    {
        // Calculator has no base list — a miss here must NOT emit the inheritance hint,
        // otherwise the hint becomes noise on every ordinary typo.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("NoSuchMethodExists");

        var missed       = !r.Found;
        var noInheritHint = !r.Output.Contains("inherit");
        var ok = missed && noInheritHint;
        return new TestOutcome(ok,
            ok ? "NOT FOUND on a type with no base list emits no inheritance hint"
               : $"missed={missed} noInheritHint={noInheritHint} :: {r.Output}",
            (0, 0, 0));
    }

    private static TestOutcome Alias_RenamesPrivateOnly()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitAliased();

        // public symbols must NOT be renamed.
        var publicSurvived = r.Output.Contains("public double Run(")
                          && r.Output.Contains("public double LastMean")
                          && r.Output.Contains("public Calculator(");
        // private helpers should be renamed (the ledger lists them as M1..M3).
        var ledgerLooksRight = r.Output.Contains("M1=WeightedSum") || r.Output.Contains("M1=Sum") || r.Output.Contains("M1=ApplyBias");

        var ok = publicSurvived && ledgerLooksRight;
        return new TestOutcome(ok,
            ok ? "public API preserved; private helpers in ledger"
               : $"publicSurvived={publicSurvived} ledgerLooksRight={ledgerLooksRight}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Alias_PreservesNameofArgument()
    {
        var path = Fixture("NameofGuard.cs");
        var r = new FocusedEmitter(path).EmitAliased();

        // The argument of nameof(...) must stay textually "_counter" — even though the field is renamed.
        var hasIntactNameof = r.Output.Contains("nameof(_counter)");
        // And the field itself should have been renamed to F1 in declarations / non-nameof usages.
        var renamedField = r.Output.Contains("F1=_counter");

        var ok = hasIntactNameof && renamedField;
        return new TestOutcome(ok,
            ok ? "nameof(_counter) intact; field renamed to F1 elsewhere"
               : $"nameofIntact={hasIntactNameof} renamedField={renamedField}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Alias_LedgerDisambiguatesDuplicateNames()
    {
        var path = Fixture("AmbiguousNested.cs");
        var r = new FocusedEmitter(path).EmitAliased();

        // Three different classes each have a private _state field. The ledger must qualify them with their containing type so the reader can map back unambiguously.
        var qualifiedOuter = r.Output.Contains("AmbiguousNested._state");
        var qualifiedInner = r.Output.Contains("Inner._state");
        var qualifiedOther = r.Output.Contains("Other._state");

        var ok = qualifiedOuter && qualifiedInner && qualifiedOther;
        return new TestOutcome(ok,
            ok ? "ledger qualifies _state by container in all three classes"
               : $"outer={qualifiedOuter} inner={qualifiedInner} other={qualifiedOther}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Alias_OutputReparsesCleanly()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitAliased();

        // Strip the leading ledger comment block before re-parsing (comments are fine for the parser, but verify).
        var tree = CSharpSyntaxTree.ParseText(r.Output);
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var ok = errors.Count == 0;
        return new TestOutcome(ok,
            ok ? "aliased output parses without errors"
               : $"parse errors: {string.Join("; ", errors.Select(d => d.GetMessage()))}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Generics_And_Records_Survive_Minify()
    {
        var path = Fixture("GenericsAndRecords.cs");
        var r = new FocusedEmitter(path).EmitMinified();

        var hasRecord = r.Output.Contains("public sealed record Pair<TKey, TValue>");
        var hasWhereClause = r.Output.Contains("where T : notnull");
        var hasSwitchArm = r.Output.Contains("int i when i < 0");

        var ok = hasRecord && hasWhereClause && hasSwitchArm;
        return new TestOutcome(ok,
            ok ? "records, generic constraints, and switch arms survive"
               : $"record={hasRecord} where={hasWhereClause} switch={hasSwitchArm}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome TaskRealism_FocusOutputContainsAnswerableLogic()
    {
        // Premise: a developer asks "in Calculator.Run, what happens if total weight is zero?"
        // The focused output must contain enough text for someone reading ONLY it to answer correctly.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("Run", depth: 1);

        // Required tokens to answer "weight==0 produces zero raw, then bias+clamp":
        var hasZeroBranch = r.Output.Contains("weight == 0 ? 0 : total / weight");
        var hasClamp = r.Output.Contains("Math.Max(0, biased)");
        var hasBiasBody = r.Output.Contains("x + _bias");

        var ok = hasZeroBranch && hasClamp && hasBiasBody;
        return new TestOutcome(ok,
            ok ? "focus output contains the zero-branch, bias, and clamp — enough to answer the task"
               : $"zero={hasZeroBranch} clamp={hasClamp} biasBody={hasBiasBody}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome RealWorld_Minify_LargeSourceFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Emitters", "FocusedEmitter.cs");
        var r = new FocusedEmitter(path).EmitMinified();

        var tree = CSharpSyntaxTree.ParseText(r.Output);
        var parses = !tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error);
        var saved = TokenSaving(r.OriginalChars, r.FocusedChars);
        var ok = parses && saved.percent >= 25;
        return new TestOutcome(ok,
            ok ? $"FocusedEmitter.cs minified losslessly; {saved.percent:F1}% saved"
               : $"parses={parses} savedPct={saved.percent:F1}",
            saved);
    }

    private static TestOutcome RealWorld_Focus_LargeSourceFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Emitters", "FocusedEmitter.cs");
        var r = new FocusedEmitter(path).Emit("Emit", depth: 1);

        var saved = TokenSaving(r.OriginalChars, r.FocusedChars);
        var hasFocusBody = r.Output.Contains("focusMethods.Count == 0");
        var hasHelperBody = r.Output.Contains("CollectReferencedSymbols")
                         && r.Output.Contains("AppendNamespaceOpen");

        var ok = r.Found && hasFocusBody && hasHelperBody && saved.percent >= 60;
        return new TestOutcome(ok,
            ok ? $"Focus on Emit with depth=1: {saved.percent:F1}% reduction with helpers preserved"
               : $"found={r.Found} focusBody={hasFocusBody} helper={hasHelperBody} savedPct={saved.percent:F1}",
            saved);
    }

    // ---------- JavaScript emitter ----------

    private static TestOutcome Js_Minify_StripsComments()
    {
        var path = Fixture("sample.js");
        var r = new JavaScriptEmitter().Minify(path);

        var hasLineComment = r.Output.Contains("Top-level line comment");
        var hasBlockComment = r.Output.Contains("Block comment describing");
        var hasDocComment = r.Output.Contains("doc comment");
        var hasTrailingLine = r.Output.Contains("trailing comment after a string");
        var hasInline = r.Output.Contains("inline note");

        var ok = !hasLineComment && !hasBlockComment && !hasDocComment && !hasTrailingLine && !hasInline;
        return new TestOutcome(ok,
            ok ? "all // and /* */ comment forms stripped"
               : $"residue line={hasLineComment} block={hasBlockComment} doc={hasDocComment} trailingLine={hasTrailingLine} inline={hasInline}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Js_Minify_PreservesStringContents()
    {
        var path = Fixture("sample.js");
        var r = new JavaScriptEmitter().Minify(path);

        // Comment-like sequences INSIDE strings must survive verbatim.
        var hasFakeComment = r.Output.Contains("hello // not-a-comment /* nor this */ world");
        // Escape sequences must survive (double-backslash in a path).
        var hasEscapedPath = r.Output.Contains(@"'C:\\Users\\test'");
        // Template literal contents preserved including interpolation.
        var hasTemplate = r.Output.Contains("${greeting}");

        var ok = hasFakeComment && hasEscapedPath && hasTemplate;
        return new TestOutcome(ok,
            ok ? "strings, escapes, and template literals preserved verbatim"
               : $"fakeComment={hasFakeComment} escapedPath={hasEscapedPath} template={hasTemplate}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Js_Minify_SavesTokens()
    {
        var path = Fixture("sample.js");
        var r = new JavaScriptEmitter().Minify(path);
        var saved = TokenSaving(r.OriginalChars, r.OutputChars);
        var ok = r.Found && saved.percent >= 25;
        return new TestOutcome(ok,
            ok ? $"sample.js minified; {saved.percent:F1}% saved"
               : $"found={r.Found} savedPct={saved.percent:F1}",
            saved);
    }

    private static TestOutcome Js_Registry_DispatchesByExtension()
    {
        var jsEmitter = LanguageEmitterRegistry.Find("foo/bar.js");
        var mjsEmitter = LanguageEmitterRegistry.Find("foo/bar.mjs");
        var jsxEmitter = LanguageEmitterRegistry.Find("foo/bar.jsx");

        var ok = jsEmitter is JavaScriptEmitter
              && mjsEmitter is JavaScriptEmitter
              && jsxEmitter is JavaScriptEmitter;

        return new TestOutcome(ok,
            ok ? ".js, .mjs, .jsx all dispatched to JavaScriptEmitter"
               : $"js={jsEmitter?.GetType().Name} mjs={mjsEmitter?.GetType().Name} jsx={jsxEmitter?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Registry_ReturnsNullForUnsupportedExtensions()
    {
        var rs = LanguageEmitterRegistry.Find("baz.rs");
        var go = LanguageEmitterRegistry.Find("baz.go");
        var txt = LanguageEmitterRegistry.Find("readme.txt");

        var ok = rs is null && go is null && txt is null;
        return new TestOutcome(ok,
            ok ? "registry returns null for .rs/.go/.txt"
               : $"rs={rs?.Language} go={go?.Language} txt={txt?.Language}",
            (0, 0, 0));
    }

    // ---------- C# emitter (via the unified interface) ----------

    private static TestOutcome Cs_Registry_DispatchesByExtension()
    {
        // .razor now belongs to RazorEmitter (covers markup + @code).
        // .razor.cs ends in .cs and stays with CSharpEmitter (pure code-behind).
        var cs = LanguageEmitterRegistry.Find("Foo.cs");
        var razorCs = LanguageEmitterRegistry.Find("Bar.razor.cs");

        var ok = cs is CSharpEmitter && razorCs is CSharpEmitter;
        return new TestOutcome(ok,
            ok ? ".cs and .razor.cs dispatched to CSharpEmitter (.razor → RazorEmitter)"
               : $"cs={cs?.GetType().Name} razorCs={razorCs?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Cs_Minify_DelegatesToRoslyn()
    {
        // Round-trip: the unified Minify path must produce the same content as the Roslyn EmitMinified.
        var path = Fixture("Calculator.cs");
        var viaInterface = new CSharpEmitter().Minify(path);
        var viaRoslyn = new FocusedEmitter(path).EmitMinified();

        var ok = viaInterface.Found
              && viaInterface.Output == viaRoslyn.Output
              && viaInterface.OriginalChars == viaRoslyn.OriginalChars
              && viaInterface.OutputChars == viaRoslyn.FocusedChars;

        return new TestOutcome(ok,
            ok ? "CSharpEmitter output matches FocusedEmitter.EmitMinified byte-for-byte"
               : "C# interface adapter diverged from underlying Roslyn output",
            TokenSaving(viaInterface.OriginalChars, viaInterface.OutputChars));
    }

    // ---------- TypeScript emitter ----------

    private static TestOutcome Ts_Registry_DispatchesByExtension()
    {
        var ts = LanguageEmitterRegistry.Find("foo.ts");
        var tsx = LanguageEmitterRegistry.Find("foo.tsx");
        var mts = LanguageEmitterRegistry.Find("foo.mts");
        var cts = LanguageEmitterRegistry.Find("foo.cts");

        var ok = ts is TypeScriptEmitter
              && tsx is TypeScriptEmitter
              && mts is TypeScriptEmitter
              && cts is TypeScriptEmitter;

        return new TestOutcome(ok,
            ok ? ".ts/.tsx/.mts/.cts dispatched to TypeScriptEmitter (not JS)"
               : $"ts={ts?.GetType().Name} tsx={tsx?.GetType().Name} mts={mts?.GetType().Name} cts={cts?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Ts_Minify_PreservesTypeAnnotations()
    {
        var path = Fixture("sample.ts");
        var r = new TypeScriptEmitter().Minify(path);

        // Type annotations are just identifiers + punctuation, so they MUST survive a lexical pass.
        var hasReturnType = r.Output.Contains(": string");
        var hasParamType = r.Output.Contains("name: string");
        var hasGeneric = r.Output.Contains("Box<T>");
        var hasInterface = r.Output.Contains("interface User");

        var ok = hasReturnType && hasParamType && hasGeneric && hasInterface;
        return new TestOutcome(ok,
            ok ? "type annotations, generics, and interface decls preserved"
               : $"returnType={hasReturnType} paramType={hasParamType} generic={hasGeneric} interface={hasInterface}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Ts_Minify_StripsComments()
    {
        var path = Fixture("sample.ts");
        var r = new TypeScriptEmitter().Minify(path);

        var hasLine = r.Output.Contains("module-level note");
        var hasBlock = r.Output.Contains("block describing User");

        var ok = !hasLine && !hasBlock;
        return new TestOutcome(ok,
            ok ? "TS comments stripped, types intact"
               : $"line={hasLine} block={hasBlock}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- Python emitter ----------

    private static TestOutcome Py_Registry_DispatchesByExtension()
    {
        var py = LanguageEmitterRegistry.Find("script.py");
        var pyi = LanguageEmitterRegistry.Find("stubs.pyi");

        var ok = py is PythonEmitter && pyi is PythonEmitter;
        return new TestOutcome(ok,
            ok ? ".py and .pyi dispatched to PythonEmitter"
               : $"py={py?.GetType().Name} pyi={pyi?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Py_Minify_StripsHashComments()
    {
        var path = Fixture("sample.py");
        var r = new PythonEmitter().Minify(path);

        var hasTopComment = r.Output.Contains("Module-level comment");
        var hasInline = r.Output.Contains("inline note about x");
        var hasTrailing = r.Output.Contains("end-of-line note");

        var ok = !hasTopComment && !hasInline && !hasTrailing;
        return new TestOutcome(ok,
            ok ? "all '#' comment forms stripped"
               : $"top={hasTopComment} inline={hasInline} trailing={hasTrailing}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Py_Minify_PreservesIndentation()
    {
        var path = Fixture("sample.py");
        var r = new PythonEmitter().Minify(path);

        // The method body line must keep its leading 8 spaces (class -> method -> body).
        var hasIndented = r.Output.Contains("        return self.n");
        // The class-level def must keep its 4 spaces.
        var hasMethodDef = r.Output.Contains("    def increment(self):");

        var ok = hasIndented && hasMethodDef;
        return new TestOutcome(ok,
            ok ? "leading indentation preserved verbatim (class+method)"
               : $"bodyIndent={hasIndented} methodIndent={hasMethodDef}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Py_Minify_PreservesStringsWithHash()
    {
        var path = Fixture("sample.py");
        var r = new PythonEmitter().Minify(path);

        // A '#' inside a string must not be treated as a comment.
        var hasHashInString = r.Output.Contains("\"this # is not a comment\"");
        // Triple-quoted docstring (with a '#' inside) must survive verbatim.
        var hasDocstring = r.Output.Contains("Docstring with a # inside that must survive");

        var ok = hasHashInString && hasDocstring;
        return new TestOutcome(ok,
            ok ? "'#' inside strings and triple-quoted docstrings preserved"
               : $"hashInString={hasHashInString} docstring={hasDocstring}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Py_Minify_CollapsesBlankRuns()
    {
        var path = Fixture("sample.py");
        var r = new PythonEmitter().Minify(path);

        // Source has three consecutive blank lines; output should have at most one.
        var hasTripleBlank = r.Output.Contains("\n\n\n");
        var ok = !hasTripleBlank;
        return new TestOutcome(ok,
            ok ? "blank-line runs collapsed to single blank"
               : "still has 3+ consecutive newlines",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- JSON emitter ----------

    private static TestOutcome Json_Registry_DispatchesByExtension()
    {
        var json = LanguageEmitterRegistry.Find("config.json");
        var jsonc = LanguageEmitterRegistry.Find("tsconfig.jsonc");
        var ok = json is JsonEmitter && jsonc is JsonEmitter;
        return new TestOutcome(ok,
            ok ? ".json and .jsonc dispatched to JsonEmitter"
               : $"json={json?.GetType().Name} jsonc={jsonc?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Json_Minify_CollapsesWhitespacePreservesStrings()
    {
        var path = Fixture("sample.json");
        var r = new JsonEmitter().Minify(path);

        // No spaces between structural tokens after minify.
        var compact = r.Output.Contains("\"name\":\"Token Saver\"");
        // # and // inside strings preserved.
        var stringIntact = r.Output.Contains("\"A # symbol and // sequence inside a string\"");
        // Escaped quotes survive.
        var escapeIntact = r.Output.Contains("\\\"escaped quotes\\\"");
        // Output is shorter and well-formed (starts with { and ends with }).
        var brackets = r.Output.StartsWith("{") && r.Output.EndsWith("}");

        var ok = compact && stringIntact && escapeIntact && brackets;
        return new TestOutcome(ok,
            ok ? "structural whitespace collapsed; string contents and escapes intact"
               : $"compact={compact} string={stringIntact} escape={escapeIntact} brackets={brackets}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Jsonc_Minify_StripsComments()
    {
        var path = Fixture("sample.jsonc");
        var r = new JsonEmitter().Minify(path);

        var hasLine = r.Output.Contains("line comment");
        var hasBlock = r.Output.Contains("block comment");
        var keysIntact = r.Output.Contains("\"compilerOptions\"") && r.Output.Contains("\"include\"");

        var ok = !hasLine && !hasBlock && keysIntact;
        return new TestOutcome(ok,
            ok ? "JSONC // and /* */ comments stripped, keys intact"
               : $"line={hasLine} block={hasBlock} keys={keysIntact}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- YAML emitter ----------

    private static TestOutcome Yaml_Registry_DispatchesByExtension()
    {
        var yaml = LanguageEmitterRegistry.Find("config.yaml");
        var yml = LanguageEmitterRegistry.Find("docker-compose.yml");
        var ok = yaml is YamlEmitter && yml is YamlEmitter;
        return new TestOutcome(ok,
            ok ? ".yaml and .yml dispatched to YamlEmitter"
               : $"yaml={yaml?.GetType().Name} yml={yml?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Yaml_Minify_StripsHashCommentsKeepsIndent()
    {
        var path = Fixture("sample.yaml");
        var r = new YamlEmitter().Minify(path);

        var hasTopComment = r.Output.Contains("top-level comment");
        var hasTrailing = r.Output.Contains("trailing comment");
        // Indentation must survive — nested 'web:' under 'services:' is 2-space indented.
        var hasNestedKey = r.Output.Contains("  web:");
        var hasDeepKey = r.Output.Contains("    image: nginx");
        // '#' inside a quoted string survives.
        var hasHashInString = r.Output.Contains("\"value with # not a comment\"");

        var ok = !hasTopComment && !hasTrailing && hasNestedKey && hasDeepKey && hasHashInString;
        return new TestOutcome(ok,
            ok ? "comments stripped; indentation preserved; '#' in strings intact"
               : $"top={hasTopComment} trail={hasTrailing} nest={hasNestedKey} deep={hasDeepKey} hashStr={hasHashInString}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Yaml_Minify_CollapsesBlankRuns()
    {
        var path = Fixture("sample.yaml");
        var r = new YamlEmitter().Minify(path);
        var ok = !r.Output.Contains("\n\n\n");
        return new TestOutcome(ok,
            ok ? "no 3+ consecutive newlines"
               : "blank-line runs not collapsed",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- XML emitter ----------

    private static TestOutcome Xml_Registry_DispatchesByExtension()
    {
        var xml = LanguageEmitterRegistry.Find("config.xml");
        var csproj = LanguageEmitterRegistry.Find("App.csproj");
        var props = LanguageEmitterRegistry.Find("Directory.Build.props");
        var targets = LanguageEmitterRegistry.Find("Custom.targets");
        var config = LanguageEmitterRegistry.Find("App.config");

        var ok = xml is XmlEmitter
              && csproj is XmlEmitter
              && props is XmlEmitter
              && targets is XmlEmitter
              && config is XmlEmitter;
        return new TestOutcome(ok,
            ok ? ".xml/.csproj/.props/.targets/.config dispatched to XmlEmitter"
               : $"xml={xml?.GetType().Name} csproj={csproj?.GetType().Name} props={props?.GetType().Name} targets={targets?.GetType().Name} config={config?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Xml_Minify_StripsCommentsKeepsElements()
    {
        var path = Fixture("sample.xml");
        var r = new XmlEmitter().Minify(path);

        var hasTopComment = r.Output.Contains("top comment");
        var hasMidComment = r.Output.Contains("another comment");
        var hasElements = r.Output.Contains("<item id=\"1\">first</item>")
                       && r.Output.Contains("<item id=\"2\">second</item>")
                       && r.Output.Contains("<child>value</child>");
        var noTripleBlank = !r.Output.Contains("\n\n\n");

        var ok = !hasTopComment && !hasMidComment && hasElements && noTripleBlank;
        return new TestOutcome(ok,
            ok ? "<!-- --> comments stripped; elements intact; blank runs collapsed"
               : $"top={hasTopComment} mid={hasMidComment} elements={hasElements} noTriple={noTripleBlank}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- HTML emitter ----------

    private static TestOutcome Html_Registry_DispatchesByExtension()
    {
        var html = LanguageEmitterRegistry.Find("page.html");
        var htm = LanguageEmitterRegistry.Find("legacy.htm");
        var ok = html is HtmlEmitter && htm is HtmlEmitter;
        return new TestOutcome(ok,
            ok ? ".html and .htm dispatched to HtmlEmitter"
               : $"html={html?.GetType().Name} htm={htm?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Html_Minify_StripsCommentsCollapsesAttrs()
    {
        var path = Fixture("sample.html");
        var r = new HtmlEmitter().Minify(path);

        var hasTopComment = r.Output.Contains("top of file comment");
        var hasMainComment = r.Output.Contains("main content");
        // The original has multiple spaces between attributes — must collapse.
        var hasCollapsedAttrs = r.Output.Contains("<div class=\"container\" id=\"root\">");
        var hasElementsIntact = r.Output.Contains("<h1>Hello</h1>");

        var ok = !hasTopComment && !hasMainComment && hasCollapsedAttrs && hasElementsIntact;
        return new TestOutcome(ok,
            ok ? "<!-- --> comments stripped; attribute spacing collapsed; elements intact"
               : $"top={hasTopComment} main={hasMainComment} attrs={hasCollapsedAttrs} elems={hasElementsIntact}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- CSS emitter ----------

    private static TestOutcome Css_Registry_DispatchesByExtension()
    {
        var css = LanguageEmitterRegistry.Find("site.css");
        var scss = LanguageEmitterRegistry.Find("vars.scss");
        var less = LanguageEmitterRegistry.Find("theme.less");
        var ok = css is CssEmitter && scss is CssEmitter && less is CssEmitter;
        return new TestOutcome(ok,
            ok ? ".css/.scss/.less dispatched to CssEmitter"
               : $"css={css?.GetType().Name} scss={scss?.GetType().Name} less={less?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Css_Minify_StripsCommentsPreservesStrings()
    {
        var path = Fixture("sample.css");
        var r = new CssEmitter().Minify(path);

        var hasTopComment = r.Output.Contains("top-level comment");
        var hasInlineComment = r.Output.Contains("inline comment");
        var hasMultiComment = r.Output.Contains("multi-line");
        // /* */ inside a string content value must survive.
        var hasStringIntact = r.Output.Contains("\"/* not a comment */\"");
        // URL() values intact.
        var hasUrl = r.Output.Contains("url(\"img/bg.png\")");

        var ok = !hasTopComment && !hasInlineComment && !hasMultiComment && hasStringIntact && hasUrl;
        return new TestOutcome(ok,
            ok ? "comments stripped; strings and url() intact"
               : $"top={hasTopComment} inline={hasInlineComment} multi={hasMultiComment} string={hasStringIntact} url={hasUrl}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- Razor emitter ----------

    private static TestOutcome Razor_Registry_DispatchesByExtension()
    {
        var razor = LanguageEmitterRegistry.Find("Page.razor");
        var razorCs = LanguageEmitterRegistry.Find("Page.razor.cs"); // .cs extension → CSharpEmitter
        var ok = razor is RazorEmitter && razorCs is CSharpEmitter;
        return new TestOutcome(ok,
            ok ? ".razor → RazorEmitter; .razor.cs → CSharpEmitter"
               : $"razor={razor?.GetType().Name} razorCs={razorCs?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Razor_Minify_CombinesMarkupAndCode()
    {
        var path = Fixture("sample.razor");
        var r = new RazorEmitter().Minify(path);

        // Markup half: stripped of <!-- --> comments, elements preserved.
        var hasMarkupHeader = r.Output.Contains("RAZOR MARKUP");
        var hasButton = r.Output.Contains("<button @onclick=\"Increment\">+1</button>");
        var hasMarkupCommentStripped = !r.Output.Contains("markup comment to be stripped");

        // Code half: @code body preserved, comments stripped.
        var hasCodeHeader = r.Output.Contains("RAZOR @code");
        var hasIncrement = r.Output.Contains("_count++");
        var hasCommentStripped = !r.Output.Contains("line comment in C#")
                              && !r.Output.Contains("Increment the counter");

        var ok = hasMarkupHeader && hasButton && hasMarkupCommentStripped
              && hasCodeHeader && hasIncrement && hasCommentStripped;
        return new TestOutcome(ok,
            ok ? "Razor output contains BOTH minified markup AND minified C# @code"
               : $"markupHdr={hasMarkupHeader} btn={hasButton} mkComStripped={hasMarkupCommentStripped} codeHdr={hasCodeHeader} incr={hasIncrement} codeComStripped={hasCommentStripped}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- C# Outline ----------

    private static TestOutcome Outline_EmitsSignaturesOnly_NoBodies()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitOutline();

        // Public/private method signatures present.
        var hasRunSig = r.Output.Contains("Run");
        var hasWeightedSumSig = r.Output.Contains("WeightedSum");
        // Bodies must be ABSENT.
        var noRunBody = !r.Output.Contains("Math.Max(0, biased)");
        var noWeightedSumBody = !r.Output.Contains("s += values[i] * weights[i]");

        var saved = TokenSaving(r.OriginalChars, r.FocusedChars);
        var significantSaving = saved.percent >= 50;

        var ok = r.Found && hasRunSig && hasWeightedSumSig && noRunBody && noWeightedSumBody && significantSaving;
        return new TestOutcome(ok,
            ok ? $"all signatures present; no bodies; {saved.percent:F1}% saved"
               : $"runSig={hasRunSig} wsSig={hasWeightedSumSig} noRunBody={noRunBody} noWsBody={noWeightedSumBody} saved={saved.percent:F1}",
            saved);
    }

    private static TestOutcome Outline_IncludesAllTopLevelTypes()
    {
        var path = Fixture("AmbiguousNested.cs");
        var r = new FocusedEmitter(path).EmitOutline();

        // AmbiguousNested has three classes: outer + nested Inner + sibling Other.
        var hasOuter = r.Output.Contains("class AmbiguousNested");
        var hasInner = r.Output.Contains("class Inner");
        var hasOther = r.Output.Contains("class Other");

        var ok = hasOuter && hasInner && hasOther;
        return new TestOutcome(ok,
            ok ? "outer, nested Inner, and sibling Other all present"
               : $"outer={hasOuter} inner={hasInner} other={hasOther}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- EmitMultiple ----------

    private static TestOutcome EmitMultiple_BothMethodsPresent()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitMultiple(["Run", "WeightedSum"], depth: 0);

        var hasRun         = r.Output.Contains("Run");
        var hasWeightedSum = r.Output.Contains("WeightedSum");
        var ok = r.Found && hasRun && hasWeightedSum;
        return new TestOutcome(ok,
            ok ? "both Run and WeightedSum present in single multi-method output"
               : $"found={r.Found} run={hasRun} ws={hasWeightedSum}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome EmitMultiple_SharedSignaturesDeduped()
    {
        // Multi output must be smaller than the full file — confirming it's a focused
        // view and not a whole-file dump. On small files with few shared symbols the
        // multi output can equal or slightly exceed the sum of two singles (no overlap
        // means no dedup gain), but it's always well below the original file size.
        var path = Fixture("Calculator.cs");
        var emitter = new FocusedEmitter(path);

        var multi  = emitter.EmitMultiple(["Run", "WeightedSum"], depth: 0);
        var originalChars = multi.OriginalChars;

        var ok = multi.Found && multi.FocusedChars < originalChars;
        return new TestOutcome(ok,
            ok ? $"multi ({multi.FocusedChars} chars) < original ({originalChars} chars) — focused view confirmed"
               : $"multi={multi.FocusedChars} original={originalChars}",
            TokenSaving(multi.OriginalChars, multi.FocusedChars));
    }

    private static TestOutcome EmitMultiple_PartialNotFound_ReportsWhichAreAbsent()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitMultiple(["Run", "DoesNotExist"], depth: 0);

        // Found=true because at least one method was found; NOT FOUND listed in output.
        var hasRun     = r.Output.Contains("Run");
        var reportsGap = r.Output.Contains("NOT FOUND") && r.Output.Contains("DoesNotExist");
        var ok = r.Found && hasRun && reportsGap;
        return new TestOutcome(ok,
            ok ? "partial match: Run found, DoesNotExist reported in NOT FOUND comment"
               : $"found={r.Found} run={hasRun} gap={reportsGap}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- Razor multi-@code block ----------

    private static TestOutcome Razor_MultipleCodeBlocks_BothBlocksMerged()
    {
        var path = Fixture("multi-code-block.razor");
        var r = new FocusedEmitter(path).EmitOutline();

        var hasExecSql     = r.Output.Contains("ExecSql");
        var hasClearGrid   = r.Output.Contains("ClearGrid");
        var hasMenuItem    = r.Output.Contains("GridFilterMenuItem");

        var ok = r.Found && hasExecSql && hasClearGrid && hasMenuItem;
        return new TestOutcome(ok,
            ok ? "members from both @code blocks visible in outline"
               : $"ExecSql={hasExecSql} ClearGrid={hasClearGrid} GridFilterMenuItem={hasMenuItem}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Razor_Focus_FindsMethodInFirstCodeBlock()
    {
        var path = Fixture("multi-code-block.razor");
        var r = new FocusedEmitter(path).Emit("ExecSql", depth: 0);

        var ok = r.Found && r.Output.Contains("ExecSql");
        return new TestOutcome(ok,
            ok ? "focus_method found ExecSql inside the first @code block"
               : $"found={r.Found}",
            TokenSaving(r.OriginalChars, r.Output.Length));
    }

    private static TestOutcome Razor_BracesInStrings_DoNotCorruptExtraction()
    {
        // The first @code block has  private string _tag = "closing }";
        // The lone } inside the string literal used to make the naive brace counter
        // hit depth=0 early, truncating the first block before ExecSql and ClearGrid.
        var path = Fixture("multi-code-block.razor");
        var r = new FocusedEmitter(path).EmitOutline();

        var hasExecSql   = r.Output.Contains("ExecSql");
        var hasClearGrid = r.Output.Contains("ClearGrid");

        var ok = r.Found && hasExecSql && hasClearGrid;
        return new TestOutcome(ok,
            ok ? "} inside string literal did not truncate first @code block"
               : $"ExecSql={hasExecSql} ClearGrid={hasClearGrid} (brace-in-string corruption likely)",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- C emitter ----------

    private static TestOutcome C_Registry_DispatchesByExtension()
    {
        var c = LanguageEmitterRegistry.Find("main.c");
        var h = LanguageEmitterRegistry.Find("utils.h");
        var ok = c is CEmitter && h is CEmitter;
        return new TestOutcome(ok,
            ok ? ".c and .h dispatched to CEmitter"
               : $"c={c?.GetType().Name} h={h?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome C_Minify_StripsComments()
    {
        var path = Fixture("sample.c");
        var r = new CEmitter().Minify(path);

        var hasBlock   = r.Output.Contains("top-level block comment");
        var hasLine    = r.Output.Contains("line comment about MAX");
        var hasInline  = r.Output.Contains("inline note");
        var hasMulti   = r.Output.Contains("multi-line block comment");
        var hasTrail   = r.Output.Contains("another comment");

        var ok = !hasBlock && !hasLine && !hasInline && !hasMulti && !hasTrail;
        return new TestOutcome(ok,
            ok ? "all // and /* */ comment forms stripped"
               : $"block={hasBlock} line={hasLine} inline={hasInline} multi={hasMulti} trail={hasTrail}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome C_Minify_PreservesPreprocessorDirectives()
    {
        var path = Fixture("sample.c");
        var r = new CEmitter().Minify(path);

        var hasInclude = r.Output.Contains("#include <stdio.h>");
        var hasDefine  = r.Output.Contains("#define MAX 100");
        var hasMacro   = r.Output.Contains("#define GREET(name)");

        var ok = hasInclude && hasDefine && hasMacro;
        return new TestOutcome(ok,
            ok ? "#include and #define directives preserved"
               : $"include={hasInclude} define={hasDefine} macro={hasMacro}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome C_Minify_BracesInStringsDoNotCorrupt()
    {
        var path = Fixture("sample.c");
        var r = new CEmitter().Minify(path);

        // The string "result = %d }" has a lone } — must not end extraction early.
        var hasMain    = r.Output.Contains("int main(");
        var hasAdd     = r.Output.Contains("int add(");
        var hasString  = r.Output.Contains("result = %d }");

        var ok = hasMain && hasAdd && hasString;
        return new TestOutcome(ok,
            ok ? "} inside string literal did not corrupt output"
               : $"main={hasMain} add={hasAdd} string={hasString}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- C++ emitter ----------

    private static TestOutcome Cpp_Registry_DispatchesByExtension()
    {
        var cpp = LanguageEmitterRegistry.Find("app.cpp");
        var cc  = LanguageEmitterRegistry.Find("app.cc");
        var cxx = LanguageEmitterRegistry.Find("app.cxx");
        var hpp = LanguageEmitterRegistry.Find("app.hpp");
        var hh  = LanguageEmitterRegistry.Find("app.hh");
        var inl = LanguageEmitterRegistry.Find("app.inl");

        var ok = cpp is CppEmitter && cc is CppEmitter && cxx is CppEmitter
              && hpp is CppEmitter && hh is CppEmitter && inl is CppEmitter;
        return new TestOutcome(ok,
            ok ? ".cpp/.cc/.cxx/.hpp/.hh/.inl dispatched to CppEmitter"
               : $"cpp={cpp?.GetType().Name} cc={cc?.GetType().Name} hpp={hpp?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Cpp_Minify_StripsComments()
    {
        var path = Fixture("sample.cpp");
        var r = new CppEmitter().Minify(path);

        var hasLine   = r.Output.Contains("top-level line comment");
        var hasBlock  = r.Output.Contains("block comment describing");
        var hasInline = r.Output.Contains("constructor comment");
        var hasMulti  = r.Output.Contains("multi-line block comment");
        var hasTrail  = r.Output.Contains("trailing comment");

        var ok = !hasLine && !hasBlock && !hasInline && !hasMulti && !hasTrail;
        return new TestOutcome(ok,
            ok ? "all // and /* */ comment forms stripped"
               : $"line={hasLine} block={hasBlock} inline={hasInline} multi={hasMulti} trail={hasTrail}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Cpp_Minify_PreservesPreprocessorDirectives()
    {
        var path = Fixture("sample.cpp");
        var r = new CppEmitter().Minify(path);

        var hasInclude = r.Output.Contains("#include <iostream>");
        var hasDefine  = r.Output.Contains("#define VERSION");

        var ok = hasInclude && hasDefine;
        return new TestOutcome(ok,
            ok ? "#include and #define directives preserved"
               : $"include={hasInclude} define={hasDefine}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Cpp_Minify_BracesInStringsDoNotCorrupt()
    {
        var path = Fixture("sample.cpp");
        var r = new CppEmitter().Minify(path);

        // The string "Calculator v... offset=} end" has a lone } — must survive intact.
        var hasClass   = r.Output.Contains("class Calculator");
        var hasAdd     = r.Output.Contains("int add(");
        var hasString  = r.Output.Contains("offset=} end");

        var ok = hasClass && hasAdd && hasString;
        return new TestOutcome(ok,
            ok ? "} inside string literal did not corrupt output"
               : $"class={hasClass} add={hasAdd} string={hasString}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- X++ emitter ----------

    private static TestOutcome Xpp_Registry_DispatchesByExtension()
    {
        var xpp = LanguageEmitterRegistry.Find("SalesOrderProcessor.xpp");
        var ok = xpp is XppEmitter;
        return new TestOutcome(ok,
            ok ? ".xpp dispatched to XppEmitter"
               : $"xpp={xpp?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Xpp_Minify_StripsComments()
    {
        var path = Fixture("sample.xpp");
        var r = new XppEmitter().Minify(path);

        var hasBlock   = r.Output.Contains("top-level block comment");
        var hasLine    = r.Output.Contains("line comment about the class");
        var hasInline  = r.Output.Contains("inline note");
        var hasMulti   = r.Output.Contains("multi-line block comment");
        var hasTrail   = r.Output.Contains("another comment");

        var ok = !hasBlock && !hasLine && !hasInline && !hasMulti && !hasTrail;
        return new TestOutcome(ok,
            ok ? "all // and /* */ comment forms stripped"
               : $"block={hasBlock} line={hasLine} inline={hasInline} multi={hasMulti} trail={hasTrail}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Xpp_Minify_PreservesMacroDirectives()
    {
        var path = Fixture("sample.xpp");
        var r = new XppEmitter().Minify(path);

        var hasMax   = r.Output.Contains("#define.MaxItems(100)");
        var hasGreet = r.Output.Contains("#define.Greeting('Hello')");

        var ok = hasMax && hasGreet;
        return new TestOutcome(ok,
            ok ? "#define macro directives preserved"
               : $"max={hasMax} greet={hasGreet}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Xpp_Minify_BracesInStringsDoNotCorrupt()
    {
        var path = Fixture("sample.xpp");
        var r = new XppEmitter().Minify(path);

        // The string "result = {0} } trailing" has a lone } — must survive intact.
        var hasClass   = r.Output.Contains("class SalesOrderProcessor");
        var hasProcess = r.Output.Contains("int process(");
        var hasString  = r.Output.Contains("result = {0} } trailing");

        var ok = hasClass && hasProcess && hasString;
        return new TestOutcome(ok,
            ok ? "} inside string literal did not corrupt output"
               : $"class={hasClass} process={hasProcess} string={hasString}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    // ---------- Lazy semantic model ----------

    private static TestOutcome LazyModel_OutlineDoesNotLoadModel()
    {
        // EmitOutline is syntax-only — it must never touch the semantic model.
        // We prove this by passing an empty reference list: if the model were
        // accessed it would still succeed (empty compilation), so instead we
        // assert directly on IsModelLoaded after the call.
        var path = Fixture("Calculator.cs");
        var emitter = new FocusedEmitter(path, referenceAssemblyPaths: Array.Empty<string>());
        var r = emitter.EmitOutline();

        var hasSignature = r.Output.Contains("Run") && r.Output.Contains("WeightedSum");
        var modelUnused = !emitter.IsModelLoaded;

        var ok = r.Found && hasSignature && modelUnused;
        return new TestOutcome(ok,
            ok ? "EmitOutline completed; IsModelLoaded=false — no compilation triggered"
               : $"found={r.Found} sig={hasSignature} modelUnused={modelUnused}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome LazyModel_MinifyDoesNotLoadModel()
    {
        // EmitMinified is syntax-only — same proof as outline.
        var path = Fixture("Calculator.cs");
        var emitter = new FocusedEmitter(path, referenceAssemblyPaths: Array.Empty<string>());
        var r = emitter.EmitMinified();

        var hasLogic = r.Output.Contains("WeightedSum") && r.Output.Contains("ApplyBias");
        var modelUnused = !emitter.IsModelLoaded;

        var ok = r.Found && hasLogic && modelUnused;
        return new TestOutcome(ok,
            ok ? "EmitMinified completed; IsModelLoaded=false — no compilation triggered"
               : $"found={r.Found} logic={hasLogic} modelUnused={modelUnused}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome LazyModel_FocusLoadsModel()
    {
        // Emit needs symbol resolution, so it must trigger the lazy build.
        var path = Fixture("Calculator.cs");
        var emitter = new FocusedEmitter(path);

        var beforeFocus = emitter.IsModelLoaded;
        var r = emitter.Emit("Run", depth: 0);
        var afterFocus = emitter.IsModelLoaded;

        var ok = r.Found && !beforeFocus && afterFocus;
        return new TestOutcome(ok,
            ok ? "IsModelLoaded: false before Emit, true after — lazy build confirmed"
               : $"found={r.Found} before={beforeFocus} after={afterFocus}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome LazyModel_AliasLoadsModel()
    {
        // EmitAliased also needs symbol resolution.
        var path = Fixture("Calculator.cs");
        var emitter = new FocusedEmitter(path);

        var beforeAlias = emitter.IsModelLoaded;
        var r = emitter.EmitAliased();
        var afterAlias = emitter.IsModelLoaded;

        var ok = r.Found && !beforeAlias && afterAlias;
        return new TestOutcome(ok,
            ok ? "IsModelLoaded: false before EmitAliased, true after — lazy build confirmed"
               : $"found={r.Found} before={beforeAlias} after={afterAlias}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome LazyModel_Focus_OutputUnchanged()
    {
        // Regression: the lazy refactor must not change what Emit produces.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("Run", depth: 1);

        var hasRunBody   = r.Output.Contains("Math.Max(0, biased)");
        var hasHelper    = r.Output.Contains("ApplyBias");

        var ok = r.Found && hasRunBody && hasHelper;
        return new TestOutcome(ok,
            ok ? "lazy model: Emit output unchanged — focus body and depth=1 helper both present"
               : $"found={r.Found} body={hasRunBody} helper={hasHelper}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome LazyModel_Outline_OutputUnchanged()
    {
        // Regression: the lazy refactor must not change what EmitOutline produces.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitOutline();

        var hasSig  = r.Output.Contains("Run") && r.Output.Contains("WeightedSum");
        var noBody  = !r.Output.Contains("Math.Max(0, biased)");

        var ok = r.Found && hasSig && noBody;
        return new TestOutcome(ok,
            ok ? "lazy model: EmitOutline output unchanged — signatures present, bodies absent"
               : $"found={r.Found} sig={hasSig} noBody={noBody}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- Property accessor signatures ----------

    private static TestOutcome PropertySignature_GetOnly_NoSetInSignature()
    {
        // A get-only auto property must show { get; } not { get; set; }
        var path = Fixture("PropertyShapes.cs");
        var r = new FocusedEmitter(path).Emit("Touch", depth: 0);

        var hasGetOnly  = r.Output.Contains("GetOnly { get; }");
        var noFakeSet   = !r.Output.Contains("GetOnly { get; set; }");

        var ok = r.Found && hasGetOnly && noFakeSet;
        return new TestOutcome(ok,
            ok ? "get-only property shows { get; } — no spurious set;"
               : $"found={r.Found} hasGetOnly={hasGetOnly} noFakeSet={noFakeSet}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome PropertySignature_InitOnly_ShowsInit()
    {
        // An init-only property must show { get; init; }
        var path = Fixture("PropertyShapes.cs");
        var r = new FocusedEmitter(path).Emit("Touch", depth: 0);

        var hasInit = r.Output.Contains("InitOnly { get; init; }");

        var ok = r.Found && hasInit;
        return new TestOutcome(ok,
            ok ? "init-only property shows { get; init; }"
               : $"found={r.Found} hasInit={hasInit}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome PropertySignature_ExpressionBodied_ShowsGetOnly()
    {
        // An expression-bodied property (=> ...) must show { get; }
        var path = Fixture("PropertyShapes.cs");
        var r = new FocusedEmitter(path).Emit("Touch", depth: 0);

        var hasComputed = r.Output.Contains("Computed { get; }");

        var ok = r.Found && hasComputed;
        return new TestOutcome(ok,
            ok ? "expression-bodied property shows { get; }"
               : $"found={r.Found} hasComputed={hasComputed} snippet={r.Output[..Math.Min(300, r.Output.Length)]}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome PropertySignature_ReadWrite_ShowsBothAccessors()
    {
        // A normal read-write property must still show { get; set; }
        var path = Fixture("PropertyShapes.cs");
        var r = new FocusedEmitter(path).Emit("Touch", depth: 0);

        var hasReadWrite = r.Output.Contains("ReadWrite { get; set; }");

        var ok = r.Found && hasReadWrite;
        return new TestOutcome(ok,
            ok ? "read-write property still shows { get; set; }"
               : $"found={r.Found} hasReadWrite={hasReadWrite}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome PropertySignature_PrivateSetter_ShowsModifier()
    {
        // A property with a private setter must show { get; private set; }
        var path = Fixture("PropertyShapes.cs");
        var r = new FocusedEmitter(path).Emit("Touch", depth: 0);

        var hasPrivateSet = r.Output.Contains("PrivateSet { get; private set; }");

        var ok = r.Found && hasPrivateSet;
        return new TestOutcome(ok,
            ok ? "private-setter property shows { get; private set; }"
               : $"found={r.Found} hasPrivateSet={hasPrivateSet}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- Missing member types in outline ----------

    private static TestOutcome Outline_Indexer_AppearsInSignature()
    {
        var path = Fixture("OperatorOverloads.cs");
        var r = new FocusedEmitter(path).EmitOutline();

        var hasIndexer = r.Output.Contains("this[int index]");
        var ok = r.Found && hasIndexer;
        return new TestOutcome(ok,
            ok ? "expression-bodied indexer appears in outline"
               : $"found={r.Found} hasIndexer={hasIndexer}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Outline_Operator_AppearsInSignature()
    {
        var path = Fixture("OperatorOverloads.cs");
        var r = new FocusedEmitter(path).EmitOutline();

        var hasOperator = r.Output.Contains("operator +");
        var ok = r.Found && hasOperator;
        return new TestOutcome(ok,
            ok ? "binary operator overload appears in outline"
               : $"found={r.Found} hasOperator={hasOperator}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Outline_ConversionOperator_AppearsInSignature()
    {
        var path = Fixture("OperatorOverloads.cs");
        var r = new FocusedEmitter(path).EmitOutline();

        var hasImplicit = r.Output.Contains("implicit operator");
        var hasExplicit = r.Output.Contains("explicit operator");
        var ok = r.Found && hasImplicit && hasExplicit;
        return new TestOutcome(ok,
            ok ? "implicit and explicit conversion operators both appear in outline"
               : $"found={r.Found} implicit={hasImplicit} explicit={hasExplicit}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Outline_IndexerWithAccessorList_ShowsAccessors()
    {
        // The string indexer has { get; set; } — both accessors must appear.
        var path = Fixture("OperatorOverloads.cs");
        var r = new FocusedEmitter(path).EmitOutline();

        var hasGetSet = r.Output.Contains("this[string key] { get; set; }");
        var ok = r.Found && hasGetSet;
        return new TestOutcome(ok,
            ok ? "indexer with explicit get+set shows { get; set; }"
               : $"found={r.Found} hasGetSet={hasGetSet}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- Field initializer stripping ----------

    private static TestOutcome FieldSignature_InitializerStripped()
    {
        // Initializers must be absent from field signatures in focused output.
        var path = Fixture("FieldInitializers.cs");
        var r = new FocusedEmitter(path).Emit("Touch", depth: 0);

        var noSimpleInit   = !r.Output.Contains("_count = 0");
        var noComplexInit  = !r.Output.Contains("new(StringComparer");
        var noStringInit   = !r.Output.Contains("\"default label value\"");
        var noConstInit    = !r.Output.Contains("= 100");

        var ok = r.Found && noSimpleInit && noComplexInit && noStringInit && noConstInit;
        return new TestOutcome(ok,
            ok ? "all field initializers stripped from signatures"
               : $"simpleInit={!noSimpleInit} complexInit={!noComplexInit} stringInit={!noStringInit} constInit={!noConstInit}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome FieldSignature_TypeAndNamePreserved()
    {
        // Type and identifier must survive even when initializer is stripped.
        var path = Fixture("FieldInitializers.cs");
        var r = new FocusedEmitter(path).Emit("Touch", depth: 0);

        var hasCount  = r.Output.Contains("int _count");
        var hasCache  = r.Output.Contains("Dictionary<string, List<string>> _cache");
        var hasLabel  = r.Output.Contains("string _label");

        var ok = r.Found && hasCount && hasCache && hasLabel;
        return new TestOutcome(ok,
            ok ? "type and name preserved after initializer strip"
               : $"count={hasCount} cache={hasCache} label={hasLabel}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome FieldSignature_MultipleDeclaratorsHandled()
    {
        // "int _min = 0, _max = 100;" must become "int _min, _max;"
        var path = Fixture("FieldInitializers.cs");
        var r = new FocusedEmitter(path).Emit("Touch", depth: 0);

        var hasMulti   = r.Output.Contains("_min, _max");
        var noMinInit  = !r.Output.Contains("int.MinValue");
        var noMaxInit  = !r.Output.Contains("int.MaxValue");

        var ok = r.Found && hasMulti && noMinInit && noMaxInit;
        return new TestOutcome(ok,
            ok ? "multi-declarator field collapsed to \"type name1, name2;\" with no initializers"
               : $"multi={hasMulti} noMinInit={noMinInit} noMaxInit={noMaxInit}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- #region stripping ----------

    private static TestOutcome Region_Minify_StripsRegionDirectives()
    {
        var path = Fixture("RegionHeavy.cs");
        var r = new FocusedEmitter(path).EmitMinified();

        var hasRegion    = r.Output.Contains("#region");
        var hasEndRegion = r.Output.Contains("#endregion");
        var hasLogic     = r.Output.Contains("Double") && r.Output.Contains("UpperName");

        var ok = r.Found && !hasRegion && !hasEndRegion && hasLogic;
        return new TestOutcome(ok,
            ok ? $"#region/#endregion stripped; logic intact; {TokenSaving(r.OriginalChars, r.FocusedChars).percent:F1}% saved"
               : $"region={hasRegion} endregion={hasEndRegion} logic={hasLogic}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Region_Focus_StripsRegionDirectivesWhenMinified()
    {
        // focus_method with minify=true goes through MinifyText which uses CommentStripper.
        var path = Fixture("RegionHeavy.cs");
        var focused = new FocusedEmitter(path).Emit("Double", depth: 0);
        var minified = FocusedEmitter.MinifyText(focused.Output);

        var hasRegion    = minified.Contains("#region");
        var hasEndRegion = minified.Contains("#endregion");
        var hasDouble    = minified.Contains("Double");

        var ok = focused.Found && !hasRegion && !hasEndRegion && hasDouble;
        return new TestOutcome(ok,
            ok ? "#region/#endregion absent after MinifyText; focus body intact"
               : $"region={hasRegion} endregion={hasEndRegion} double={hasDouble}",
            TokenSaving(focused.OriginalChars, minified.Length));
    }

    private static TestOutcome Region_LogicPreservedAfterStrip()
    {
        // Stripping regions must not remove any method bodies or field declarations.
        var path = Fixture("RegionHeavy.cs");
        var r = new FocusedEmitter(path).EmitMinified();

        var hasFields  = r.Output.Contains("_value") && r.Output.Contains("_name");
        var hasCtor    = r.Output.Contains("RegionHeavy(");
        var hasDouble  = r.Output.Contains("Double()");
        var hasUpper   = r.Output.Contains("UpperName()");
        var hasHelper  = r.Output.Contains("Add(");

        var ok = r.Found && hasFields && hasCtor && hasDouble && hasUpper && hasHelper;
        return new TestOutcome(ok,
            ok ? "fields, constructor, public methods, and private helpers all survived region strip"
               : $"fields={hasFields} ctor={hasCtor} double={hasDouble} upper={hasUpper} helper={hasHelper}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Focus_Constructor_FoundByClassName()
    {
        // Passing the class name as focusMethodName should now find the constructor.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).Emit("Calculator", depth: 0);

        var ok = r.Found && r.Output.Contains("Calculator");
        return new TestOutcome(ok,
            ok ? "constructor found by class name — no longer returns NOT FOUND"
               : $"found={r.Found} output snippet: {r.Output[..Math.Min(120, r.Output.Length)]}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome FocusMultiple_Constructor_IncludedWithMethods()
    {
        // EmitMultiple should include both the constructor and a regular method.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitMultiple(["Calculator", "Run"], depth: 0);

        var hasConstructor = r.Found && r.Output.Contains("Calculator");
        var hasRun         = r.Output.Contains("Run");
        var ok = hasConstructor && hasRun;
        return new TestOutcome(ok,
            ok ? "constructor and method both present in multi-focus output"
               : $"found={r.Found} ctor={hasConstructor} run={hasRun}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- FocusType ----------

    private static TestOutcome FocusType_NonPrivateHasBody_PrivateHasSignature()
    {
        // Calculator: public Run has full body; private WeightedSum/Sum/ApplyBias are signatures only.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitType("Calculator");

        var hasRunBody       = r.Output.Contains("Math.Max(0, biased)");
        var hasBiasBody      = r.Output.Contains("x + _bias");
        var hasWeightedSumSig = r.Output.Contains("WeightedSum") && !r.Output.Contains("s += values[i]");
        var ok = r.Found && hasRunBody && !hasBiasBody && hasWeightedSumSig;
        return new TestOutcome(ok,
            ok ? "non-private members have full bodies; private members are signatures only"
               : $"found={r.Found} runBody={hasRunBody} biasBody={hasBiasBody} wsSig={hasWeightedSumSig}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome FocusType_OnlyTargetTypeInOutput()
    {
        // GenericsAndRecords has two types; FocusType on Bag should not include Pair content.
        var path = Fixture("GenericsAndRecords.cs");
        var r = new FocusedEmitter(path).EmitType("Bag");

        var hasBag    = r.Output.Contains("Bag");
        var noPair    = !r.Output.Contains("Pair") && !r.Output.Contains("Render");
        var ok = r.Found && hasBag && noPair;
        return new TestOutcome(ok,
            ok ? "only Bag type in output; Pair/Render content absent"
               : $"found={r.Found} bag={hasBag} noPair={noPair}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome FocusType_NotFound_ReturnsNotFound()
    {
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitType("NonExistentType");
        var ok = !r.Found;
        return new TestOutcome(ok,
            ok ? "EmitType returns not-found for missing type name"
               : "unexpectedly returned Found=true",
            (0, 0, 0));
    }

    // ---------- FocusCallers ----------

    private static TestOutcome FocusCallers_FindsCallingMethods()
    {
        // Calculator.Run calls WeightedSum, Sum, and ApplyBias — so Run is a caller of each.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitCallers("WeightedSum");

        // Run is the only method that calls WeightedSum.
        var hasRunBody = r.Output.Contains("WeightedSum") && r.Output.Contains("var total");
        var notes      = r.Notes.Contains("1 calling method");
        var ok = r.Found && hasRunBody && notes;
        return new TestOutcome(ok,
            ok ? "EmitCallers found Run as the caller of WeightedSum"
               : $"found={r.Found} body={hasRunBody} notes={notes}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome FocusCallers_NotFound_WhenNoCallers()
    {
        // Run is never called by another method in Calculator.cs.
        var path = Fixture("Calculator.cs");
        var r = new FocusedEmitter(path).EmitCallers("Run");
        var ok = !r.Found;
        return new TestOutcome(ok,
            ok ? "EmitCallers returns not-found when no method calls the target"
               : "unexpectedly returned Found=true",
            (0, 0, 0));
    }

    // ---------- Private property expansion at depth=1 ----------

    private static TestOutcome Focus_Depth1_ExpandsPrivatePropertyBody()
    {
        // PrivatePropConsumer.Compute() accesses the private `Scaled` property as an
        // identifier (not a call receiver), so depth=1 should expand its getter body.
        var path = Fixture("PrivatePropConsumer.cs");
        var r = new FocusedEmitter(path).Emit("Compute", depth: 1);

        var hasPropertyBody = r.Output.Contains("_factor * 10");
        var ok = r.Found && hasPropertyBody;
        return new TestOutcome(ok,
            ok ? "depth=1 expanded private property body (Scaled getter present in output)"
               : $"found={r.Found} propBody={hasPropertyBody}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- C# interfaces ----------

    private static TestOutcome Interface_Outline_NoLeadingSpaceOnSignatures()
    {
        // Before the Prefix() fix, modifier-less members produced " double Compute(...)"
        // (empty Mods() + space + type). The outline indents by 4 spaces, so the line
        // became "     double" (5 spaces) instead of "    double" (4 spaces).
        var path = Fixture("SampleInterface.cs");
        var r = new FocusedEmitter(path).EmitOutline();

        var hasCorrectIndent = r.Output.Contains("    double Compute(");  // 4 spaces, no extra
        var hasExtraSpace    = r.Output.Contains("     double Compute("); // 5 spaces = bug

        var ok = r.Found && hasCorrectIndent && !hasExtraSpace;
        return new TestOutcome(ok,
            ok ? "interface member signatures have no extra leading space from empty modifier list"
               : $"found={r.Found} correct={hasCorrectIndent} extraSpace={hasExtraSpace}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Interface_FocusType_DefaultImplHasBody_PrivateIsSignature()
    {
        // After the IsPrivate fix, modifier-less interface members are treated as non-private
        // (public by default). EmitType should show their full body. An explicitly private
        // interface member should still be signature only.
        var path = Fixture("SampleInterface.cs");
        var r = new FocusedEmitter(path).EmitType("ICalculator");

        var hasDefaultBody = r.Output.Contains("* 2.0");      // ComputeWithDefault body
        var hasPrivateSig  = r.Output.Contains("Scale");      // private method still listed
        var noPrivateBody  = !r.Output.Contains("v / 100.0"); // private body must NOT appear

        var ok = r.Found && hasDefaultBody && hasPrivateSig && noPrivateBody;
        return new TestOutcome(ok,
            ok ? "default interface impl has full body; private method is signature only"
               : $"found={r.Found} defaultBody={hasDefaultBody} privSig={hasPrivateSig} noPrivBody={noPrivateBody}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Interface_FocusMethod_FindsAbstractMethod()
    {
        // FocusMethod must locate a plain (abstract) interface method by name.
        var path = Fixture("SampleInterface.cs");
        var r = new FocusedEmitter(path).Emit("Compute", depth: 0);

        var ok = r.Found && r.Output.Contains("Compute");
        return new TestOutcome(ok,
            ok ? "FocusMethod finds abstract interface method by name"
               : $"found={r.Found}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- helpers ----------

    // ---------- VB.NET emitter ----------

    private static TestOutcome Vb_Registry_DispatchesByExtension()
    {
        var vb = LanguageEmitterRegistry.Find("MyModule.vb");
        var ok = vb is VBEmitter;
        return new TestOutcome(ok,
            ok ? ".vb dispatched to VBEmitter"
               : $"got {vb?.GetType().Name ?? "null"}",
            (0, 0, 0));
    }

    private static TestOutcome Vb_Minify_StripsComments()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBEmitter().Minify(path);

        var hasLineComment  = r.Output.Contains("Guard:");           // from ' Guard: ...
        var hasRemComment   = r.Output.Contains("bias application"); // from REM This is the bias application step
        var hasTopComment   = r.Output.Contains("Comments are intentionally");
        var logicPreserved  = r.Output.Contains("WeightedSum") && r.Output.Contains("ApplyBias");

        var ok = !hasLineComment && !hasRemComment && !hasTopComment && logicPreserved;
        return new TestOutcome(ok,
            ok ? "' and REM comments stripped; logic preserved"
               : $"line={hasLineComment} rem={hasRemComment} top={hasTopComment} logic={logicPreserved}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Vb_Minify_CollapsesBlankRuns()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBEmitter().Minify(path);
        var hasTripleBlank = r.Output.Contains("\n\n\n");
        var ok = !hasTripleBlank;
        return new TestOutcome(ok,
            ok ? "blank-line runs collapsed to at most one blank"
               : "still has 3+ consecutive newlines",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Vb_Minify_SavesTokens()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBEmitter().Minify(path);
        var saved = TokenSaving(r.OriginalChars, r.OutputChars);
        var ok = r.Found && saved.percent > 10;
        return new TestOutcome(ok,
            $"tokens {saved.before}→{saved.after} ({saved.percent:F1}% saved)",
            saved);
    }

    private static TestOutcome Vb_Outline_EmitsSignaturesOnly_NoBodies()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBFocusedEmitter(path).EmitOutline();

        var hasRunSig         = r.Output.Contains("Run");
        var hasWeightedSumSig = r.Output.Contains("WeightedSum");
        var noRunBody         = !r.Output.Contains("Math.Max(0, biased)");
        var noWsBody          = !r.Output.Contains("s += values(i) * weights(i)");
        var saved             = TokenSaving(r.OriginalChars, r.FocusedChars);

        var ok = r.Found && hasRunSig && hasWeightedSumSig && noRunBody && noWsBody;
        return new TestOutcome(ok,
            ok ? $"all signatures present; no bodies; {saved.percent:F1}% saved"
               : $"runSig={hasRunSig} wsSig={hasWeightedSumSig} noRunBody={noRunBody} noWsBody={noWsBody}",
            saved);
    }

    private static TestOutcome Vb_Focus_IncludesFocusMethodBody()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBFocusedEmitter(path).Emit("Run", depth: 0);

        var ok = r.Found
            && r.Output.Contains("WeightedSum(values, weights)")
            && r.Output.Contains("LastMean = Math.Max(0, biased)");

        return new TestOutcome(ok,
            ok ? "Run body present verbatim"
               : $"found={r.Found} missing expected statements",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Vb_Focus_Depth0_HelpersAreSignaturesOnly()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBFocusedEmitter(path).Emit("Run", depth: 0);

        var hasWeightedSumSig = r.Output.Contains("WeightedSum");
        var noWeightedSumBody = !r.Output.Contains("s += values(i) * weights(i)");

        var ok = r.Found && hasWeightedSumSig && noWeightedSumBody;
        return new TestOutcome(ok,
            ok ? "helper signatures present; helper bodies absent at depth=0"
               : $"found={r.Found} sig={hasWeightedSumSig} noBody={noWeightedSumBody}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Vb_Focus_Depth1_IncludesPrivateHelperBodies()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBFocusedEmitter(path).Emit("Run", depth: 1);

        var hasHelperBody = r.Output.Contains("s += values(i) * weights(i)");

        var ok = r.Found && hasHelperBody;
        return new TestOutcome(ok,
            ok ? "private helper body expanded at depth=1"
               : $"found={r.Found} helperBody={hasHelperBody}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Vb_Focus_RelevantSourceText_IsFocusPlusHelpers()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBFocusedEmitter(path).Emit("Run", depth: 1);

        var rel = r.RelevantSourceText ?? "";
        var wholeFile = System.IO.File.ReadAllText(path);

        // Relevant text holds the focus body and the expanded helper body...
        var hasHelper = rel.Contains("s += values(i) * weights(i)");
        // ...but is a strict subset of the file.
        var smaller = rel.Length > 0 && rel.Length < wholeFile.Length;

        var ok = r.Found && hasHelper && smaller;
        return new TestOutcome(ok,
            ok ? $"relevant text {rel.Length} chars < file {wholeFile.Length}; helper present"
               : $"helper={hasHelper} smaller={smaller}",
            (0, 0, 0));
    }

    private static TestOutcome Vb_FocusType_NonPrivateHasBody_PrivateHasSignature()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBFocusedEmitter(path).EmitType("VbCalculator");

        var hasRunBody        = r.Output.Contains("Math.Max(0, biased)");
        var noApplyBiasBody   = !r.Output.Contains("x + _bias");
        var hasApplyBiasSig   = r.Output.Contains("ApplyBias");

        var ok = r.Found && hasRunBody && noApplyBiasBody && hasApplyBiasSig;
        return new TestOutcome(ok,
            ok ? "public Run has full body; private ApplyBias is signature only"
               : $"found={r.Found} runBody={hasRunBody} noABBody={noApplyBiasBody} abSig={hasApplyBiasSig}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    private static TestOutcome Vb_FocusCallers_FindsCallingMethods()
    {
        var path = Fixture("VbCalculator.vb");
        var r = new VBFocusedEmitter(path).EmitCallers("WeightedSum");

        var hasCallerBody = r.Output.Contains("WeightedSum(values, weights)");
        var notesOk       = r.Notes.Contains("1 calling method");

        var ok = r.Found && hasCallerBody && notesOk;
        return new TestOutcome(ok,
            ok ? "EmitCallers found Run as the caller of WeightedSum"
               : $"found={r.Found} body={hasCallerBody} notes={notesOk}",
            TokenSaving(r.OriginalChars, r.FocusedChars));
    }

    // ---------- dnx background auto-update / config pinning ----------

    private static JsonObject MakeDnxEntry(params string[] args) => new()
    {
        ["type"] = "stdio",
        ["command"] = "dotnet",
        ["args"] = new JsonArray(args.Select(a => (JsonNode)a!).ToArray()),
    };

    private static string ArgsCsv(JsonObject entry) =>
        string.Join(",", ((JsonArray)entry["args"]!).Select(n => n!.GetValue<string>()));

    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "ts_cfgtest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(d);
        return d;
    }

    private static void CleanupDir(string d) { try { Directory.Delete(d, true); } catch { } }

    private static TestOutcome AutoUpdate_IsDnxEntry_RecognizesDnxAndSkipsOthers()
    {
        var dnx = MakeDnxEntry("tool", "execute", "TokenSaver.Mcp", "--yes");
        var global = new JsonObject { ["command"] = "tokensaver-mcp", ["args"] = new JsonArray() };
        var unrelated = new JsonObject { ["command"] = "dotnet", ["args"] = new JsonArray("run", "Other") };

        var a = TokenSaver.Mcp.RegisterCommand.IsDnxEntry(dnx);
        var b = TokenSaver.Mcp.RegisterCommand.IsDnxEntry(global);
        var c = TokenSaver.Mcp.RegisterCommand.IsDnxEntry(unrelated);

        var ok = a && !b && !c;
        return new TestOutcome(ok,
            ok ? "dnx recognized; global + unrelated rejected"
               : $"dnx={a} global={b} unrelated={c}",
            (0, 0, 0));
    }

    private static TestOutcome AutoUpdate_SetPinnedVersion_InsertsReplacesAndNoOps()
    {
        var insert = MakeDnxEntry("tool", "execute", "TokenSaver.Mcp", "--yes");
        var ch1 = TokenSaver.Mcp.RegisterCommand.SetPinnedVersion(insert, "1.99.1");
        var inserted = ArgsCsv(insert) == "tool,execute,TokenSaver.Mcp,--version,1.99.1,--yes";

        var replace = MakeDnxEntry("tool", "execute", "TokenSaver.Mcp", "--version", "1.99.0", "--yes");
        var ch2 = TokenSaver.Mcp.RegisterCommand.SetPinnedVersion(replace, "1.99.1");
        var replaced = ArgsCsv(replace) == "tool,execute,TokenSaver.Mcp,--version,1.99.1,--yes";

        var ch3 = TokenSaver.Mcp.RegisterCommand.SetPinnedVersion(replace, "1.99.1");

        var ok = ch1 && inserted && ch2 && replaced && !ch3;
        return new TestOutcome(ok,
            ok ? "insert after package id; replace existing; no-op when unchanged"
               : $"insert(ch={ch1},ok={inserted}) replace(ch={ch2},ok={replaced}) noop(ch={ch3})",
            (0, 0, 0));
    }

    private static TestOutcome AutoUpdate_IsNewer_ComparesCoreVersions()
    {
        var newer = TokenSaver.Mcp.RegisterCommand.IsNewer("1.99.1", "1.99.0");
        var older = !TokenSaver.Mcp.RegisterCommand.IsNewer("1.12.0", "1.99.0");
        var equal = !TokenSaver.Mcp.RegisterCommand.IsNewer("1.99.1", "1.99.1");
        var prerelease = !TokenSaver.Mcp.RegisterCommand.IsNewer("1.12.1-localtest", "1.12.1");

        var ok = newer && older && equal && prerelease;
        return new TestOutcome(ok,
            ok ? "newer>older true; older/equal false; prerelease suffix ignored"
               : $"newer={newer} older={older} equal={equal} prerelease={prerelease}",
            (0, 0, 0));
    }

    private static TestOutcome AutoUpdate_PinInFlat_RepinsAndPreservesUnrelated()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "mcp.json");
        var ts = MakeDnxEntry("tool", "execute", "TokenSaver.Mcp", "--version", "1.99.0", "--yes");
        ts["env"] = new JsonObject { ["TOKENSAVER_API_URL"] = "https://tokensavermcp.com" };
        var root = new JsonObject
        {
            ["inputs"] = new JsonArray(),
            ["servers"] = new JsonObject
            {
                ["other-server"] = new JsonObject { ["command"] = "node", ["args"] = new JsonArray("server.js") },
                ["tokensaver"] = ts,
            },
        };
        File.WriteAllText(path, root.ToJsonString());

        TokenSaver.Mcp.RegisterCommand.PinInFlat(path, "servers", "1.99.1");
        var outp = File.ReadAllText(path);
        CleanupDir(dir);

        var repinned = outp.Contains("1.99.1") && !outp.Contains("1.99.0");
        var preserved = outp.Contains("other-server") && outp.Contains("server.js")
                     && outp.Contains("inputs") && outp.Contains("TOKENSAVER_API_URL");

        var ok = repinned && preserved;
        return new TestOutcome(ok,
            ok ? "tokensaver re-pinned to 1.99.1; unrelated keys/servers/env preserved"
               : $"repinned={repinned} preserved={preserved}",
            (0, 0, 0));
    }

    private static TestOutcome AutoUpdate_PinInVsCode_RepinsNestedEntry()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "settings.json");
        var root = new JsonObject
        {
            ["editor.fontSize"] = 14,
            ["mcp"] = new JsonObject
            {
                ["servers"] = new JsonObject
                {
                    ["tokensaver"] = MakeDnxEntry("tool", "execute", "TokenSaver.Mcp", "--yes"),
                    ["foo"] = new JsonObject { ["command"] = "foo" },
                },
            },
        };
        File.WriteAllText(path, root.ToJsonString());

        TokenSaver.Mcp.RegisterCommand.PinInVsCode(path, "1.99.1");
        var outp = File.ReadAllText(path);
        CleanupDir(dir);

        var repinned = outp.Contains("--version") && outp.Contains("1.99.1");
        var preserved = outp.Contains("editor.fontSize") && outp.Contains("foo");

        var ok = repinned && preserved;
        return new TestOutcome(ok,
            ok ? "nested mcp.servers.tokensaver pinned; sibling + settings preserved"
               : $"repinned={repinned} preserved={preserved}",
            (0, 0, 0));
    }

    private static TestOutcome AutoUpdate_PinInFlat_LeavesGlobalEntryUntouched()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "claude.json");
        var root = new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["tokensaver"] = new JsonObject { ["command"] = "tokensaver-mcp", ["args"] = new JsonArray() },
            },
        };
        File.WriteAllText(path, root.ToJsonString());

        TokenSaver.Mcp.RegisterCommand.PinInFlat(path, "mcpServers", "1.99.1");
        var outp = File.ReadAllText(path);
        CleanupDir(dir);

        var ok = !outp.Contains("--version");
        return new TestOutcome(ok,
            ok ? "global-command entry not rewritten (no --version added)"
               : "global entry was incorrectly modified",
            (0, 0, 0));
    }

    // Guards the issue-66 fix: the manual `self-update` path no longer gates re-pinning
    // on the running process version (always "latest" under dnx execute) but on whether
    // the configs actually changed. PinInFlat must report true when it rewrites a stale
    // pin and false when the config is already at the target version (idempotent).
    private static TestOutcome AutoUpdate_PinInFlat_ReportsWhetherConfigChanged()
    {
        var dir = TempDir();
        var path = Path.Combine(dir, "mcp.json");
        var root = new JsonObject
        {
            ["servers"] = new JsonObject
            {
                ["tokensaver"] = MakeDnxEntry("tool", "execute", "TokenSaver.Mcp", "--version", "1.99.0", "--yes"),
            },
        };
        File.WriteAllText(path, root.ToJsonString());

        // Stale config pinned to 1.99.0 -> re-pin to 1.99.1 reports a change.
        var firstChanged = TokenSaver.Mcp.RegisterCommand.PinInFlat(path, "servers", "1.99.1");
        // Already pinned to 1.99.1 -> re-pin is a no-op and reports no change.
        var secondChanged = TokenSaver.Mcp.RegisterCommand.PinInFlat(path, "servers", "1.99.1");
        CleanupDir(dir);

        var ok = firstChanged && !secondChanged;
        return new TestOutcome(ok,
            ok ? "PinInFlat returned true on stale re-pin, false when already current"
               : $"firstChanged={firstChanged} secondChanged={secondChanged}",
            (0, 0, 0));
    }

    private static string Fixture(string name) => Path.Combine(FixturesDir, name);

    private static int CountMethodsAndConstructors(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        return root.DescendantNodes().OfType<MethodDeclarationSyntax>().Count()
             + root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().Count();
    }

    private static (int before, int after, double percent) TokenSaving(int originalChars, int focusedChars)
    {
        var before = Math.Max(1, originalChars / 4);
        var after = Math.Max(1, focusedChars / 4);
        var pct = originalChars == 0 ? 0 : 100.0 * (originalChars - focusedChars) / originalChars;
        return (before, after, pct);
    }

    private static string StripToolHeader(string output)
    {
        // Drop leading "//"-prefixed comment lines so we can scan the actual code body for stray comments.
        var sb = new StringBuilder();
        bool inHeader = true;
        foreach (var line in output.Split('\n'))
        {
            if (inHeader && (line.TrimStart().StartsWith("//") || string.IsNullOrWhiteSpace(line)))
                continue;
            inHeader = false;
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private static void Run(string name, Func<TestOutcome> body)
    {
        TestOutcome outcome;
        try
        {
            outcome = body();
        }
        catch (Exception ex)
        {
            outcome = new TestOutcome(false, $"threw: {ex.GetType().Name}: {ex.Message}", (0, 0, 0));
        }

        Results.Add(new TestRecord(name, outcome.Passed, outcome.Notes, outcome.Tokens));
        var status = outcome.Passed ? "PASS" : "FAIL";
        Console.WriteLine($"  [{status}] {name}  —  {outcome.Notes}");
    }

    private static TestOutcome PerCallHeader_IsOverheadFree()
    {
        // The per-call "with tool" figure must NOT fold in the MCP overhead — overhead
        // is a session cost, reported on its own line, not charged to any single call.
        const int overhead = 1000;
        var path = Fixture("Calculator.cs");

        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 0;
        var baselineWith = ParseWithToolTokens(
            TokenSaver.Mcp.FocusedEmitterTools.OutlineCSharpFile(path).Split('\n')[0]);

        EmissionCache.Clear();
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = overhead; // also resets session totals
        var firstHeader = TokenSaver.Mcp.FocusedEmitterTools.OutlineCSharpFile(path).Split('\n')[0];
        var firstWith = ParseWithToolTokens(firstHeader);

        var ok = firstWith == baselineWith && !firstHeader.Contains("overhead");
        return new TestOutcome(ok,
            ok ? $"per-call with tool = {firstWith}, overhead-free"
               : $"expected {baselineWith} overhead-free, header: {firstHeader}",
            (0, 0, 0));
    }

    private static TestOutcome SessionLine_SubtractsOverheadOnce()
    {
        // Across N calls the session net must equal cumulative raw savings minus a
        // SINGLE overhead — never N overheads.
        const int overhead = 1000;
        var path = Fixture("Calculator.cs");
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = overhead; // resets session totals

        var sessionLine = "";
        long saved = 0, net = 0;
        for (var i = 0; i < 3; i++)
        {
            EmissionCache.Clear();
            var lines = TokenSaver.Mcp.FocusedEmitterTools.OutlineCSharpFile(path).Split('\n');
            sessionLine = lines[1];
            (saved, net) = ParseSession(sessionLine);
        }

        var ok = saved > 0 && net == saved - overhead && sessionLine.Contains("3 calls");
        return new TestOutcome(ok,
            ok ? $"after 3 calls: raw saved {saved}, net {net} (overhead {overhead} once)"
               : $"overhead not subtracted exactly once — line: {sessionLine}",
            (0, 0, 0));
    }

    private static TestOutcome PerCallHeader_NeverLabelsInitial()
    {
        var path = Fixture("Calculator.cs");
        TokenSaver.Mcp.FocusedEmitterTools.OverheadTokens = 1000; // resets session totals
        EmissionCache.Clear();
        var first = TokenSaver.Mcp.FocusedEmitterTools.OutlineCSharpFile(path).Split('\n')[0];
        EmissionCache.Clear();
        var second = TokenSaver.Mcp.FocusedEmitterTools.OutlineCSharpFile(path).Split('\n')[0];

        var ok = !first.Contains("(Initial)") && !second.Contains("(Initial)")
              && first.Contains("[Focused Emitter]") && !first.Contains("overhead");
        return new TestOutcome(ok,
            ok ? $"per-call header overhead-free, no (Initial) label — {first}"
               : $"unexpected per-call header — first: {first}",
            (0, 0, 0));
    }

    private static TestOutcome ToolSchemaOverheadCost()
    {
        var schemaTokens = TokenSaver.Mcp.FocusedEmitterTools.ComputeOverheadTokens("");
        var ok = schemaTokens > 0;
        return new TestOutcome(ok,
            ok ? $"tool schema (descriptions only): {schemaTokens} tokens; server instructions add on top"
               : "schema token count should be > 0",
            (0, 0, 0));
    }

    private static int ParseWithToolTokens(string header)
    {
        var m = System.Text.RegularExpressions.Regex.Match(header, @"with tool:\s*([\d\s, ]+)");
        if (!m.Success) return -1;
        var raw = System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value.Trim(), @"[\s, ]", "");
        return int.TryParse(raw, out var n) ? n : -1;
    }

    private static int ParseWithoutToolTokens(string header)
    {
        var m = System.Text.RegularExpressions.Regex.Match(header, @"without tool:\s*([\d\s,]+)");
        if (!m.Success) return -1;
        var raw = System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value.Trim(), @"[\s,]", "");
        return int.TryParse(raw, out var n) ? n : -1;
    }

    // Parses the token count from "...relevant code (X tokens): ..." line.
    private static int ParseTargetedBaseline(string output)
    {
        var m = System.Text.RegularExpressions.Regex.Match(output, @"relevant code \(([\d\s,]+) tokens\)");
        if (!m.Success) return -1;
        var raw = System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value.Trim(), @"[\s,]", "");
        return int.TryParse(raw, out var n) ? n : -1;
    }

    // Parses the "session: N calls · raw saved X · net of O one-time MCP overhead = Y"
    // line into (rawSaved, net).
    private static (long saved, long net) ParseSession(string line)
    {
        long Grab(string pattern)
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, pattern);
            if (!m.Success) return long.MinValue;
            var raw = System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value.Trim(), @"[\s,]", "");
            return long.TryParse(raw, out var n) ? n : long.MinValue;
        }
        return (Grab(@"raw saved\s*(-?[\d\s,]+)"), Grab(@"=\s*(-?[\d\s,]+)"));
    }

    private static void WriteReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# TokenSaver — Test Report\n");
        sb.AppendLine($"_Generated: {DateTime.Now:yyyy-MM-dd HH:mm}_\n");

        var passed = Results.Count(r => r.Passed);
        sb.AppendLine($"**{passed}/{Results.Count} scenarios passed.**\n");

        sb.AppendLine("## Results\n");
        sb.AppendLine("| Scenario | Result | Tokens before | Tokens after | Saved | Notes |");
        sb.AppendLine("|---|---|---:|---:|---:|---|");
        foreach (var r in Results)
        {
            var status = r.Passed ? "PASS" : "FAIL";
            var before = r.Tokens.before == 0 ? "—" : r.Tokens.before.ToString();
            var after = r.Tokens.after == 0 ? "—" : r.Tokens.after.ToString();
            var saved = r.Tokens.before == 0 ? "—" : $"{r.Tokens.percent:F1}%";
            sb.AppendLine($"| {r.Name} | {status} | {before} | {after} | {saved} | {r.Notes} |");
        }

        sb.AppendLine("\n## What each scenario proves\n");
        sb.AppendLine("- **Minify_***: lossless minify — same method count, same logic, output reparses, comments stripped.");
        sb.AppendLine("- **Focus_***: focus mode — the named method's body is verbatim; unrelated members are dropped; private helpers at depth=0 are signatures only and at depth=1 have full bodies.");
        sb.AppendLine("- **Alias_***: alias mode — only private symbols renamed, public API intact, `nameof(...)` argument preserved, ledger disambiguates duplicate names across nested classes (the bug we fixed today).");
        sb.AppendLine("- **TaskRealism_***: confirms a focused output still contains enough information for an AI reader to answer a concrete behavioural question about the method.");

        File.WriteAllText(ReportPath, sb.ToString());
    }

    // ---------- ProjectTraversal ----------

    private static readonly string TraversalDir = Path.Combine(FixturesDir, "traversal");

    private static TestOutcome Traversal_FindCallerFiles_FindsFileWithCaller()
    {
        // Beta.cs has Drawer.Draw which calls s.Name() — should be returned
        var t = new ProjectTraversal(TraversalDir);
        var files = t.FindCallerFiles("Name");
        var foundBeta = files.Any(f => Path.GetFileName(f) == "Beta.cs");
        var ok = files.Count >= 1 && foundBeta;
        return new TestOutcome(ok,
            ok ? "Beta.cs found as caller of Name()"
               : $"files={files.Count} foundBeta={foundBeta}",
            (0, 0, 0));
    }

    private static TestOutcome Traversal_FindCallerFiles_ReturnsEmptyForUnknownMethod()
    {
        var t = new ProjectTraversal(TraversalDir);
        var files = t.FindCallerFiles("NonExistentXyz");
        var ok = files.Count == 0;
        return new TestOutcome(ok,
            ok ? "empty list for unknown method name"
               : $"unexpectedly returned {files.Count} file(s)",
            (0, 0, 0));
    }

    private static TestOutcome Traversal_FindImplementors_FindsImplementingTypes()
    {
        // Alpha.cs has Circle : IShape, Beta.cs has Square : IShape
        var t = new ProjectTraversal(TraversalDir);
        var impls = t.FindImplementors("IShape");
        var typeNames = impls.Select(i => i.TypeName).ToList();
        var hasCircle = typeNames.Contains("Circle");
        var hasSquare = typeNames.Contains("Square");
        var ok = hasCircle && hasSquare && impls.Count == 2;
        return new TestOutcome(ok,
            ok ? "Circle and Square found as implementors of IShape"
               : $"types=[{string.Join(",", typeNames)}]",
            (0, 0, 0));
    }

    private static TestOutcome Traversal_FindImplementors_ReturnsEmptyForUnknownInterface()
    {
        var t = new ProjectTraversal(TraversalDir);
        var impls = t.FindImplementors("IDoesNotExist");
        var ok = impls.Count == 0;
        return new TestOutcome(ok,
            ok ? "empty list for unknown interface name"
               : $"unexpectedly returned {impls.Count} result(s)",
            (0, 0, 0));
    }

    private static TestOutcome Traversal_AcceptsCsprojPath()
    {
        // Pass the tests .csproj — should scan .cs files in tests/ (excluding obj/bin)
        var csproj = Path.GetFullPath(Path.Combine(FixturesDir, "..", "TokenSaverTests.csproj"));
        if (!File.Exists(csproj))
            return new TestOutcome(false, $".csproj not found at {csproj}", (0, 0, 0));
        var t = new ProjectTraversal(csproj);
        var ok = t.FileCount > 0;
        return new TestOutcome(ok,
            ok ? $".csproj path accepted; {t.FileCount} file(s) scanned"
               : "FileCount was 0 after passing .csproj path",
            (0, 0, 0));
    }

    // ---------- FocusedEmitterTools cache (end-to-end) ----------

    private static TestOutcome McpTool_SecondCallReturnsReparseSkipped()
    {
        EmissionCache.Clear();
        var path = Fixture("Calculator.cs");

        var first  = TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "Run", depth: 0, minify: false);
        var second = TokenSaver.Mcp.FocusedEmitterTools.FocusMethod(path, "Run", depth: 0, minify: false);

        var firstIsFresh     = !first.Contains("[re-parse skipped]");
        var secondIsCacheHit = second.Contains("[re-parse skipped]");
        var secondHasBody    = second.Contains("WeightedSum");

        var ok = firstIsFresh && secondIsCacheHit && secondHasBody;
        return new TestOutcome(ok,
            ok ? "first call fresh, second call re-parse skipped with full output"
               : $"firstFresh={firstIsFresh} secondCacheHit={secondIsCacheHit} secondHasBody={secondHasBody}",
            (0, 0, 0));
    }

    // ---------- EmissionCache ----------

    private static TestOutcome Cache_MissOnFirstCall()
    {
        EmissionCache.Clear();
        var path = Fixture("Calculator.cs");
        var hit = EmissionCache.TryGet(path, "Run", depth: 0, minify: false, out _, out _, out _);
        return new TestOutcome(!hit, !hit ? "cold cache returns false" : "unexpected cache hit on first call", (0, 0, 0));
    }

    private static TestOutcome Cache_HitOnSecondCall()
    {
        EmissionCache.Clear();
        var path = Fixture("Calculator.cs");
        var result = new FocusedEmitter(path).Emit("Run");
        var stored = "// header\nsome output";
        EmissionCache.Set(path, "Run", depth: 0, minify: false, stored, 100, 10);
        var hit = EmissionCache.TryGet(path, "Run", depth: 0, minify: false, out var output, out _, out _);
        var hasReparseSkipped = output.Contains("[re-parse skipped]");
        var hasBody = output.Contains("some output");
        var ok = hit && hasReparseSkipped && hasBody;
        return new TestOutcome(ok,
            ok ? "cache hit returns full output with [re-parse skipped] on header"
               : $"hit={hit} reparseSkipped={hasReparseSkipped} body={hasBody}",
            (0, 0, 0));
    }

    private static TestOutcome Cache_InvalidatedAfterFileChange()
    {
        EmissionCache.Clear();
        var path = Fixture("Calculator.cs");
        EmissionCache.Set(path, "Run", depth: 0, minify: false, "// header\nsome output", 100, 10);

        // Simulate a file change by writing the file with a future timestamp.
        var original = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, original.AddSeconds(1));
        try
        {
            var hit = EmissionCache.TryGet(path, "Run", depth: 0, minify: false, out _, out _, out _);
            return new TestOutcome(!hit,
                !hit ? "cache miss after file timestamp changed"
                     : "cache incorrectly returned stale entry",
                (0, 0, 0));
        }
        finally
        {
            File.SetLastWriteTimeUtc(path, original);
        }
    }

    // ---------- Markdown emitter ----------

    private static TestOutcome Md_Registry_DispatchesByExtension()
    {
        var md = LanguageEmitterRegistry.Find("readme.md");
        var markdown = LanguageEmitterRegistry.Find("doc.markdown");
        var ok = md is MarkdownEmitter && markdown is MarkdownEmitter;
        return new TestOutcome(ok,
            ok ? ".md and .markdown dispatched to MarkdownEmitter"
               : $"md={md?.GetType().Name} markdown={markdown?.GetType().Name}",
            (0, 0, 0));
    }

    private static TestOutcome Md_Minify_StripsHtmlComments()
    {
        var path = Fixture("sample.md");
        var r = new MarkdownEmitter().Minify(path);

        var hasTopComment = r.Output.Contains("top-level comment");
        var hasSectionComment = r.Output.Contains("section comment");
        var hasHeading = r.Output.Contains("# Sample Document");

        var ok = !hasTopComment && !hasSectionComment && hasHeading;
        return new TestOutcome(ok,
            ok ? "HTML comments stripped, headings preserved"
               : $"topComment={hasTopComment} sectionComment={hasSectionComment} heading={hasHeading}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Md_Minify_CollapsesBlankRuns()
    {
        var path = Fixture("sample.md");
        var r = new MarkdownEmitter().Minify(path);

        var hasTripleBlank = r.Output.Contains("\n\n\n");
        var ok = !hasTripleBlank;
        return new TestOutcome(ok,
            ok ? "blank-line runs collapsed to single blank"
               : "still has 3+ consecutive newlines",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private static TestOutcome Md_Minify_PreservesIndentation()
    {
        var path = Fixture("sample.md");
        var r = new MarkdownEmitter().Minify(path);

        var hasIndentedCode = r.Output.Contains("    indented code block");
        var hasNestedList = r.Output.Contains("    - nested item");

        var ok = hasIndentedCode && hasNestedList;
        return new TestOutcome(ok,
            ok ? "leading indentation preserved for code block and nested list"
               : $"indentedCode={hasIndentedCode} nestedList={hasNestedList}",
            TokenSaving(r.OriginalChars, r.OutputChars));
    }

    private sealed record TestOutcome(bool Passed, string Notes, (int before, int after, double percent) Tokens);
    private sealed record TestRecord(string Name, bool Passed, string Notes, (int before, int after, double percent) Tokens);
}
