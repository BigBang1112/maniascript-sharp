using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Detects <c>OnChange(value, oldValue => { ... })</c> calls (declared as
/// <c>OnChange&lt;T&gt;(T value, ChangeCallback&lt;T&gt; callback, ...)</c> on <c>CNod</c>
/// or any other API type) and extracts the pieces needed to translate them into a static
/// <c>if (Value != OldValue) { ... OldValue = Value; }</c> block. Shared by
/// <see cref="OnChangeCollector"/> (which declares the backing globals) and
/// <see cref="StatementEmitter"/> (which emits the call-site translation).
/// </summary>
internal static class OnChangeSupport
{
    internal readonly struct Match
    {
        public ExpressionSyntax ValueExpr { get; }
        public ITypeSymbol ValueType { get; }
        public string BackingName { get; }
        public AnonymousFunctionExpressionSyntax Callback { get; }
        public string CallbackParamName { get; }

        public Match(ExpressionSyntax valueExpr, ITypeSymbol valueType, string backingName,
            AnonymousFunctionExpressionSyntax callback, string callbackParamName)
        {
            ValueExpr = valueExpr;
            ValueType = valueType;
            BackingName = backingName;
            Callback = callback;
            CallbackParamName = callbackParamName;
        }
    }

    /// <summary>Returns true when <paramref name="sym"/> is an <c>OnChange(T, ChangeCallback&lt;T&gt;, ...)</c>-shaped method.</summary>
    internal static bool IsOnChangeMethod(IMethodSymbol? sym)
    {
        if (sym is not { Name: "OnChange" } || sym.Parameters.Length < 2) return false;
        var cb = sym.Parameters[1].Type as INamedTypeSymbol;
        return cb is { Name: "ChangeCallback" } && cb.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp";
    }

    /// <summary>Attempts to fully match a call's shape (field/property value + single-param callback). See <see cref="Match"/>.</summary>
    internal static bool TryMatch(SemanticModel model, InvocationExpressionSyntax inv, out Match match)
    {
        match = default;
        if (!IsOnChangeMethod(model.GetSymbolInfo(inv).Symbol as IMethodSymbol)) return false;

        var args = inv.ArgumentList.Arguments;
        if (args.Count < 2) return false;

        var valueExpr = args[0].Expression;

        // Callback: oldValue => { ... } or (T oldValue) => { ... } — exactly one parameter.
        var callbackParamName = (args[1].Expression as AnonymousFunctionExpressionSyntax) switch
        {
            SimpleLambdaExpressionSyntax sl => sl.Parameter.Identifier.Text,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } pl
                => pl.ParameterList.Parameters[0].Identifier.Text,
            _ => null,
        };
        if (callbackParamName is null || args[1].Expression is not AnonymousFunctionExpressionSyntax callback)
            return false;

        // The backing global's name is derived from the referenced field/property.
        var baseName = model.GetSymbolInfo(valueExpr).Symbol switch
        {
            IFieldSymbol f => NameMangler.PascalCase(f.Name),
            IPropertySymbol p => NameMangler.PascalCase(p.Name),
            _ => null,
        };
        if (baseName is null) return false;

        var valueType = model.GetTypeInfo(valueExpr).Type;
        if (valueType is null) return false;

        match = new Match(valueExpr, valueType, "Old" + baseName, callback, callbackParamName);
        return true;
    }
}
