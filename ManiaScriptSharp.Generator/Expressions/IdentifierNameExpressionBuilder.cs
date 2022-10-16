using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class IdentifierNameExpressionBuilder : ExpressionBuilder<IdentifierNameSyntax>
{
    public override void Write(int ident, IdentifierNameSyntax expression, ImmutableArray<ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        var symbol = bodyBuilder.SemanticModel.GetSymbolInfo(expression).Symbol;

        if (symbol is null)
        {
            // TODO: Add some form of warning here
        }
        
        var text = expression.Identifier.Text;

        if (symbol is IMethodSymbol {ReceiverType.Name: "ManiaScript"})
        {
            Writer.Write(char.ToLower(text[0]) + text.Substring(1));
            return;
        }

        if (bodyBuilder.IsBuildingEventHandling || !parameters.Any(x => x.Identifier.Text == text))
        {
            Writer.Write(Standardizer.StandardizeName(text));
        }
        else
        {
            Writer.Write(Standardizer.StandardizeUnderscoreName(text));
        }
    }
}