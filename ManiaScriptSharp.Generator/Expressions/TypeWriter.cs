using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class TypeWriter : ExpressionWriter<TypeSyntax>
{
    public override void Write(TypeSyntax expression)
    {
        var symbol = GetSymbol() ?? throw new ExpressionException("Symbol not found");

        Writer.Write(Standardizer.CSharpTypeToManiaScriptType(symbol.Name));
    }
}