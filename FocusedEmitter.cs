using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLean;

/// <summary>
/// Given a focus method, emit only the slice of the file an LLM needs
/// to reason about it: the method itself, plus the SIGNATURES of anything
/// it references (other methods, properties, fields, types).
/// 
/// Method bodies of called methods are excluded — only signatures.
/// This is the single biggest token saver: a 500-line service class
/// that has 30 methods and the user is asking about one of them
/// becomes ~80 lines instead of 500.
/// 
/// We use Roslyn's SemanticModel for symbol resolution rather than
/// just text matching, which means we correctly handle:
/// - Overloads (we pick the right one)
/// - Generics
/// - Inherited members from base classes
/// - Extension methods
/// - Namespace-qualified calls
/// </summary>
public sealed class FocusedEmitter
{
    private readonly SyntaxTree _tree;
    private readonly IEnumerable<string>? _referenceAssemblyPaths;
    private Compilation? _compilation;
    private SemanticModel? _model;

    /// <summary>
    /// True once the semantic model has been initialised.
    /// EmitOutline and EmitMinified never set this; Emit, EmitMultiple, and EmitAliased do.
    /// </summary>
    public bool IsModelLoaded => _model is not null;

    /// <summary>
    /// Lazily builds the Roslyn compilation and semantic model on first access.
    /// Only Emit, EmitMultiple, and EmitAliased need symbol resolution — outline
    /// and minify work purely on the syntax tree, so they skip this cost entirely.
    /// </summary>
    private SemanticModel Model
    {
        get
        {
            if (_model is not null) return _model;

            // Build a minimal compilation. For real use, you'd load the .csproj via
            // MSBuildWorkspace so all references are available. For a CLI that just
            // takes a single file, we use a stub set of references — this means
            // some symbol resolution may fail (e.g., types from project references),
            // but in-file resolution always works.
            var references = (_referenceAssemblyPaths ?? GetDefaultReferences())
                .Where(File.Exists)
                .Select(p => MetadataReference.CreateFromFile(p))
                .ToArray();

            _compilation = CSharpCompilation.Create(
                assemblyName: "RoslynLeanScratch",
                syntaxTrees: [_tree],
                references: references);

            _model = _compilation.GetSemanticModel(_tree);
            return _model;
        }
    }

    public FocusedEmitter(string sourceFilePath, IEnumerable<string>? referenceAssemblyPaths = null)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Source file not found", sourceFilePath);

        var source = File.ReadAllText(sourceFilePath);
        if (RazorPreprocessor.IsRazor(sourceFilePath))
            source = RazorPreprocessor.ExtractCSharp(source);
        _tree = CSharpSyntaxTree.ParseText(source, path: sourceFilePath);
        _referenceAssemblyPaths = referenceAssemblyPaths;
    }

    /// <summary>
    /// Emit a focused view of the source containing:
    /// - The focus method, in full
    /// - Containing type's declaration (signature only — fields/props/other methods reduced to signatures)
    /// - Any types referenced from the focus method's body (signatures only)
    /// - Necessary namespace and using context
    /// </summary>
    public FocusResult Emit(string focusMethodName, int depth = 0)
    {
        var root = _tree.GetCompilationUnitRoot();

        // 1) Find the focus member. Matches methods AND constructors so that callers
        //    can focus on a constructor by passing the class name as focusMethodName.
        var focusMethods = root.DescendantNodes()
            .Where(n => (n is MethodDeclarationSyntax m && m.Identifier.Text == focusMethodName)
                     || (n is ConstructorDeclarationSyntax c && c.Identifier.Text == focusMethodName))
            .Cast<MemberDeclarationSyntax>()
            .ToList();

        if (focusMethods.Count == 0)
            return FocusResult.NotFound(focusMethodName);

        // 2) Find which type contains them.
        // Multiple matches across types isn't supported in v1; take the first.
        var containingType = focusMethods[0].FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is null)
            return FocusResult.NotFound($"{focusMethodName} (no containing type)");

        // 3) Walk the focus methods to find every symbol they reference.
        var referencedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var method in focusMethods)
            CollectReferencedSymbols(method, referencedSymbols);

        // 3b) Optionally expand: collect private methods invoked transitively
        //     up to `depth` levels. The AI sees the actual logic of helpers
        //     instead of guessing from a signature.
        var expandedMethods = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (depth >= 1)
        {
            var frontier = new List<MemberDeclarationSyntax>(focusMethods);
            for (int level = 0; level < depth; level++)
            {
                var nextFrontier = new List<MemberDeclarationSyntax>();
                foreach (var method in frontier)
                {
                    foreach (var inv in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        var info = Model.GetSymbolInfo(inv);
                        var sym = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                        if (sym is not IMethodSymbol ms) continue;
                        if (ms.DeclaredAccessibility != Accessibility.Private) continue;
                        if (!expandedMethods.Add(ms)) continue;

                        // Find the syntax for this private method to recurse into next level.
                        foreach (var decl in ms.DeclaringSyntaxReferences)
                        {
                            if (decl.GetSyntax() is MethodDeclarationSyntax mds &&
                                mds.SyntaxTree == _tree &&
                                !focusMethods.Contains(mds))
                            {
                                nextFrontier.Add(mds);
                            }
                        }
                    }

                    // Also expand private property bodies accessed in this method.
                    // Added to expandedMethods but not the frontier — we don't recurse
                    // into getter/setter logic the way we do for helper methods.
                    foreach (var node in method.DescendantNodes())
                    {
                        ISymbol? propertySym = node switch
                        {
                            MemberAccessExpressionSyntax mae => Model.GetSymbolInfo(mae).Symbol,
                            IdentifierNameSyntax id when id.Parent is not MemberAccessExpressionSyntax
                                => Model.GetSymbolInfo(id).Symbol,
                            _ => null
                        };
                        if (propertySym is not IPropertySymbol ps) continue;
                        if (ps.DeclaredAccessibility != Accessibility.Private) continue;
                        expandedMethods.Add(ps);
                    }
                }
                if (nextFrontier.Count == 0) break;
                frontier = nextFrontier;
            }
        }

        // 4) Build the output. Order: usings, namespace, type with focus method
        //    full-bodied + expanded helpers full-bodied + everything else reduced to signatures.
        var sb = new StringBuilder();
        AppendUsings(sb, root);
        AppendNamespaceOpen(sb, containingType);
        AppendTypeWithFocus(sb, containingType, focusMethods, referencedSymbols, expandedMethods);
        AppendNamespaceClose(sb, containingType);

        var output = sb.ToString();
        var originalLength = _tree.GetText().Length;

        return new FocusResult(
            Found: true,
            Output: output,
            OriginalChars: originalLength,
            FocusedChars: output.Length,
            FocusMethodName: focusMethodName,
            Notes: BuildNotes(focusMethods.Count, referencedSymbols.Count, containingType, expandedMethods.Count, depth));
    }

    /// <summary>
    /// Same as Emit but focuses on several named methods in one pass.
    /// The file is parsed once; referenced signatures are deduplicated across
    /// all focus methods, so the combined output is smaller than calling
    /// Emit N times and the caller saves N-1 MCP round-trips.
    /// </summary>
    public FocusResult EmitMultiple(IReadOnlyList<string> methodNames, int depth = 0)
    {
        var nameSet = new HashSet<string>(methodNames, StringComparer.Ordinal);
        var root = _tree.GetCompilationUnitRoot();

        var allFocusMethods = root.DescendantNodes()
            .Where(n => (n is MethodDeclarationSyntax m && nameSet.Contains(m.Identifier.Text))
                     || (n is ConstructorDeclarationSyntax c && nameSet.Contains(c.Identifier.Text)))
            .Cast<MemberDeclarationSyntax>()
            .ToList();

        if (allFocusMethods.Count == 0)
            return FocusResult.NotFound(string.Join(", ", methodNames));

        // Collect referenced symbols across ALL focus methods (union).
        var referencedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var method in allFocusMethods)
            CollectReferencedSymbols(method, referencedSymbols);

        // Depth expansion across all focus methods combined.
        var expandedMethods = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (depth >= 1)
        {
            var frontier = new List<MemberDeclarationSyntax>(allFocusMethods);
            for (int level = 0; level < depth; level++)
            {
                var nextFrontier = new List<MemberDeclarationSyntax>();
                foreach (var method in frontier)
                {
                    foreach (var inv in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        var info = Model.GetSymbolInfo(inv);
                        var sym = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                        if (sym is not IMethodSymbol ms) continue;
                        if (ms.DeclaredAccessibility != Accessibility.Private) continue;
                        if (!expandedMethods.Add(ms)) continue;
                        foreach (var decl in ms.DeclaringSyntaxReferences)
                        {
                            if (decl.GetSyntax() is MethodDeclarationSyntax mds &&
                                mds.SyntaxTree == _tree &&
                                !allFocusMethods.Contains(mds))
                                nextFrontier.Add(mds);
                        }
                    }

                    // Also expand private property bodies accessed in this method.
                    foreach (var node in method.DescendantNodes())
                    {
                        ISymbol? propertySym = node switch
                        {
                            MemberAccessExpressionSyntax mae => Model.GetSymbolInfo(mae).Symbol,
                            IdentifierNameSyntax id when id.Parent is not MemberAccessExpressionSyntax
                                => Model.GetSymbolInfo(id).Symbol,
                            _ => null
                        };
                        if (propertySym is not IPropertySymbol ps) continue;
                        if (ps.DeclaredAccessibility != Accessibility.Private) continue;
                        expandedMethods.Add(ps);
                    }
                }
                if (nextFrontier.Count == 0) break;
                frontier = nextFrontier;
            }
        }

        // Group by containing type so each type is emitted once.
        var byType = allFocusMethods
            .GroupBy(m => m.FirstAncestorOrSelf<TypeDeclarationSyntax>(),
                     (type, methods) => (type, methods: methods.ToList()))
            .Where(g => g.type is not null)
            .ToList();

        var sb = new StringBuilder();
        AppendUsings(sb, root);
        foreach (var (type, methods) in byType)
        {
            AppendNamespaceOpen(sb, type!);
            AppendTypeWithFocus(sb, type!, methods, referencedSymbols, expandedMethods);
            AppendNamespaceClose(sb, type!);
            sb.AppendLine();
        }

        var foundNames = allFocusMethods.Select(GetMemberName).Distinct().ToList();
        var notFound = methodNames.Where(n => !foundNames.Contains(n)).ToList();
        if (notFound.Count > 0)
            sb.AppendLine($"// NOT FOUND: {string.Join(", ", notFound)}");

        var output = sb.ToString();
        var originalLength = _tree.GetText().Length;

        var notes = new StringBuilder();
        notes.AppendLine($"// Focused emission of {Path.GetFileName(_tree.FilePath)}");
        notes.AppendLine($"// Focus method(s): {allFocusMethods.Count} method(s) with full body ({string.Join(", ", foundNames)})");
        if (notFound.Count > 0)
            notes.AppendLine($"// Not found: {string.Join(", ", notFound)}");
        if (depth >= 1)
            notes.AppendLine($"// Expanded helpers (depth {depth}): {expandedMethods.Count} private member(s) with full body");
        notes.AppendLine($"// Other members: {referencedSymbols.Count} symbols referenced, signatures only");

        return new FocusResult(
            Found: true,
            Output: output,
            OriginalChars: originalLength,
            FocusedChars: output.Length,
            FocusMethodName: string.Join(", ", methodNames),
            Notes: notes.ToString());
    }

    /// <summary>
    /// Emit a focused view of a named type: non-private members with full bodies,
    /// private members as signatures only. Sits between Outline (all signatures)
    /// and MinifyCSharpFile (everything), and is especially useful when a file
    /// contains multiple types and you only need one, or when you want to skip
    /// private implementation noise.
    /// </summary>
    public FocusResult EmitType(string typeName)
    {
        var root = _tree.GetCompilationUnitRoot();

        var targetType = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == typeName);

        if (targetType is null)
            return FocusResult.NotFound(typeName);

        var sb = new StringBuilder();
        AppendUsings(sb, root);
        AppendNamespaceOpen(sb, targetType);

        var modifiers = string.Join(" ", targetType.Modifiers.Select(m => m.Text));
        var kind = targetType.Keyword.Text;
        var baseList = targetType.BaseList?.ToString() ?? "";
        sb.AppendLine($"{modifiers} {kind} {targetType.Identifier}{targetType.TypeParameterList} {baseList}".Trim());
        sb.AppendLine("{");

        int fullBodyCount = 0, sigOnlyCount = 0;
        foreach (var member in targetType.Members)
        {
            if (member is TypeDeclarationSyntax nested)
            {
                sb.AppendLine($"    // nested {nested.Keyword.Text} {nested.Identifier} — use FocusType to inspect");
                continue;
            }

            if (IsPrivate(member))
            {
                var sig = ToSignature(member);
                if (sig is not null)
                {
                    sb.AppendLine($"    {sig}");
                    sigOnlyCount++;
                }
            }
            else
            {
                sb.AppendLine(IndentLines(member.ToFullString().Trim(), "    "));
                sb.AppendLine();
                fullBodyCount++;
            }
        }

        sb.AppendLine("}");
        AppendNamespaceClose(sb, targetType);

        var output = sb.ToString();
        var originalLength = _tree.GetText().Length;
        var notes =
            $"// Type focus: {typeName} in {Path.GetFileName(_tree.FilePath)}\n" +
            $"// {fullBodyCount} non-private member(s) with full body; {sigOnlyCount} private member(s) as signatures\n";

        return new FocusResult(
            Found: true,
            Output: output,
            OriginalChars: originalLength,
            FocusedChars: output.Length,
            FocusMethodName: $"(type:{typeName})",
            Notes: notes);
    }

    /// <summary>
    /// Find all methods in the file that directly invoke <paramref name="targetMethodName"/>,
    /// then emit them as a focused multi-method view. Answers "what calls X?" without
    /// loading the whole file. Uses name matching rather than full semantic resolution,
    /// so it may miss calls routed through delegates or interfaces.
    /// </summary>
    public FocusResult EmitCallers(string targetMethodName, int depth = 0)
    {
        var root = _tree.GetCompilationUnitRoot();

        var callerNames = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text != targetMethodName
                     && m.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(inv =>
                            (inv.Expression is IdentifierNameSyntax id
                             && id.Identifier.Text == targetMethodName)
                         || (inv.Expression is MemberAccessExpressionSyntax mae
                             && mae.Name.Identifier.Text == targetMethodName)))
            .Select(m => m.Identifier.Text)
            .Distinct()
            .ToList();

        if (callerNames.Count == 0)
            return new FocusResult(false,
                $"// No callers of '{targetMethodName}' found in source",
                _tree.GetText().Length, 0, $"(callers:{targetMethodName})", "");

        var result = EmitMultiple(callerNames, depth);
        var notes =
            $"// Callers of '{targetMethodName}' in {Path.GetFileName(_tree.FilePath)}\n" +
            $"// {callerNames.Count} calling method(s): {string.Join(", ", callerNames)}\n";

        return result with { Notes = notes };
    }

    /// <summary>
    /// Whole-file lossless minifier: strip every comment (XML doc, // and /* */),
    /// drop blank lines, collapse indentation. The output is functionally
    /// identical C# — Roslyn parses, rewrites, and re-emits it, so logic
    /// is guaranteed preserved. The cost is human readability; the win is
    /// 30-60% fewer tokens with zero semantic loss.
    /// </summary>
    public FocusResult EmitMinified()
    {
        var root = _tree.GetCompilationUnitRoot();
        var stripped = (CompilationUnitSyntax)new CommentStripper().Visit(root)!;
        var normalized = stripped.NormalizeWhitespace(indentation: "", eol: "\n");
        var output = normalized.ToFullString();
        var originalLength = _tree.GetText().Length;

        var notes = $"// Minified emission of {Path.GetFileName(_tree.FilePath)}\n" +
                    $"// Comments stripped, whitespace collapsed — logic preserved verbatim\n";

        return new FocusResult(
            Found: true,
            Output: output,
            OriginalChars: originalLength,
            FocusedChars: output.Length,
            FocusMethodName: "(minified)",
            Notes: notes);
    }

    /// <summary>
    /// Rename every PRIVATE method, property, field, and event to a short
    /// code (M1, P1, F1, E1...) and prepend a ledger so the LLM can map back.
    /// Public/internal/protected symbols are left alone — we can't see callers
    /// from another file, so renaming them risks breaking referenced code.
    /// Identifiers inside nameof(...) are also excluded.
    ///
    /// Composes with the minifier: comments are stripped and whitespace
    /// collapsed in the same pass, so this is the most aggressive lossless
    /// mode the CLI offers.
    /// </summary>
    /// <summary>
    /// Emit a skeleton of the file: every type and every member as a signature,
    /// no bodies. Useful for "what's in this file" navigation questions where
    /// the model doesn't need to read any specific implementation.
    /// </summary>
    public FocusResult EmitOutline()
    {
        var root = _tree.GetCompilationUnitRoot();
        var sb = new StringBuilder();

        AppendUsings(sb, root);

        var typeCount = 0;
        var memberCount = 0;
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            // Only top-level type declarations in the file get their own block;
            // nested types are emitted as part of their containing type's block.
            if (type.Parent is TypeDeclarationSyntax) continue;
            AppendTypeOutline(sb, type, ref typeCount, ref memberCount, indent: "");
            sb.AppendLine();
        }

        var output = sb.ToString().TrimEnd() + "\n";
        var originalLength = _tree.GetText().Length;
        var notes =
            $"// Outline of {Path.GetFileName(_tree.FilePath)}\n" +
            $"// {typeCount} type(s), {memberCount} member(s) — signatures only, no bodies\n";

        return new FocusResult(
            Found: true,
            Output: output,
            OriginalChars: originalLength,
            FocusedChars: output.Length,
            FocusMethodName: "(outline)",
            Notes: notes);
    }

    private void AppendTypeOutline(StringBuilder sb, TypeDeclarationSyntax type, ref int typeCount, ref int memberCount, string indent)
    {
        typeCount++;
        var modifiers = string.Join(" ", type.Modifiers.Select(m => m.Text));
        var kind = type.Keyword.Text;
        var baseList = type.BaseList?.ToString() ?? "";
        sb.AppendLine($"{indent}{modifiers} {kind} {type.Identifier}{type.TypeParameterList} {baseList}".TrimEnd().Replace("  ", " "));
        sb.AppendLine($"{indent}{{");
        foreach (var member in type.Members)
        {
            if (member is TypeDeclarationSyntax nested)
            {
                AppendTypeOutline(sb, nested, ref typeCount, ref memberCount, indent + "    ");
                continue;
            }
            var sig = ToSignature(member);
            if (sig is null) continue;
            sb.AppendLine($"{indent}    {sig}");
            memberCount++;
        }
        sb.AppendLine($"{indent}}}");
    }

    public FocusResult EmitAliased()
    {
        var root = _tree.GetCompilationUnitRoot();

        // 1) Collect rename targets: private members of types in this file.
        var renames = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        var ledger = new SymbolLedger();

        foreach (var node in root.DescendantNodes())
        {
            var declared = Model.GetDeclaredSymbol(node);
            if (declared is null) continue;
            if (declared.DeclaredAccessibility != Accessibility.Private) continue;

            var containing = declared.ContainingType?.Name;
            string? alias = declared switch
            {
                IMethodSymbol m when m.MethodKind == MethodKind.Ordinary => ledger.NewMethod(m.Name, containing),
                IPropertySymbol p => ledger.NewProperty(p.Name, containing),
                IFieldSymbol f when !f.IsImplicitlyDeclared => ledger.NewField(f.Name, containing),
                IEventSymbol e => ledger.NewEvent(e.Name, containing),
                _ => null
            };
            if (alias is not null)
                renames[declared] = alias;
        }

        if (renames.Count == 0)
        {
            // Nothing private to rename — fall back to plain minifier so the
            // user still gets a sensible result instead of an empty win.
            return EmitMinified();
        }

        // 2) Find identifiers inside nameof(...) to exclude from the rewrite.
        var nameofExcluded = new HashSet<SyntaxNode>();
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is IdentifierNameSyntax id && id.Identifier.Text == "nameof")
            {
                foreach (var inside in inv.ArgumentList.DescendantNodes())
                    nameofExcluded.Add(inside);
            }
        }

        // 3) Rewrite declarations and references.
        var aliased = (CompilationUnitSyntax)new AliasRewriter(Model, renames, nameofExcluded).Visit(root)!;

        // 4) Compose with the minifier (strip comments + normalize whitespace).
        var stripped = (CompilationUnitSyntax)new CommentStripper().Visit(aliased)!;
        var normalized = stripped.NormalizeWhitespace(indentation: "", eol: "\n");

        // 5) Prepend the ledger.
        var sb = new StringBuilder();
        sb.Append(ledger.ToCommentBlock());
        sb.Append(normalized.ToFullString());

        var output = sb.ToString();
        var originalLength = _tree.GetText().Length;

        var notes = $"// Aliased emission of {Path.GetFileName(_tree.FilePath)}\n" +
                    $"// {renames.Count} private symbols renamed; ledger inlined; logic preserved\n";

        return new FocusResult(
            Found: true,
            Output: output,
            OriginalChars: originalLength,
            FocusedChars: output.Length,
            FocusMethodName: "(aliased)",
            Notes: notes);
    }

    private sealed class SymbolLedger
    {
        private readonly List<Entry> _methods = new();
        private readonly List<Entry> _props = new();
        private readonly List<Entry> _fields = new();
        private readonly List<Entry> _events = new();

        private readonly record struct Entry(string Alias, string Original, string? Container);

        public string NewMethod(string name, string? container)   { var a = $"M{_methods.Count + 1}"; _methods.Add(new(a, name, container)); return a; }
        public string NewProperty(string name, string? container) { var a = $"P{_props.Count + 1}";   _props.Add(new(a, name, container));   return a; }
        public string NewField(string name, string? container)    { var a = $"F{_fields.Count + 1}";  _fields.Add(new(a, name, container));  return a; }
        public string NewEvent(string name, string? container)    { var a = $"E{_events.Count + 1}";  _events.Add(new(a, name, container));  return a; }

        public string ToCommentBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine("// === SYMBOL LEDGER (private members renamed) ===");
            if (_methods.Count > 0) sb.AppendLine($"// Methods:    {Format(_methods)}");
            if (_props.Count > 0)   sb.AppendLine($"// Properties: {Format(_props)}");
            if (_fields.Count > 0)  sb.AppendLine($"// Fields:     {Format(_fields)}");
            if (_events.Count > 0)  sb.AppendLine($"// Events:     {Format(_events)}");
            sb.AppendLine("// ===");
            return sb.ToString();

            string Format(List<Entry> list)
            {
                // Qualify the original name with its containing type only when a name appears
                // in more than one container. Avoids paying the qualifier cost when there's no ambiguity.
                var duplicateNames = list.GroupBy(e => e.Original)
                    .Where(g => g.Select(e => e.Container).Distinct().Count() > 1)
                    .Select(g => g.Key)
                    .ToHashSet();
                return string.Join(", ", list.Select(e =>
                    duplicateNames.Contains(e.Original) && e.Container is not null
                        ? $"{e.Alias}={e.Container}.{e.Original}"
                        : $"{e.Alias}={e.Original}"));
            }
        }
    }

    private sealed class AliasRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _model;
        private readonly Dictionary<ISymbol, string> _renames;
        private readonly HashSet<SyntaxNode> _excluded;
        // Name -> alias, for last-resort text matching when SemanticModel
        // can't bind the reference (common when external SDK types fail
        // to resolve against the stub reference set). Only populated for
        // names that are unambiguous within the rename set.
        private readonly Dictionary<string, string> _aliasByName;

        public AliasRewriter(SemanticModel model, Dictionary<ISymbol, string> renames, HashSet<SyntaxNode> excluded)
        {
            _model = model;
            _renames = renames;
            _excluded = excluded;

            _aliasByName = new Dictionary<string, string>();
            var collisions = new HashSet<string>();
            foreach (var (sym, alias) in renames)
            {
                if (collisions.Contains(sym.Name)) continue;
                if (_aliasByName.ContainsKey(sym.Name))
                {
                    _aliasByName.Remove(sym.Name);
                    collisions.Add(sym.Name);
                    continue;
                }
                _aliasByName[sym.Name] = alias;
            }
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (_excluded.Contains(node)) return base.VisitIdentifierName(node);

            // 1) Fully bound symbol — the happy path.
            var info = _model.GetSymbolInfo(node);
            var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            if (symbol is not null && _renames.TryGetValue(symbol, out var alias))
                return SyntaxFactory.IdentifierName(alias).WithTriviaFrom(node);

            // 2) Last-resort text match — only when the binder failed entirely
            //    AND the identifier isn't on the right side of a member access
            //    (e.g., `something.Foo` where Foo could be any external member).
            if (symbol is null && _aliasByName.TryGetValue(node.Identifier.Text, out var aliasByText))
            {
                if (node.Parent is MemberAccessExpressionSyntax mae && mae.Name == node)
                    return base.VisitIdentifierName(node);
                return SyntaxFactory.IdentifierName(aliasByText).WithTriviaFrom(node);
            }

            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var symbol = _model.GetDeclaredSymbol(node);
            var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
            if (symbol is not null && _renames.TryGetValue(symbol, out var alias))
                return visited.WithIdentifier(SyntaxFactory.Identifier(alias));
            return visited;
        }

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var symbol = _model.GetDeclaredSymbol(node);
            var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
            if (symbol is not null && _renames.TryGetValue(symbol, out var alias))
                return visited.WithIdentifier(SyntaxFactory.Identifier(alias));
            return visited;
        }

        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            var symbol = _model.GetDeclaredSymbol(node);
            var visited = (VariableDeclaratorSyntax)base.VisitVariableDeclarator(node)!;
            if (symbol is not null && _renames.TryGetValue(symbol, out var alias))
                return visited.WithIdentifier(SyntaxFactory.Identifier(alias));
            return visited;
        }
    }

    /// <summary>
    /// Apply the lossless minifier (strip comments, collapse whitespace) to
    /// any C# source text. Used to post-process focused-method output so
    /// `--method=` benefits from the same token-savings as `--csharpfile=`.
    /// </summary>
    public static string MinifyText(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var stripped = (CompilationUnitSyntax)new CommentStripper().Visit(root)!;
        return stripped.NormalizeWhitespace(indentation: "", eol: "\n").ToFullString();
    }

    private sealed class CommentStripper : CSharpSyntaxRewriter
    {
        // visitIntoStructuredTrivia: false so VisitTrivia receives the outer trivia wrapper
        // directly for all structured trivias (doc comments, #region, #endregion) and we can
        // remove them in one place by returning default. With visitIntoStructuredTrivia: true
        // the rewriter dispatches structured trivias as node visits, which can bypass VisitTrivia
        // for directive kinds (notably #endregion) and leave them in the output.
        public CommentStripper() : base(visitIntoStructuredTrivia: false) { }

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia) =>
            trivia.Kind() switch
            {
                SyntaxKind.SingleLineCommentTrivia => default,
                SyntaxKind.MultiLineCommentTrivia => default,
                SyntaxKind.SingleLineDocumentationCommentTrivia => default,
                SyntaxKind.MultiLineDocumentationCommentTrivia => default,
                SyntaxKind.RegionDirectiveTrivia => default,
                SyntaxKind.EndRegionDirectiveTrivia => default,
                // An orphaned #endregion (no matching #region in a focused/partial snippet)
                // is parsed as BadDirectiveTrivia — strip it too.
                SyntaxKind.BadDirectiveTrivia => default,
                _ => trivia
            };
    }

    // -------------------------- collection ---------------------------

    /// <summary>
    /// Walks a method body and records every symbol it touches. We use the
    /// SemanticModel to resolve identifiers to their declaring symbols — that's
    /// what makes this compiler-grade rather than regex-grade.
    /// </summary>
    private void CollectReferencedSymbols(MemberDeclarationSyntax method, HashSet<ISymbol> sink)
    {
        foreach (var node in method.DescendantNodes())
        {
            // Identifier references: variable names, type names, method calls
            ISymbol? symbol = node switch
            {
                IdentifierNameSyntax id      => Model.GetSymbolInfo(id).Symbol,
                MemberAccessExpressionSyntax m => Model.GetSymbolInfo(m).Symbol,
                InvocationExpressionSyntax inv => Model.GetSymbolInfo(inv).Symbol,
                ObjectCreationExpressionSyntax oc => Model.GetSymbolInfo(oc).Symbol,
                _ => null
            };

            if (symbol is null) continue;
            if (symbol.Kind == SymbolKind.Local || symbol.Kind == SymbolKind.Parameter) continue;
            if (symbol.Kind == SymbolKind.Namespace) continue;
            sink.Add(symbol);

            // Also include the symbol's containing type — if you call
            // Foo.Bar(), we want to know Foo exists.
            if (symbol.ContainingType is { } ct) sink.Add(ct);
        }
    }

    // -------------------------- emission ---------------------------

    private static void AppendUsings(StringBuilder sb, CompilationUnitSyntax root)
    {
        foreach (var u in root.Usings)
            sb.AppendLine(u.ToFullString().TrimEnd());
        if (root.Usings.Any()) sb.AppendLine();
    }

    private static void AppendNamespaceOpen(StringBuilder sb, TypeDeclarationSyntax type)
    {
        var ns = type.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        if (ns is null) return;
        if (ns is FileScopedNamespaceDeclarationSyntax fs)
            sb.AppendLine($"namespace {fs.Name};").AppendLine();
        else if (ns is NamespaceDeclarationSyntax bs)
            sb.AppendLine($"namespace {bs.Name}").AppendLine("{");
    }

    private static void AppendNamespaceClose(StringBuilder sb, TypeDeclarationSyntax type)
    {
        var ns = type.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
        if (ns is not null) sb.AppendLine("}");
    }

    /// <summary>
    /// Emit the containing type. Within it:
    /// - The focus methods get their full body
    /// - Other members of this type get signatures only
    /// - Members not referenced by the focus methods are skipped entirely
    /// </summary>
    private void AppendTypeWithFocus(
        StringBuilder sb,
        TypeDeclarationSyntax type,
        List<MemberDeclarationSyntax> focusMethods,
        HashSet<ISymbol> referenced,
        HashSet<ISymbol> expandedMethods)
    {
        var modifiers = string.Join(" ", type.Modifiers.Select(m => m.Text));
        var kind = type.Keyword.Text;

        var baseList = type.BaseList?.ToString() ?? "";
        sb.AppendLine($"{modifiers} {kind} {type.Identifier}{type.TypeParameterList} {baseList}".Trim());
        sb.AppendLine("{");

        foreach (var member in type.Members)
        {
            if (focusMethods.Contains(member))
            {
                // Focus method: full body
                sb.AppendLine(IndentLines(member.ToFullString().Trim(), "    "));
                sb.AppendLine();
                continue;
            }

            // GetDeclaredSymbol returns null for FieldDeclarationSyntax — the symbol
            // lives on each inner VariableDeclaratorSyntax. Check all declarators.
            ISymbol? memberSymbol = member is FieldDeclarationSyntax fieldDecl
                ? fieldDecl.Declaration.Variables
                    .Select(v => Model.GetDeclaredSymbol(v))
                    .FirstOrDefault(s => s is not null && referenced.Contains(s))
                : Model.GetDeclaredSymbol(member);
            if (memberSymbol is null) continue;

            // Expanded helper: emit full body so the AI sees real logic, not a guess
            if (expandedMethods.Contains(memberSymbol))
            {
                sb.AppendLine(IndentLines(member.ToFullString().Trim(), "    "));
                sb.AppendLine();
                continue;
            }

            // Other referenced members: signature only
            if (!referenced.Contains(memberSymbol)) continue;

            var sig = ToSignature(member);
            if (sig is not null)
                sb.AppendLine($"    {sig}");
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Reduce a member declaration to its signature (no body, no initializer).
    /// </summary>
    private static string PropertyAccessors(PropertyDeclarationSyntax p) =>
        AccessorBlock(p.AccessorList, p.ExpressionBody is not null);

    private static string AccessorBlock(AccessorListSyntax? accessorList, bool hasExpressionBody)
    {
        if (hasExpressionBody || accessorList is null)
            return "{ get; }";
        var accessors = string.Join(" ", accessorList.Accessors.Select(a =>
        {
            var mods = Mods(a.Modifiers);
            var kw = a.Keyword.Text;
            return mods.Length > 0 ? $"{mods} {kw};" : $"{kw};";
        }));
        return $"{{ {accessors} }}";
    }

    private static string GetMemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text,
        ConstructorDeclarationSyntax c => c.Identifier.Text,
        _ => throw new InvalidOperationException($"Unexpected focus member type: {member.GetType().Name}")
    };

    private static string? ToSignature(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m =>
            $"{Mods(m.Modifiers)} {m.ReturnType} {m.Identifier}{m.TypeParameterList}{m.ParameterList};",
        PropertyDeclarationSyntax p =>
            $"{Mods(p.Modifiers)} {p.Type} {p.Identifier} {PropertyAccessors(p)}",
        FieldDeclarationSyntax f =>
            $"{Mods(f.Modifiers)} {f.Declaration.Type} {string.Join(", ", f.Declaration.Variables.Select(v => v.Identifier.Text))};",
        ConstructorDeclarationSyntax c =>
            $"{Mods(c.Modifiers)} {c.Identifier}{c.ParameterList};",
        EventFieldDeclarationSyntax e =>
            $"{Mods(e.Modifiers)} event {e.Declaration};",
        IndexerDeclarationSyntax i =>
            $"{Mods(i.Modifiers)} {i.Type} this{i.ParameterList} {AccessorBlock(i.AccessorList, i.ExpressionBody is not null)}",
        OperatorDeclarationSyntax o =>
            $"{Mods(o.Modifiers)} {o.ReturnType} operator {o.OperatorToken}{o.ParameterList};",
        ConversionOperatorDeclarationSyntax cv =>
            $"{Mods(cv.Modifiers)} {cv.ImplicitOrExplicitKeyword} operator {cv.Type}{cv.ParameterList};",
        _ => null
    };

    private static string Mods(SyntaxTokenList tokens) =>
        string.Join(" ", tokens.Select(t => t.Text));

    private static string IndentLines(string text, string indent) =>
        string.Join('\n', text.Split('\n').Select(l => indent + l));

    /// <summary>
    /// Returns true if a member has no explicit public/protected/internal modifier.
    /// In a class body the implicit default is private.
    /// </summary>
    private static bool IsPrivate(MemberDeclarationSyntax member)
    {
        SyntaxTokenList mods = member switch
        {
            BaseMethodDeclarationSyntax m => m.Modifiers,
            BasePropertyDeclarationSyntax p => p.Modifiers,
            BaseFieldDeclarationSyntax f => f.Modifiers,
            _ => default
        };
        return !mods.Any(t => t.IsKind(SyntaxKind.PublicKeyword)
                           || t.IsKind(SyntaxKind.ProtectedKeyword)
                           || t.IsKind(SyntaxKind.InternalKeyword));
    }

    // -------------------------- helpers ---------------------------

    private string BuildNotes(int focusCount, int refCount, TypeDeclarationSyntax type, int expandedCount, int depth)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Focused emission of {Path.GetFileName(_tree.FilePath)}");
        sb.AppendLine($"// Focus method(s): {focusCount} overload(s) included with full body");
        if (depth >= 1)
            sb.AppendLine($"// Expanded helpers (depth {depth}): {expandedCount} private member(s) included with full body");
        sb.AppendLine($"// Other members: {refCount} symbols referenced, signatures only");
        sb.AppendLine($"// Containing type: {type.Identifier}");
        return sb.ToString();
    }

    /// <summary>
    /// Default reference set: just the running .NET runtime so basic types
    /// (System.Object, System.String, Task) resolve. For real use, point
    /// at the project's reference assemblies.
    /// </summary>
    private static IEnumerable<string> GetDefaultReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedAssemblies is null) yield break;
        foreach (var p in trustedAssemblies.Split(Path.PathSeparator))
            yield return p;
    }
}

/// <summary>
/// The result of a focused emission, including stats useful for
/// "how much did this save" reporting.
/// </summary>
public sealed record FocusResult(
    bool Found,
    string Output,
    int OriginalChars,
    int FocusedChars,
    string FocusMethodName,
    string Notes)
{
    public int CharsSaved => OriginalChars - FocusedChars;
    public double ReductionPercent =>
        OriginalChars == 0 ? 0 : (double)CharsSaved / OriginalChars * 100;
    public int OriginalTokensEstimate => Math.Max(1, OriginalChars / 4);
    public int FocusedTokensEstimate => Math.Max(1, FocusedChars / 4);

    public static FocusResult NotFound(string name) =>
        new(false, $"// Method '{name}' not found in source", 0, 0, name, "");
}
