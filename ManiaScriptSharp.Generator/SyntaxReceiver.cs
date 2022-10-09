using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class SyntaxReceiver : ISyntaxReceiver
{
    public string? ClassName { get; private set; }
    
    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        ClassName = syntaxNode.AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault()?
            .Identifier
            .Text;
    }
}