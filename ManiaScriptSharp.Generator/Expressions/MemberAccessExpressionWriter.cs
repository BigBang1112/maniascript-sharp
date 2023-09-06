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
            && nameSymbol?.Name is "Clear" or "Add";

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
        "DictionaryExtensions",
        "IList",
        "ICollection",
        "ImmutableArray",
        "ObjectExtensions",
        "object"
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

        if (nameSymbol.Name == "ToJson")
        {
            Writer.Write("tojson");
            return true;
        }

        if (nameSymbol.Name is "Contains" or "ContainsValue")
        {
            Writer.Write("exists");
            return true;
        }

        if (nameSymbol.Name == "ContainsKey")
        {
            Writer.Write("existskey");
            return true;
        }

        if (nameSymbol.Name is "Count" or "Length")
        {
            Writer.Write("count");
            return true;
        }

        if (nameSymbol.Name is "IndexOf" or "KeyOf")
        {
            Writer.Write("keyof");
            return true;
        }

        if (nameSymbol.Name == "Sort")
        {
            Writer.Write("sort");
            return true;
        }

        if (nameSymbol.Name == "Remove")
        {
            Writer.Write("remove");
            return true;
        }

        if (nameSymbol.Name == "Add")
        {
            Writer.Write(isDeclareFor && expressionName is not null ? "AddTo" + expressionName : "add");
            return true;
        }

        return false;
    }
}