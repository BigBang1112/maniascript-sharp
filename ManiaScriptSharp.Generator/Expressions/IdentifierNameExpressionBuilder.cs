using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class IdentifierNameExpressionBuilder : ExpressionBuilder<IdentifierNameSyntax>
{
    public override void Write(int ident, IdentifierNameSyntax expression, ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        var symbol = bodyBuilder.SemanticModel.GetSymbolInfo(expression).Symbol;
        
        var text = expression.Identifier.Text;

        if (symbol is IMethodSymbol {ReceiverType.Name: "ManiaScript"})
        {
            Writer.Write(char.ToLower(text[0]) + text.Substring(1));
            return;
        }
        
        Writer.Write(parameters.ContainsKey(text)
            ? Standardizer.StandardizeUnderscoreName(text)
            : Standardizer.StandardizeName(text));
    }
}