using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System.Text;

namespace RoslynLean;

public sealed class VBFocusedEmitter
{
    private readonly string _filePath;
    private readonly SyntaxTree _tree;
    private readonly CompilationUnitSyntax _root;
    private readonly int _originalChars;
    private VisualBasicCompilation? _compilation;
    private SemanticModel? _model;

    private SemanticModel Model
    {
        get
        {
            if (_model is not null) return _model;
            var refs = GetDefaultReferences()
                .Where(File.Exists)
                .Select(p => MetadataReference.CreateFromFile(p))
                .ToArray();
            _compilation = VisualBasicCompilation.Create(
                "VBLeanScratch",
                [_tree],
                refs,
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            _model = _compilation.GetSemanticModel(_tree);
            return _model;
        }
    }

    public VBFocusedEmitter(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);
        _filePath = filePath;
        var source = File.ReadAllText(filePath);
        _originalChars = source.Length;
        _tree = VisualBasicSyntaxTree.ParseText(source, path: filePath);
        _root = (CompilationUnitSyntax)_tree.GetRoot();
    }

    public FocusResult EmitMinified()
    {
        var stripped = new CommentStripper().Visit(_root)!;
        var output = CollapseBlankRuns(stripped.ToFullString());
        var notes = $"' Minified emission of {Path.GetFileName(_filePath)}\n" +
                    $"' Comments stripped, blank runs collapsed — logic preserved verbatim\n";
        return new FocusResult(true, output, _originalChars, output.Length, "(minified)", notes);
    }

    public FocusResult EmitOutline()
    {
        var sb = new StringBuilder();
        AppendImports(sb);
        int typeCount = 0, memberCount = 0;
        foreach (var typeBlock in _root.DescendantNodes().OfType<TypeBlockSyntax>())
        {
            if (typeBlock.Parent is TypeBlockSyntax) continue;
            AppendTypeOutline(sb, typeBlock, ref typeCount, ref memberCount, "");
            sb.AppendLine();
        }
        var output = sb.ToString().TrimEnd() + "\n";
        var notes = $"' Outline of {Path.GetFileName(_filePath)}\n" +
                    $"' {typeCount} type(s), {memberCount} member(s) — signatures only, no bodies\n";
        return new FocusResult(true, output, _originalChars, output.Length, "(outline)", notes);
    }

    public FocusResult Emit(string focusMethodName, int depth = 0)
    {
        var focusMethods = FindMethods(focusMethodName);
        if (focusMethods.Count == 0)
            return FocusResult.NotFound(focusMethodName);
        var containingType = GetContainingType(focusMethods[0]);
        if (containingType is null)
            return FocusResult.NotFound($"{focusMethodName} (no containing type)");
        var referencedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var m in focusMethods)
            CollectReferencedSymbols(m, referencedSymbols);
        var expandedMethods = ExpandHelpers(focusMethods, depth);
        var sb = new StringBuilder();
        var relevant = new StringBuilder();
        AppendImports(sb);
        AppendNamespaceOpen(sb, containingType);
        AppendTypeWithFocus(sb, containingType, focusMethods, referencedSymbols, expandedMethods, relevant);
        AppendNamespaceClose(sb, containingType);
        var output = sb.ToString();
        var notes = BuildNotes(focusMethods.Count, referencedSymbols.Count, containingType, expandedMethods.Count, depth);
        return new FocusResult(true, output, _originalChars, output.Length, focusMethodName, notes)
        {
            RelevantSourceText = relevant.ToString()
        };
    }

    public FocusResult EmitMultiple(IReadOnlyList<string> methodNames, int depth = 0)
    {
        var nameSet = new HashSet<string>(methodNames, StringComparer.OrdinalIgnoreCase);
        var allFocusMethods = _root.DescendantNodes()
            .Where(n =>
                (n is MethodBlockSyntax mb && nameSet.Contains(mb.SubOrFunctionStatement.Identifier.Text)) ||
                (n is ConstructorBlockSyntax && nameSet.Contains("New")))
            .Cast<StatementSyntax>()
            .Distinct()
            .ToList();
        if (allFocusMethods.Count == 0)
            return FocusResult.NotFound(string.Join(", ", methodNames));
        var referencedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var m in allFocusMethods)
            CollectReferencedSymbols(m, referencedSymbols);
        var expandedMethods = ExpandHelpers(allFocusMethods, depth);
        var byType = allFocusMethods
            .GroupBy(GetContainingType)
            .Where(g => g.Key is not null)
            .ToList();
        var sb = new StringBuilder();
        var relevant = new StringBuilder();
        AppendImports(sb);
        foreach (var group in byType)
        {
            AppendNamespaceOpen(sb, group.Key!);
            AppendTypeWithFocus(sb, group.Key!, group.ToList(), referencedSymbols, expandedMethods, relevant);
            AppendNamespaceClose(sb, group.Key!);
            sb.AppendLine();
        }
        var foundNames = allFocusMethods.Select(GetMemberName).OfType<string>().Distinct().ToList();
        var notFound = methodNames.Where(n => !foundNames.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
        if (notFound.Count > 0)
            sb.AppendLine($"' NOT FOUND: {string.Join(", ", notFound)}");
        var output = sb.ToString();
        var notesBuilder = new StringBuilder();
        notesBuilder.AppendLine($"' Focused emission of {Path.GetFileName(_filePath)}");
        notesBuilder.AppendLine($"' Focus method(s): {allFocusMethods.Count} with full body ({string.Join(", ", foundNames)})");
        if (notFound.Count > 0) notesBuilder.AppendLine($"' Not found: {string.Join(", ", notFound)}");
        if (depth >= 1) notesBuilder.AppendLine($"' Expanded helpers (depth {depth}): {expandedMethods.Count}");
        notesBuilder.AppendLine($"' Other members: {referencedSymbols.Count} symbols referenced, signatures only");
        return new FocusResult(true, output, _originalChars, output.Length, string.Join(", ", methodNames), notesBuilder.ToString())
        {
            RelevantSourceText = relevant.ToString()
        };
    }

    public FocusResult EmitType(string typeName)
    {
        var targetType = _root.DescendantNodes().OfType<TypeBlockSyntax>()
            .FirstOrDefault(t => GetTypeName(t) == typeName);
        if (targetType is null)
            return FocusResult.NotFound(typeName);
        var sb = new StringBuilder();
        AppendImports(sb);
        AppendNamespaceOpen(sb, targetType);
        sb.AppendLine(targetType.BlockStatement.ToString().Trim());
        var relevant = new StringBuilder();
        int fullBodyCount = 0, sigOnlyCount = 0;
        foreach (var member in targetType.Members)
        {
            if (member is TypeBlockSyntax nested)
            {
                sb.AppendLine($"    ' nested {GetTypeKind(nested)} {GetTypeName(nested)} — use FocusType to inspect");
                continue;
            }
            if (IsPrivateMember(member))
            {
                var sig = ToSignature(member);
                if (sig is not null) { sb.AppendLine($"    {sig}"); sigOnlyCount++; }
            }
            else
            {
                sb.AppendLine(IndentLines(member.ToFullString().Trim(), "    "));
                sb.AppendLine();
                relevant.AppendLine(member.ToFullString().Trim());
                fullBodyCount++;
            }
        }
        sb.AppendLine(targetType.EndBlockStatement.ToString().Trim());
        AppendNamespaceClose(sb, targetType);
        var output = sb.ToString();
        var notes = $"' Type focus: {typeName} in {Path.GetFileName(_filePath)}\n" +
                    $"' {fullBodyCount} non-private member(s) with full body; {sigOnlyCount} private member(s) as signatures\n";
        return new FocusResult(true, output, _originalChars, output.Length, $"(type:{typeName})", notes)
        {
            RelevantSourceText = relevant.ToString()
        };
    }

    public FocusResult EmitCallers(string targetMethodName, int depth = 0)
    {
        var callerNames = _root.DescendantNodes().OfType<MethodBlockSyntax>()
            .Where(m =>
                m.SubOrFunctionStatement.Identifier.Text != targetMethodName &&
                m.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(inv => GetInvokedName(inv) == targetMethodName))
            .Select(m => m.SubOrFunctionStatement.Identifier.Text)
            .Distinct()
            .ToList();
        if (callerNames.Count == 0)
            return new FocusResult(false, $"' No callers of '{targetMethodName}' found in source",
                _originalChars, 0, $"(callers:{targetMethodName})", "");
        var result = EmitMultiple(callerNames, depth);
        var notes = $"' Callers of '{targetMethodName}' in {Path.GetFileName(_filePath)}\n" +
                    $"' {callerNames.Count} calling method(s): {string.Join(", ", callerNames)}\n";
        return result with { Notes = notes };
    }

    private void AppendTypeOutline(StringBuilder sb, TypeBlockSyntax typeBlock, ref int typeCount, ref int memberCount, string indent)
    {
        typeCount++;
        sb.AppendLine($"{indent}{typeBlock.BlockStatement.ToString().Trim()}");
        foreach (var member in typeBlock.Members)
        {
            if (member is TypeBlockSyntax nested)
            {
                AppendTypeOutline(sb, nested, ref typeCount, ref memberCount, indent + "    ");
                continue;
            }
            var sig = ToSignature(member);
            if (sig is null) continue;
            sb.AppendLine($"{indent}    {sig}");
            memberCount++;
        }
        sb.AppendLine($"{indent}{typeBlock.EndBlockStatement.ToString().Trim()}");
    }

    private List<StatementSyntax> FindMethods(string name) =>
        _root.DescendantNodes()
            .Where(n =>
                (n is MethodBlockSyntax mb && mb.SubOrFunctionStatement.Identifier.Text == name) ||
                (n is ConstructorBlockSyntax && name == "New"))
            .Cast<StatementSyntax>()
            .ToList();

    private HashSet<ISymbol> ExpandHelpers(List<StatementSyntax> focusMethods, int depth)
    {
        var expanded = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (depth < 1) return expanded;
        var frontier = new List<StatementSyntax>(focusMethods);
        for (int level = 0; level < depth; level++)
        {
            var nextFrontier = new List<StatementSyntax>();
            foreach (var method in frontier)
            {
                foreach (var inv in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var info = Model.GetSymbolInfo(inv);
                    var sym = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                    if (sym is not IMethodSymbol ms) continue;
                    if (ms.DeclaredAccessibility != Accessibility.Private) continue;
                    if (!expanded.Add(ms)) continue;
                    foreach (var decl in ms.DeclaringSyntaxReferences)
                    {
                        if (decl.GetSyntax() is MethodBlockSyntax mbs &&
                            mbs.SyntaxTree == _tree &&
                            !focusMethods.Contains(mbs))
                            nextFrontier.Add(mbs);
                    }
                }
            }
            if (nextFrontier.Count == 0) break;
            frontier = nextFrontier;
        }
        return expanded;
    }

    private void CollectReferencedSymbols(StatementSyntax member, HashSet<ISymbol> sink)
    {
        foreach (var node in member.DescendantNodes())
        {
            ISymbol? symbol = node switch
            {
                IdentifierNameSyntax id => Model.GetSymbolInfo(id).Symbol,
                MemberAccessExpressionSyntax m => Model.GetSymbolInfo(m).Symbol,
                InvocationExpressionSyntax inv => Model.GetSymbolInfo(inv).Symbol,
                ObjectCreationExpressionSyntax oc => Model.GetSymbolInfo(oc).Symbol,
                _ => null
            };
            if (symbol is null) continue;
            if (symbol.Kind is SymbolKind.Local or SymbolKind.Parameter or SymbolKind.Namespace) continue;
            sink.Add(symbol);
            if (symbol.ContainingType is { } ct) sink.Add(ct);
        }
    }

    private void AppendTypeWithFocus(
        StringBuilder sb,
        TypeBlockSyntax typeBlock,
        List<StatementSyntax> focusMethods,
        HashSet<ISymbol> referenced,
        HashSet<ISymbol> expandedMethods,
        StringBuilder? relevantSink = null)
    {
        sb.AppendLine(typeBlock.BlockStatement.ToString().Trim());
        foreach (var member in typeBlock.Members)
        {
            if (focusMethods.Contains(member))
            {
                sb.AppendLine(IndentLines(member.ToFullString().Trim(), "    "));
                sb.AppendLine();
                relevantSink?.AppendLine(member.ToFullString().Trim());
                continue;
            }
            ISymbol? memberSymbol = member switch
            {
                MethodBlockSyntax mb => Model.GetDeclaredSymbol(mb.SubOrFunctionStatement),
                ConstructorBlockSyntax cb => Model.GetDeclaredSymbol(cb.SubNewStatement),
                PropertyBlockSyntax pb => Model.GetDeclaredSymbol(pb.PropertyStatement),
                FieldDeclarationSyntax fd => fd.Declarators
                    .SelectMany(d => d.Names)
                    .Select(n => Model.GetDeclaredSymbol(n))
                    .FirstOrDefault(s => s is not null && referenced.Contains(s)),
                EventBlockSyntax eb => Model.GetDeclaredSymbol(eb.EventStatement),
                EventStatementSyntax es => Model.GetDeclaredSymbol(es),
                _ => null
            };
            if (memberSymbol is null) continue;
            if (expandedMethods.Contains(memberSymbol))
            {
                sb.AppendLine(IndentLines(member.ToFullString().Trim(), "    "));
                sb.AppendLine();
                relevantSink?.AppendLine(member.ToFullString().Trim());
                continue;
            }
            if (!referenced.Contains(memberSymbol)) continue;
            var sig = ToSignature(member);
            if (sig is not null)
                sb.AppendLine($"    {sig}");
        }
        sb.AppendLine(typeBlock.EndBlockStatement.ToString().Trim());
    }

    private void AppendImports(StringBuilder sb)
    {
        foreach (var imp in _root.Imports)
            sb.AppendLine(imp.ToString().Trim());
        if (_root.Imports.Any()) sb.AppendLine();
    }

    private static void AppendNamespaceOpen(StringBuilder sb, TypeBlockSyntax typeBlock)
    {
        var ns = typeBlock.Ancestors().OfType<NamespaceBlockSyntax>().FirstOrDefault();
        if (ns is not null)
        {
            sb.AppendLine(ns.NamespaceStatement.ToString().Trim());
            sb.AppendLine();
        }
    }

    private static void AppendNamespaceClose(StringBuilder sb, TypeBlockSyntax typeBlock)
    {
        var ns = typeBlock.Ancestors().OfType<NamespaceBlockSyntax>().FirstOrDefault();
        if (ns is not null)
            sb.AppendLine("End Namespace");
    }

    private static TypeBlockSyntax? GetContainingType(StatementSyntax member) =>
        member.Ancestors().OfType<TypeBlockSyntax>().FirstOrDefault();

    private static string? GetMemberName(StatementSyntax member) => member switch
    {
        MethodBlockSyntax mb => mb.SubOrFunctionStatement.Identifier.Text,
        ConstructorBlockSyntax => "New",
        PropertyBlockSyntax pb => pb.PropertyStatement.Identifier.Text,
        _ => null
    };

    private static SyntaxTokenList GetModifiers(StatementSyntax member) => member switch
    {
        MethodBlockSyntax mb => mb.SubOrFunctionStatement.Modifiers,
        ConstructorBlockSyntax cb => cb.SubNewStatement.Modifiers,
        PropertyBlockSyntax pb => pb.PropertyStatement.Modifiers,
        FieldDeclarationSyntax fd => fd.Modifiers,
        EventBlockSyntax eb => eb.EventStatement.Modifiers,
        EventStatementSyntax es => es.Modifiers,
        _ => default
    };

    private static bool IsPrivateMember(StatementSyntax member)
    {
        var mods = GetModifiers(member);
        if (mods.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))) return true;
        // Fields with no explicit access modifier are Private by default in VB classes
        return member is FieldDeclarationSyntax &&
               !mods.Any(m => m.IsKind(SyntaxKind.PublicKeyword) ||
                               m.IsKind(SyntaxKind.FriendKeyword) ||
                               m.IsKind(SyntaxKind.ProtectedKeyword));
    }

    private static string? ToSignature(StatementSyntax member) => member switch
    {
        MethodBlockSyntax mb => mb.SubOrFunctionStatement.ToString().Trim(),
        ConstructorBlockSyntax cb => cb.SubNewStatement.ToString().Trim(),
        PropertyBlockSyntax pb => pb.PropertyStatement.ToString().Trim(),
        FieldDeclarationSyntax fd => fd.ToString().Trim(),
        EventBlockSyntax eb => eb.EventStatement.ToString().Trim(),
        EventStatementSyntax es => es.ToString().Trim(),
        DelegateStatementSyntax ds => ds.ToString().Trim(),
        _ => null
    };

    private static string GetTypeName(TypeBlockSyntax typeBlock) => typeBlock switch
    {
        ClassBlockSyntax cb => cb.ClassStatement.Identifier.Text,
        ModuleBlockSyntax mb => mb.ModuleStatement.Identifier.Text,
        InterfaceBlockSyntax ib => ib.InterfaceStatement.Identifier.Text,
        StructureBlockSyntax sb => sb.StructureStatement.Identifier.Text,
        _ => "Unknown"
    };

    private static string GetTypeKind(TypeBlockSyntax typeBlock) => typeBlock switch
    {
        ClassBlockSyntax => "Class",
        ModuleBlockSyntax => "Module",
        InterfaceBlockSyntax => "Interface",
        StructureBlockSyntax => "Structure",
        _ => "Type"
    };

    private static string? GetInvokedName(InvocationExpressionSyntax inv) => inv.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax mae => mae.Name.Identifier.Text,
        _ => null
    };

    private static string IndentLines(string text, string indent)
    {
        var sb = new StringBuilder();
        foreach (var line in text.Split('\n'))
            sb.AppendLine(indent + line.TrimEnd('\r'));
        return sb.ToString().TrimEnd();
    }

    private string BuildNotes(int focusCount, int refCount, TypeBlockSyntax type, int expandedCount, int depth)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"' Focused emission of {Path.GetFileName(_filePath)}");
        sb.AppendLine($"' Focus method(s): {focusCount} overload(s) with full body");
        if (depth >= 1) sb.AppendLine($"' Expanded helpers (depth {depth}): {expandedCount} private member(s)");
        sb.AppendLine($"' Other members: {refCount} symbols referenced, signatures only");
        sb.AppendLine($"' Containing type: {GetTypeName(type)}");
        return sb.ToString();
    }

    internal static string MinifyText(string source)
    {
        var tree = VisualBasicSyntaxTree.ParseText(source);
        var stripped = new CommentStripper().Visit(tree.GetRoot())!;
        return CollapseBlankRuns(stripped.ToFullString());
    }

    internal static string CollapseBlankRuns(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool prevBlank = false, atStart = true;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r', ' ', '\t');
            var isBlank = line.Length == 0;
            if (isBlank)
            {
                if (prevBlank || atStart) continue;
                sb.Append('\n');
                prevBlank = true;
                continue;
            }
            sb.Append(line);
            sb.Append('\n');
            prevBlank = false;
            atStart = false;
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static IEnumerable<string> GetDefaultReferences()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return Directory.GetFiles(runtimeDir, "*.dll");
    }

    private sealed class CommentStripper : VisualBasicSyntaxRewriter
    {
        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(SyntaxKind.CommentTrivia)) return default;
            return trivia;
        }
    }
}
