using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class TypeWriter : ExpressionWriter<TypeSyntax>
{
    public override void Write(TypeSyntax expression)
    {
        var symbol = GetSymbol() ?? throw new ExpressionException("Symbol not found");

        if (symbol is ITypeSymbol typeSymbol)
        {
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(typeSymbol, new HashSet<string>(BodyBuilder.Head.Structs.Select(x => x.Name))));
        }
    }
}