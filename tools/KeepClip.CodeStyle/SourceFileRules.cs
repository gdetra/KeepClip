using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KeepClip.CodeStyle;

internal static class SourceFileRules
{
    public static void Validate(string path, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var types = root.DescendantNodes().Count(node =>
            (node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax) &&
            node.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax);
        if (types > 1)
        {
            throw new InvalidOperationException($"Only one top-level type is allowed per file: {path}");
        }
    }
}
