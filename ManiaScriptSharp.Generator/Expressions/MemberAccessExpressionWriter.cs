using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public class MemberAccessExpressionWriter : ExpressionWriter<MemberAccessExpressionSyntax>
{
    public override void Write(MemberAccessExpressionSyntax expression)
    {
        switch (expression.Expression)
        {
            case BaseExpressionSyntax:
                if (GetSymbol(expression.Name) is IPropertySymbol)
                {
                    WriteSyntax(expression.Name);
                }
                return;
            case ThisExpressionSyntax:
                return;
        }

        var expressionSymbol = GetSymbol(expression.Expression);

        switch (expressionSymbol)
        {
            case INamespaceSymbol:
            case {IsStatic: true, Name: "ManiaScript"}:
                WriteSyntax(expression.Name);
                return;
        }

        var nameSymbol = GetSymbol(expression.Name);

        if (expressionSymbol is ITypeSymbol {TypeKind: TypeKind.Enum} && expression.Expression is IdentifierNameSyntax)
        {
            Writer.Write(expressionSymbol.ContainingType.Name);
            Writer.Write("::");
        }

        var isDeclareFor = (expressionSymbol?.GetAttributes()
            .Any(x => x.AttributeClass?.Name is NameConsts.NetwriteAttribute) ?? false)
            && nameSymbol?.Name is "Clear";

        if (!isDeclareFor)
        {
            WriteSyntax(expression.Expression);
        }

        if (expressionSymbol?.IsStatic == true || expressionSymbol is ITypeSymbol {TypeKind: TypeKind.Enum}
                                               || nameSymbol is ITypeSymbol {TypeKind: TypeKind.Enum})
        {
            Writer.Write("::");
        }
        else if (!isDeclareFor)
        {
            Writer.Write('.');
        }

        if (TryRemap(nameSymbol, expressionSymbol?.Name, isDeclareFor))
        {
            return;
        }

        WriteSyntax(expression.Name);
    }

    private static readonly HashSet<string> specialTypes = new()
    {
        "Dictionary",
        "IList",
        "ICollection",
        "ImmutableArray",
        "ObjectExtensions"
    };

    private bool TryRemap(ISymbol? nameSymbol, string? expressionName, bool isDeclareFor)
    {
        if (nameSymbol is null || !specialTypes.Contains(nameSymbol.ContainingType.Name))
        {
            return false;
        }

        if (nameSymbol.Name == "Clear")
        {
            Writer.Write(isDeclareFor && expressionName is not null ? "Clear" + expressionName : "clear");
            return true;
        }

        if (nameSymbol.Name == "FromJson")
        {
            Writer.Write("fromjson");
            return true;
        }

        if (nameSymbol.Name == "ContainsKey")
        {
            Writer.Write("existskey");
            return true;
        }

        if (nameSymbol.Name == "Add")
        {
            Writer.Write("add");
            return true;
        }

        return false;
    }
}