using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLean;

/// <summary>
/// A single Dependency-Injection container registration discovered in source.
/// <paramref name="Key"/> is non-null only for keyed registrations (e.g. AddKeyedScoped).
/// </summary>
public sealed record DiRegistration(
    string FilePath, int Line, string Method,
    string ServiceType, string ImplType, string? Key);

/// <summary>
/// One type declared in the project, as surfaced by <see cref="ProjectTraversal.MapTypes"/>.
/// <paramref name="Bases"/> is null when the type has no base list.
/// </summary>
public sealed record TypeMapEntry(
    string Name, string Kind, string FilePath, int Line, string? Bases, string Modifiers);

/// <summary>
/// Scans all .cs files in a project directory to support cross-file traversal queries.
/// Uses syntax-tree analysis (no compilation), consistent with EmitCallers.
/// </summary>
public sealed class ProjectTraversal
{
    /// <summary>
    /// DI registration method names recognised by <see cref="FindDiRegistrations"/>.
    /// Core Microsoft.Extensions.DependencyInjection lifetimes, their TryAdd and keyed variants.
    /// </summary>
    private static readonly HashSet<string> DiMethodNames = new(StringComparer.Ordinal)
    {
        "AddScoped", "AddSingleton", "AddTransient",
        "TryAddScoped", "TryAddSingleton", "TryAddTransient",
        "AddKeyedScoped", "AddKeyedSingleton", "AddKeyedTransient",
        "TryAddKeyedScoped", "TryAddKeyedSingleton", "TryAddKeyedTransient",
    };

    private readonly List<(string Path, SyntaxTree Tree)> _files;

    public int FileCount => _files.Count;

    public ProjectTraversal(string projectPath)
    {
        Root = ResolveDirectory(projectPath);
        _files = Directory
            .EnumerateFiles(Root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f))
            // Discovery (enumeration) runs every call; only the read+parse is cached per
            // file by mtime, so new/edited files are always picked up. See ParsedTreeCache.
            .Select(f => (Path: f, Tree: ParsedTreeCache.GetOrParse(f)))
            .ToList();
    }

    /// <summary>The resolved project root directory the scan was rooted at.</summary>
    public string Root { get; }

    /// <summary>
    /// Returns paths of files that contain at least one method calling <paramref name="methodName"/>.
    /// </summary>
    public List<string> FindCallerFiles(string methodName) =>
        _files
            .Where(f => FileContainsCaller(f.Tree, methodName))
            .Select(f => f.Path)
            .ToList();

    /// <summary>
    /// Returns (filePath, typeName) pairs for every type that lists <paramref name="interfaceName"/>
    /// in its base list.
    /// </summary>
    public List<(string FilePath, string TypeName)> FindImplementors(string interfaceName) =>
        _files
            .SelectMany(f => GetImplementors(f.Tree, f.Path, interfaceName))
            .ToList();

    /// <summary>
    /// Returns every DI container registration across the project that references
    /// <paramref name="typeName"/> as either the service type or the implementation type.
    /// </summary>
    public List<DiRegistration> FindDiRegistrations(string typeName) =>
        _files
            .SelectMany(f => GetDiRegistrations(f.Tree, f.Path, typeName))
            .ToList();

    /// <summary>
    /// Returns every type (class/struct/record/interface/enum) declared in the project, with
    /// its kind, declaring file, line, base list, and visibility modifiers. When
    /// <paramref name="nameFilter"/> is non-empty, only types whose name contains it
    /// (case-insensitive) are returned. The project entrypoint for "what types exist and where".
    /// </summary>
    public List<TypeMapEntry> MapTypes(string? nameFilter = null) =>
        _files
            .SelectMany(f => GetTypeEntries(f.Tree, f.Path))
            .Where(e => string.IsNullOrWhiteSpace(nameFilter)
                     || e.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static bool FileContainsCaller(SyntaxTree tree, string targetMethod)
    {
        var root = tree.GetCompilationUnitRoot();
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Any(m =>
                m.Identifier.Text != targetMethod &&
                m.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(inv =>
                    (inv.Expression is IdentifierNameSyntax id && id.Identifier.Text == targetMethod) ||
                    (inv.Expression is MemberAccessExpressionSyntax mae && mae.Name.Identifier.Text == targetMethod)));
    }

    private static IEnumerable<(string FilePath, string TypeName)> GetImplementors(
        SyntaxTree tree, string filePath, string interfaceName)
    {
        var root = tree.GetCompilationUnitRoot();
        return root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(t => t.BaseList?.Types.Any(bt => SimpleName(bt.Type) == interfaceName) == true)
            .Select(t => (FilePath: filePath, TypeName: t.Identifier.Text));
    }

    private static IEnumerable<DiRegistration> GetDiRegistrations(
        SyntaxTree tree, string filePath, string typeName)
    {
        var root = tree.GetCompilationUnitRoot();
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            // Callee name lives in member access (services.AddScoped) or a bare identifier.
            SimpleNameSyntax? callee = inv.Expression switch
            {
                MemberAccessExpressionSyntax mae => mae.Name,
                SimpleNameSyntax sn => sn,
                _ => null
            };
            if (callee is null) continue;

            var method = callee.Identifier.Text;
            if (!DiMethodNames.Contains(method)) continue;

            var (serviceType, implType, key) = ExtractRegistration(callee, inv.ArgumentList);
            if (serviceType is null) continue;

            if (serviceType != typeName && implType != typeName) continue;

            var line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return new DiRegistration(filePath, line, method, serviceType, implType ?? serviceType, key);
        }
    }

    /// <summary>
    /// Pulls (service, impl, key) out of a single DI registration call. Handles the generic
    /// (AddScoped&lt;IFoo, Foo&gt;), single-generic + factory-lambda (AddSingleton&lt;IFoo&gt;(sp => new Foo()))
    /// and typeof (AddScoped(typeof(IFoo), typeof(Foo))) forms. Returns a null service type when
    /// the call carries no resolvable type (e.g. an instance-only AddSingleton(myObj)).
    /// </summary>
    private static (string? Service, string? Impl, string? Key) ExtractRegistration(
        SimpleNameSyntax callee, ArgumentListSyntax? args)
    {
        string? service = null;
        string? impl = null;

        if (callee is GenericNameSyntax generic)
        {
            var typeArgs = generic.TypeArgumentList.Arguments;
            if (typeArgs.Count >= 1) service = SimpleName(typeArgs[0]);
            if (typeArgs.Count >= 2) impl = SimpleName(typeArgs[1]);
        }
        else if (args is not null)
        {
            // Non-generic: AddScoped(typeof(IFoo), typeof(Foo)) — read the typeof() arguments.
            var typeOfs = args.Arguments
                .Select(a => a.Expression)
                .OfType<TypeOfExpressionSyntax>()
                .ToList();
            if (typeOfs.Count >= 1) service = SimpleName(typeOfs[0].Type);
            if (typeOfs.Count >= 2) impl = SimpleName(typeOfs[1].Type);
        }

        // Factory lambda: AddScoped<IFoo>(sp => new Foo()) — infer impl from the created type.
        if (impl is null && args is not null)
        {
            var created = args.Arguments
                .Select(a => a.Expression)
                .OfType<LambdaExpressionSyntax>()
                .SelectMany(l => l.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                .Select(oc => SimpleName(oc.Type))
                .FirstOrDefault();
            if (created is not null) impl = created;
        }

        // Keyed registrations take the key as the first non-type argument (string or identifier).
        // Match on "Keyed" anywhere in the name so both AddKeyed* and TryAddKeyed* are covered.
        string? key = null;
        if (callee.Identifier.Text.Contains("Keyed", StringComparison.Ordinal) && args is not null)
        {
            var keyArg = args.Arguments
                .Select(a => a.Expression)
                .FirstOrDefault(e => e is LiteralExpressionSyntax or IdentifierNameSyntax or MemberAccessExpressionSyntax);
            key = keyArg switch
            {
                LiteralExpressionSyntax lit => lit.Token.ValueText,
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax mae => mae.Name.Identifier.Text,
                _ => null
            };
        }

        return (service, impl, key);
    }

    private static readonly string[] InterestingModifiers =
        { "public", "internal", "abstract", "static", "sealed" };

    private static IEnumerable<TypeMapEntry> GetTypeEntries(SyntaxTree tree, string filePath)
    {
        var root = tree.GetCompilationUnitRoot();
        foreach (var decl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var kind = decl switch
            {
                EnumDeclarationSyntax => "enum",
                TypeDeclarationSyntax t => t.Keyword.Text,
                _ => "type"
            };

            var bases = decl.BaseList?.Types
                .Select(bt => SimpleName(bt.Type))
                .Where(n => n is not null)
                .ToList();
            var basesStr = bases is { Count: > 0 } ? string.Join(", ", bases) : null;

            var modifiers = string.Join(" ", decl.Modifiers
                .Select(m => m.Text)
                .Where(InterestingModifiers.Contains));

            var line = decl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return new TypeMapEntry(decl.Identifier.Text, kind, filePath, line, basesStr, modifiers);
        }
    }

    /// <summary>Extracts the simple (unqualified) name from a type syntax node.</summary>
    private static string? SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        GenericNameSyntax gn => gn.Identifier.Text,
        QualifiedNameSyntax qn => qn.Right.Identifier.Text,
        _ => null
    };

    private static string ResolveDirectory(string projectPath)
    {
        var full = Path.GetFullPath(projectPath);
        if (Directory.Exists(full))
            return full;
        if (File.Exists(full) &&
            Path.GetExtension(full).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(full)!;
        throw new ArgumentException($"Path does not exist or is not a directory/.csproj: {projectPath}");
    }

    private static bool IsExcluded(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p =>
            p.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }
}
