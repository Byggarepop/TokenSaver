using System.Text;
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
        Run("Focus_NotFound_ReturnsNotFoundResult", Focus_NotFound_ReturnsNotFoundResult);
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
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "FocusedEmitter.cs");
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
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "FocusedEmitter.cs");
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

    // ---------- helpers ----------

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

    private sealed record TestOutcome(bool Passed, string Notes, (int before, int after, double percent) Tokens);
    private sealed record TestRecord(string Name, bool Passed, string Notes, (int before, int after, double percent) Tokens);
}
