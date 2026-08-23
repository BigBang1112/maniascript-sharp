using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class IdentifierNameExpressionWriter : ExpressionWriter<IdentifierNameSyntax>
{
    public override void Write(IdentifierNameSyntax expression)
    {
        var symbol = GetSymbol();

        if (symbol is null)
        {
            // TODO: Add some form of warning here
        }

        var text = expression.Identifier.Text;

        if (symbol is not null && !symbol.DeclaringSyntaxReferences.IsDefaultOrEmpty)
        {
            // May be slow
            if (BodyBuilder.Head.Consts.Contains(symbol, SymbolEqualityComparer.Default))
            {
                text = Standardizer.StandardizeConstName(symbol.Name);
            }
            else if (BodyBuilder.Head.Settings.Contains(symbol, SymbolEqualityComparer.Default))
            {
                text = Standardizer.StandardizeSettingName(symbol.Name);
            }
            else if (BodyBuilder.Head.Globals.Contains(symbol, SymbolEqualityComparer.Default))
            {
                text = Standardizer.StandardizeGlobalName(symbol.Name);
            }
            else if (BodyBuilder.Head.Netwrites.Contains(symbol, SymbolEqualityComparer.Default) || BodyBuilder.Head.Netreads.Contains(symbol, SymbolEqualityComparer.Default) || BodyBuilder.Head.Locals.Contains(symbol, SymbolEqualityComparer.Default))
            {
                Writer.Write("Get");
                Writer.Write(symbol.Name);
                Writer.Write("()");
                return;
            }
        }

        if (symbol is IPropertySymbol propertySymbol && symbol.Name.EndsWith("E"))
        {
            var att = symbol.GetAttributes().FirstOrDefault(x => x.AttributeClass?.Name == "ActualNameAttribute");

            if (att is not null)
            {
                text = att.ConstructorArguments[0].Value?.ToString() ?? text;
            }
        }

        if (symbol is IMethodSymbol {ReceiverType.Name: "ManiaScript"})
        {
            Writer.Write(char.ToLower(text[0]) + text.Substring(1));
            return;
        }

        if (BodyBuilder.IsBuildingEventHandling || !Parameters.Any(x => x.Identifier.Text == text))
        {
            Writer.Write(Standardizer.StandardizeName(text));
        }
        else
        {
            Writer.Write(Standardizer.StandardizeUnderscoreName(text));
        }
    }
}