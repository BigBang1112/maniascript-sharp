using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class IdentifierNameExpressionBuilder : ExpressionBuilder<IdentifierNameSyntax>
{
    public override void Write(int ident, IdentifierNameSyntax expression, ImmutableDictionary<string, ParameterSyntax> parameters, ManiaScriptBodyBuilder bodyBuilder)
    {
        var text = expression.Identifier.Text;

        switch (text) // Probably temporary
        {
            case "Log":
                Writer.Write("log");
                return;
        }
        
        Writer.Write(parameters.ContainsKey(text)
            ? Standardizer.StandardizeUnderscoreName(text)
            : Standardizer.StandardizeName(text));
    }
}