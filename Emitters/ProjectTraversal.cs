using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLean;

/// <summary>
/// Scans all .cs files in a project directory to support cross-file traversal queries.
/// Uses syntax-tree analysis (no compilation), consistent with EmitCallers.
/// </summary>
public sealed class ProjectTraversal
{
    private readonly List<(string Path, SyntaxTree Tree)> _files;

    public int FileCount => _files.Count;

    public ProjectTraversal(string projectPath)
    {
        var dir = ResolveDirectory(projectPath);
        _files = Directory
            .EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f))
            .Select(f => (Path: f, Tree: CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)))
            .ToList();
    }

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
            .Where(t => t.BaseList?.Types.Any(bt =>
            {
                var name = bt.Type switch
                {
                    IdentifierNameSyntax id => id.Identifier.Text,
                    GenericNameSyntax gn => gn.Identifier.Text,
                    QualifiedNameSyntax qn => qn.Right.Identifier.Text,
                    _ => null
                };
                return name == interfaceName;
            }) == true)
            .Select(t => (FilePath: filePath, TypeName: t.Identifier.Text));
    }

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
