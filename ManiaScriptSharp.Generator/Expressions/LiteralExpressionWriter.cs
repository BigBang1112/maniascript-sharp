using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;

namespace ManiaScriptSharp.Generator.Expressions;

public class LiteralExpressionWriter : ExpressionWriter<LiteralExpressionSyntax>
{
    public override void Write(LiteralExpressionSyntax expression)
    {
        switch (expression.Token.Value)
        {
            case null:
                Writer.Write("Null");
                break;
            case string str:
                Writer.Write($"\"{str.Replace("\"", "\\\"")}\"");
                break;
            case float f:
                var fStr = f.ToString(CultureInfo.InvariantCulture);
                if (!fStr.Contains('.')) fStr += ".";
                Writer.Write(fStr);
                break;
            case double d:
                var dStr = d.ToString(CultureInfo.InvariantCulture);
                if (!dStr.Contains('.')) dStr += ".";
                Writer.Write(dStr);
                break;
            default:
                Writer.Write(expression.Token.Value);
                break;
        }
    }
}