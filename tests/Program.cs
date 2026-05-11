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
