using System.Text;
using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Translates a C# expression tree into a ManiaScript expression string.
/// Owns: identifier resolution (G_/S_/C_/Net_/Persistent_, _Param, PascalCase locals),
/// "." → "::" for static/enum members, list/dict API mapping, string interpolation
/// → multiline-string form, `^` concatenation, vector/list/dict literals, casts, patterns,
/// label call rewrite (`Foo()` → `+++Foo+++`).
/// </summary>
internal sealed class ExpressionEmitter
{
    private readonly EmitContext _ctx;
    private PatternEmitter? _patterns;
    public void Bind(PatternEmitter p) => _patterns = p;

    public ExpressionEmitter(EmitContext ctx) { _ctx = ctx; }

    public string Translate(ExpressionSyntax expr)
    {
        return expr switch
        {
            LiteralExpressionSyntax lit => TranslateLiteral(lit),
            IdentifierNameSyntax id => TranslateIdentifier(id),
            MemberAccessExpressionSyntax m => TranslateMember(m),
            InvocationExpressionSyntax inv => TranslateInvocation(inv),
            BinaryExpressionSyntax bin => TranslateBinary(bin),
            AssignmentExpressionSyntax asg => TranslateAssignment(asg),
            PrefixUnaryExpressionSyntax pre => TranslatePrefix(pre),
            PostfixUnaryExpressionSyntax post => TranslatePostfix(post),
            ParenthesizedExpressionSyntax par => $"({Translate(par.Expression)})",
            ElementAccessExpressionSyntax ea => TranslateElementAccess(ea),
            InterpolatedStringExpressionSyntax istr => TranslateInterpolatedString(istr),
            CastExpressionSyntax cast => TranslateCast(cast),
            DefaultExpressionSyntax def when EnumSupport.IsCustomEnum(_ctx.Model.GetTypeInfo(def.Type).Type) => "0",
            IsPatternExpressionSyntax isp when _patterns is not null => _patterns.TranslateAsExpression(isp),
            ObjectCreationExpressionSyntax oc => TranslateObjectCreation(oc),
            ImplicitObjectCreationExpressionSyntax ioc => TranslateImplicitObjectCreation(ioc),
            CollectionExpressionSyntax ce => TranslateCollectionExpr(ce),
            InitializerExpressionSyntax init => TranslateInitializer(init),
            // ManiaScript has no inline conditional (?:) or switch expression — only supported as a
            // statement-level if/else rewrite (see StatementEmitter.EmitTernaryAsIfElse / EmitSwitchExpressionAsIfElse).
            ConditionalExpressionSyntax => Unsupported(expr, "ternary operator '?:' (extract to an if/else statement)"),
            SwitchExpressionSyntax => Unsupported(expr, "switch expression (extract to an if/else or switch statement)"),
            _ => Unsupported(expr, expr.Kind().ToString()),
        };
    }

    private string Unsupported(ExpressionSyntax expr, string label)
    {
        _ctx.Report(Diagnostics.Unsupported, expr.GetLocation(), label);
        return $"/* {label} */";
    }

    // ------- Literals & identifiers -------

    private string TranslateLiteral(LiteralExpressionSyntax lit) => lit.Kind() switch
    {
        SyntaxKind.TrueLiteralExpression => "True",
        SyntaxKind.FalseLiteralExpression => "False",
        SyntaxKind.NullLiteralExpression => TranslateNullLiteral(lit),
        SyntaxKind.DefaultLiteralExpression => TranslateNullLiteral(lit),
        SyntaxKind.StringLiteralExpression => TranslateStringLiteral(lit),
        SyntaxKind.CharacterLiteralExpression => "\"" + lit.Token.ValueText + "\"",
        SyntaxKind.NumericLiteralExpression => TranslateNumeric(lit),
        _ => lit.Token.Text,
    };

    /// <summary>Maps <c>default</c> for custom enums to zero and null identifiers to <c>NullId</c>.</summary>
    private string TranslateNullLiteral(LiteralExpressionSyntax lit)
    {
        // The literal must belong to the bound model's tree — patterns translated from a
        // detached syntax fragment (e.g. constant sub-expressions in `is`-patterns) have no
        // semantic info available, so fall back to the untyped "Null" in that case.
        if (lit.SyntaxTree != _ctx.Model.SyntaxTree) return "Null";

        var type = _ctx.Model.GetTypeInfo(lit).ConvertedType;
        var isNullable = false;
        if (type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            isNullable = true;
            type = nullable.TypeArguments[0];
        }
        if (lit.IsKind(SyntaxKind.DefaultLiteralExpression) && !isNullable && EnumSupport.IsCustomEnum(type)) return "0";
        return type?.Name == "Ident" ? "NullId" : "Null";
    }

    private string TranslateStringLiteral(LiteralExpressionSyntax lit)
    {
        var raw = lit.Token.Text;
        if (raw.StartsWith("@\"") || raw.StartsWith("\"\"\""))
            return FormatMultilineString(lit.Token.ValueText);
        return raw;
    }

    private static string TranslateNumeric(LiteralExpressionSyntax lit)
    {
        var text = lit.Token.Text;
        // Strip C#-specific suffixes (f, d, m, u, l, ul).
        var cleaned = text.TrimEnd('f', 'F', 'd', 'D', 'm', 'M', 'u', 'U', 'l', 'L');
        if (lit.Token.Value is float or double or decimal)
            return cleaned.Contains('.') ? cleaned : cleaned + ".";
        return cleaned;
    }

    private string TranslateIdentifier(IdentifierNameSyntax id)
    {
        // `value` inside a setter accessor maps to the ManiaScript parameter `_Value`.
        if (id.Identifier.Text == "value") return "_Value";

        var sym = _ctx.Model.GetSymbolInfo(id).Symbol;
        switch (sym)
        {
            case IFieldSymbol f:
                if (EnumSupport.IsCustomEnum(f.ContainingType)) return TranslateEnumConst(f);
                if (f.HasAttr("SettingAttribute")) return NameMangler.Setting(f);
                if (f.IsConst) return NameMangler.Const(f);
                if (f.HasAttr("ManialinkControlAttribute")) return NameMangler.Global(f);
                if (f.IsLibImplementation() && _ctx.TryGetLibraryAlias((INamedTypeSymbol)f.Type, out var libAlias)) return libAlias;
                return NameMangler.Global(f);
            case IPropertySymbol p:
                if (p.HasAttr("ManialinkControlAttribute")) return NameMangler.PascalCase(p.Name);
                // Only translate to getter call for user-defined properties (those with source syntax).
                // API-generated auto-properties (e.g. LocalUser, Score on CMlScript) stay as plain names.
                if (p.GetMethod is not null && IsUserDefinedProperty(p))
                    return NameMangler.Getter(p) + "()";
                return NameMangler.PascalCase(p.Name);
            case IParameterSymbol p:
                // Lambda params bound via BindLambdaParams are registered in DeclareForLocals.
                if (_ctx.DeclareForLocals.TryGetValue(p.Name, out var pMsName))
                    return pMsName;
                return NameMangler.Parameter(p);
            case ILocalSymbol l:
                if (_ctx.DeclareForLocals.TryGetValue(l.Name, out var msName))
                    return msName;
                if (_ctx.PendingLinqChains.ContainsKey(l.Name))
                {
                    _ctx.Report(Diagnostics.UnsupportedLinq, id.GetLocation(),
                        $"'{l.Name}' is a lazy LINQ chain — assign it to a variable using a terminal call (e.g. .ToList(), .Count(), .First()) to materialise it");
                    return NameMangler.Local(l.Name);
                }
                return NameMangler.Local(l.Name);
            case IMethodSymbol m:
                if (_ctx.IsLabelMethod(m)) return $"+++{m.Name}+++";
                return NameMangler.Method(m);
            // Native API enum type used bare → route through TypeMapper for qualification.
            case INamedTypeSymbol { TypeKind: TypeKind.Enum } nt:
                return TypeMapper.Map(nt);
        }
        return id.Identifier.Text;
    }

    // ------- Member access -------

    private string TranslateMember(MemberAccessExpressionSyntax m)
    {
        // Built-in ManiaScript static helpers.
        if (m.Expression is IdentifierNameSyntax e && e.Identifier.Text == "ManiaScript")
        {
            return m.Name.Identifier.Text switch
            {
                "Now" => "Now",
                _ => m.Name.Identifier.Text.ToLowerInvariant(),
            };
        }

        // System.Math / System.MathF static constant properties → MathLib equivalents.
        if (m.Expression is IdentifierNameSyntax mathId && mathId.Identifier.Text is "Math" or "MathF")
        {
            var mathSym = _ctx.Model.GetSymbolInfo(m.Expression).Symbol as INamedTypeSymbol;
            if (mathSym?.ContainingNamespace?.ToDisplayString() == "System")
            {
                return m.Name.Identifier.Text switch
                {
                    "PI"  => "MathLib::PI()",
                    "E"   => "MathLib::Exp(1.)",
                    "Tau" => "(MathLib::PI() * 2.)",
                    var p => Unsupported(m, $"Math.{p}"),
                };
            }
        }

        var leftSym = _ctx.Model.GetSymbolInfo(m.Expression).Symbol;
        var memberSym = _ctx.Model.GetSymbolInfo(m).Symbol;

        if (memberSym is IFieldSymbol enumMember && EnumSupport.IsCustomEnum(enumMember.ContainingType))
            return TranslateEnumConst(enumMember);

        // string.Empty → "" literal. Checked before translating the receiver because `string`
        // is a PredefinedTypeSyntax, not an identifier, and isn't otherwise translatable.
        if (m.Name.Identifier.Text == "Empty" && memberSym is IFieldSymbol { IsStatic: true } emptyField
            && emptyField.ContainingType?.SpecialType == SpecialType.System_String)
            return "\"\"";

        // pair.Key / pair.Value on a `foreach (var pair in dict)` loop variable → the bare
        // synthesized Key/Value name from the rewritten `foreach (Key => Value in dict)`.
        if (m.Expression is IdentifierNameSyntax dictPairId
            && _ctx.DictPairLocals.TryGetValue(dictPairId.Identifier.Text, out var dictPairNames)
            && m.Name.Identifier.Text is "Key" or "Value")
            return m.Name.Identifier.Text == "Key" ? dictPairNames.KeyName : dictPairNames.ValueName;

        // ILib<T>.Context appears as the left-hand expression of a member-access chain
        // (e.g., Context.Foo or myLib.Context.Foo → strip the Context receiver; it is implicit in ManiaScript).
        if (IsLibContextAccess(leftSym))
        {
            if (!_ctx.IsLib)
            {
                // In consuming code, accessing Context on a lib is an error.
                _ctx.Report(Diagnostics.LibContextAccess, m.GetLocation(), "lib");
            }
            // In both lib and consuming code the context is implicit — emit only the member name.
            return m.Name.Identifier.Text;
        }

        var lhs = Translate(m.Expression);
        var name = memberSym is IFieldSymbol { IsStatic: false, ContainingType: { TypeKind: TypeKind.Struct } } structField
            ? NameMangler.StructField(structField)
            : m.Name.Identifier.Text;

        // Ident.NullId → ManiaScript's bare `NullId` constant (not namespace-qualified).
        if (name == "NullId" && memberSym is IFieldSymbol { IsStatic: true, ContainingType.Name: "Ident" })
            return "NullId";

        // Accessing .Context directly as a member on an ILib field (e.g., myLib.Context).
        if (!_ctx.IsLib && IsLibContextAccess(memberSym))
        {
            _ctx.Report(Diagnostics.LibContextAccess, m.GetLocation(), lhs);
            return lhs;
        }

        // ILib field access uses :: (namespace-scoped in ManiaScript).
        var leftIsLib = leftSym is IFieldSymbol libField && libField.IsLibImplementation();
        var leftLibType = leftSym switch
        {
            IFieldSymbol field when field.IsLibImplementation() => field.Type as INamedTypeSymbol,
            INamedTypeSymbol type when IsLibType(type) => type,
            _ => null,
        };
        var leftIsUserLib = IsUserDefinedLibType(leftLibType);

        // Static C# access (for example, `CounterLib.Limit`) has a type on the left,
        // whereas instance access has the lib field. Both refer to the same ManiaScript
        // include, so normalize either form to its emitted `as Alias` name.
        if (!_ctx.IsManialink && leftLibType is not null
            && _ctx.TryGetLibraryAlias(leftLibType, out var includeAlias))
            lhs = includeAlias;

        if (leftIsUserLib && leftSym is INamedTypeSymbol && memberSym is IFieldSymbol staticLibField)
        {
            if (staticLibField.HasAttr("SettingAttribute"))
                return $"{lhs}::{NameMangler.Setting(staticLibField)}";
            if (staticLibField.IsConst)
                return $"{lhs}::{NameMangler.Const(staticLibField)}";
        }

        // In manialink mode, user-defined libs are inlined into the host script. Their
        // members become top-level names in the SAME script, so the field receiver is
        // stripped. Consts keep their C_* name; fields use their mangled global name
        // (the inlined lib declared them as top-level `declare G_*`); user-defined
        // properties go through their inlined Get*/Set* functions.
        if (leftIsLib && _ctx.IsManialink)
        {
            if (leftIsUserLib)
            {
                if (memberSym is IFieldSymbol inlField)
                {
                    if (inlField.HasAttr("SettingAttribute")) return NameMangler.Setting(inlField);
                    if (inlField.IsConst) return NameMangler.Const(inlField);
                    return NameMangler.Global(inlField);
                }
                if (memberSym is IPropertySymbol inlProp && !inlProp.HasAttr("ManialinkControlAttribute")
                    && inlProp.GetMethod is not null && IsUserDefinedProperty(inlProp))
                    return NameMangler.Getter(inlProp) + "()";
                return m.Name.Identifier.Text;
            }
        }

        // Non-manialink script accessing a user-defined lib's members through the include
        // alias. ManiaScript aliases only expose functions and #Const/#Setting constants —
        // global variables are NOT reachable, so instance fields are an error.
        // (Official Nadeo stubs live in the ManiaScriptSharp namespace and expose no
        // instance fields; their const member names are already the C_*/S_* forms, used as-is.)
        if (leftIsLib && memberSym is IFieldSymbol libMember)
        {
            if (leftIsUserLib)
            {
                if (libMember.HasAttr("SettingAttribute"))
                    return $"{lhs}::{NameMangler.Setting(libMember)}";
                if (libMember.IsConst)
                    return $"{lhs}::{NameMangler.Const(libMember)}";
                // Instance field (a G_* global in the lib) — not reachable via the alias.
                _ctx.Report(Diagnostics.LibFieldAccess, m.GetLocation(),
                    libMember.Name, leftLibType!.Name);
                return "Null";
            }
        }

        var leftIsType = leftSym is INamedTypeSymbol;
        var memberIsStatic = memberSym?.IsStatic ?? false;
        var sep = (leftIsType || leftIsLib || (memberIsStatic && memberSym is IFieldSymbol)) ? "::" : ".";

        // StrongBox<T>.Value access — in ManiaScript the declared variable IS the value; strip .Value.
        // Detected either via symbol (field/property named Value on StrongBox<T>)
        // or via context (receiver is a declare-for local which is always a StrongBox<T>).
        if (name == "Value" && (IsStrongBoxMember(memberSym) || IsStrongBoxReceiver(m.Expression)))
            return lhs;

        // string.Length → TextLib::Length(expr)
        if (memberSym is IPropertySymbol strLenProp && strLenProp.Name == "Length"
            && strLenProp.ContainingType?.SpecialType == SpecialType.System_String)
            return $"TextLib::Length({lhs})";

        // List/dictionary property mapping.
        if (memberSym is IPropertySymbol prop && prop.Name == "Count"
            && (IsListLikeType(prop.ContainingType) || IsDictionaryType(prop.ContainingType)))
            return $"{lhs}.count";

        // User-defined property read → getter call, e.g. `obj.Score` → `obj::GetScore()`.
        if (memberSym is IPropertySymbol userProp && !userProp.HasAttr("ManialinkControlAttribute")
            && userProp.GetMethod is not null
            && IsUserDefinedProperty(userProp))
            return $"{lhs}{sep}{NameMangler.Getter(userProp)}()";

        return $"{lhs}{sep}{name}";
    }

    private string TranslateEnumConst(IFieldSymbol member)
    {
        var name = NameMangler.EnumConst(member);
        for (var owner = member.ContainingType?.ContainingType; owner is not null; owner = owner.ContainingType)
        {
            if (!TypeMapper.IsContextOrLibType(owner)
                || SymbolEqualityComparer.Default.Equals(owner, _ctx.Info.Symbol)) continue;

            if (!_ctx.IsManialink && _ctx.TryGetLibraryAlias(owner, out var alias))
                return $"{alias}::{name}";
            break;
        }
        return name;
    }

    /// <summary>
    /// Returns true when the property is declared on the context/lib class currently being
    /// emitted, or on another class implementing <c>IContext</c>/<c>ILib&lt;T&gt;</c> — those are
    /// the only cases where a matching Get*()/Set*() function actually exists (emitted by this
    /// class's own <see cref="FunctionEmitter"/> pass, or by that other lib's own pass).
    /// Properties inherited from plain API types (e.g. <c>CMlBrowser.CurMap</c>) must stay plain
    /// field access even when their declaring file isn't a <c>.g.cs</c> (some API types are
    /// hand-authored, not generated).
    /// </summary>
    private bool IsUserDefinedProperty(IPropertySymbol p)
    {
        var syntaxRef = p.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is not PropertyDeclarationSyntax) return false;
        if (p.ContainingType.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface) return false;
        if (p.IsLibContextProperty()) return false;
        var isOwnClass = SymbolEqualityComparer.Default.Equals(p.ContainingType, _ctx.Info.Symbol);
        if (!isOwnClass && !TypeMapper.IsContextOrLibType(p.ContainingType)) return false;
        var path = syntaxRef.SyntaxTree.FilePath;
        return !path.EndsWith(".g.cs", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns true when <paramref name="sym"/> is the <c>Value</c> member of <c>StrongBox&lt;T&gt;</c>.</summary>
    private static bool IsStrongBoxMember(ISymbol? sym) =>
        sym is (IFieldSymbol or IPropertySymbol) and { Name: "Value", ContainingType: { Name: "StrongBox" } };

    /// <summary>Returns true when <paramref name="expr"/> is an identifier that maps to a declare-for local.</summary>
    private bool IsStrongBoxReceiver(ExpressionSyntax expr) =>
        expr is IdentifierNameSyntax id
        && _ctx.DeclareForLocals.ContainsKey(id.Identifier.Text);

    // ------- Invocations -------

    private string TranslateInvocation(InvocationExpressionSyntax inv)
    {
        var callee = inv.Expression;
        var sym = _ctx.Model.GetSymbolInfo(callee).Symbol as IMethodSymbol;

        // `var item = () => Items[i]; item()` is an alias declaration lowered by
        // StatementEmitter. ManiaScript aliases are accessed directly rather than invoked.
        if (callee is IdentifierNameSyntax aliasId
            && inv.ArgumentList.Arguments.Count == 0
            && _ctx.AliasLambdaLocals.TryGetValue(aliasId.Identifier.Text, out var alias))
            return alias;

        // IContext.Main()/Loop() are invoked only by the generated main() wrapper.
        if (sym.IsIContextEntryPoint())
            _ctx.Report(Diagnostics.ContextEntryPointCalledDirectly, inv.GetLocation(), sym!.Name);

        // ManiaScriptSharp.ManiaScript.* helpers (also via `using static`).
        if (sym is not null && sym.ContainingType?.ToDisplayString() == "ManiaScriptSharp.ManiaScript")
        {
            var n = sym.Name.ToLowerInvariant();
            return n == "yield" ? "yield" : $"{n}({Args(inv.ArgumentList)})";
        }

        // System.Math / System.MathF static methods → MathLib:: calls.
        if (sym is not null && sym.ContainingType?.ToDisplayString() is "System.Math" or "System.MathF")
            return MapMathCall(sym, inv);

        // System.String instance and static methods → TextLib:: calls.
        if (sym is not null && sym.ContainingType?.SpecialType == SpecialType.System_String)
        {
            var mapped = MapStringCall(sym, inv);
            if (mapped is not null) return mapped;
        }

        // int.Parse / float.Parse → TextLib::ToInteger / TextLib::ToReal.
        if (sym is { IsStatic: true, Name: "Parse" } && inv.ArgumentList.Arguments.Count == 1
            && sym.ContainingType?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Single)
        {
            var arg = Translate(inv.ArgumentList.Arguments[0].Expression);
            return sym.ContainingType.SpecialType == SpecialType.System_Int32
                ? $"TextLib::ToInteger({arg})"
                : $"TextLib::ToReal({arg})";
        }

        // System.Diagnostics.Debug.Assert → assert(cond[, "msg"])
        if (sym is { Name: "Assert", ContainingType: { } ct }
            && ct.ToDisplayString() == "System.Diagnostics.Debug")
        {
            return $"assert({Args(inv.ArgumentList)})";
        }

        // ToString() on any type → "" ^ x; ManiaScript's `^` operator auto-converts every type.
        // (Static `Convert.ToString(x)` is handled below via MapConvertCall, not here.)
        if (sym is { IsStatic: false, Name: "ToString" } && callee is MemberAccessExpressionSyntax tsMa)
            return $"\"\" ^ {Translate(tsMa.Expression)}";

        // System.Convert.ToXxx(value) → TextLib::/MathLib:: conversions (same table as explicit casts).
        if (sym is not null && sym.ContainingType?.ToDisplayString() == "System.Convert")
        {
            var mapped = MapConvertCall(sym, inv);
            if (mapped is not null) return mapped;
        }

        // Console.Write/WriteLine → log
        if (callee is MemberAccessExpressionSyntax cw &&
            cw.Expression.ToString() == "Console" &&
            cw.Name.Identifier.Text is "WriteLine" or "Write")
        {
            return $"log({Args(inv.ArgumentList)})";
        }

        // List API mapping.
        if (callee is MemberAccessExpressionSyntax listMa && sym is not null)
        {
            var mapped = MapListMethod(sym, listMa, inv);
            if (mapped is not null) return mapped;
        }

        // LINQ in expression context: must be assigned to a local variable so
        // LinqChainEmitter can desugar the chain to a foreach loop at statement level.
        if (sym is not null && IsLinqMethod(sym))
        {
            _ctx.Report(Diagnostics.UnsupportedLinq, inv.GetLocation(),
                $"{sym.Name} (assign to a local variable to enable LINQ desugaring)");
            return $"/* LINQ:{sym.Name} */";
        }

        // Label call site → +++Name+++
        if (sym is not null && _ctx.IsLabelMethod(sym) && callee is IdentifierNameSyntax)
            return $"+++{sym.Name}+++";

        return $"{Translate(callee)}({Args(inv.ArgumentList)})";
    }

    private string? MapListMethod(IMethodSymbol m, MemberAccessExpressionSyntax ma, InvocationExpressionSyntax inv)
    {
        var args = inv.ArgumentList;
        // Dictionary.GetValueOrDefault is an extension method, so its declaring type is not
        // the dictionary. Detect that one special case from the receiver without intercepting
        // unrelated LINQ extension methods.
        var receiverType = _ctx.Model.GetTypeInfo(ma.Expression).Type as INamedTypeSymbol;
        var isDictionaryGetValueOrDefault = m.Name == "GetValueOrDefault" && IsDictionaryType(receiverType);
        if (!IsListLikeType(m.ContainingType) && !IsDictionaryType(m.ContainingType)
            && !isDictionaryGetValueOrDefault) return null;

        if (m.Name == "Remove" && IsListLikeType(m.ContainingType)
            && m.ContainingType.TypeArguments.Length > 0
            && IsCompositeListValue(m.ContainingType.TypeArguments[0]))
        {
            _ctx.Report(Diagnostics.RemoveCompositeListValue, inv.GetLocation());
            return "/* List.Remove(value) cannot remove list or struct values */";
        }

        var valueType = m.Name switch
        {
            "Contains" when IsListLikeType(m.ContainingType)
                && m.ContainingType.TypeArguments.Length > 0 => m.ContainingType.TypeArguments[0],
            "ContainsValue" when IsDictionaryType(m.ContainingType)
                && m.ContainingType.TypeArguments.Length > 1 => m.ContainingType.TypeArguments[1],
            _ => null,
        };
        if (valueType is not null && IsCompositeListValue(valueType))
        {
            _ctx.Report(Diagnostics.ContainsCompositeValue, inv.GetLocation());
            return "/* Contains(value) cannot check list or struct values */";
        }

        var recv = Translate(ma.Expression);
        var a = Args(args);
        return m.Name switch
        {
            "Add" => $"{recv}.add({a})",
            "Insert" when args.Arguments.Count == 2 => $"{recv}.addfirst({Translate(args.Arguments[1].Expression)})",
            "RemoveAt" => $"{recv}.removekey({a})",
            "Remove" when IsDictionaryType(m.ContainingType) => $"{recv}.removekey({a})",
            "Remove" => $"{recv}.remove({a})",
            "Clear" => $"{recv}.clear()",
            "Contains" => $"{recv}.exists({a})",
            "ContainsKey" => $"{recv}.existskey({a})",
            "ContainsValue" => $"{recv}.exists({a})",
            "IndexOf" => $"{recv}.keyof({a})",
            "GetValueOrDefault" when IsDictionaryType(receiverType) && args.Arguments.Count == 2 => $"{recv}.get({a})",
            "Sort" or "OrderBy" => $"{recv}.sort()",
            "Reverse" or "OrderByDescending" => $"{recv}.sortreverse()",
            _ => null,
        };
    }

    private static bool IsCompositeListValue(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];

        return type is IArrayTypeSymbol
            || type is INamedTypeSymbol named
                && (IsListLikeType(named) || IsDictionaryType(named)
                    || named.TypeKind == TypeKind.Struct && named.SpecialType == SpecialType.None
                        && TypeMapper.Map(named) is not ("Ident" or "Vec2" or "Vec3" or "Int2" or "Int3"));
    }

    private string Args(ArgumentListSyntax args)
        => string.Join(", ", args.Arguments.Select(a => a.NameColon is { } nc
            ? $"/* {nc.Name.Identifier.Text}: */ {Translate(a.Expression)}"
            : Translate(a.Expression)));

    // ------- Binary / postfix / assignment / element access -------

    private string TranslateBinary(BinaryExpressionSyntax bin)
    {
        // `as` cast
        if (bin.IsKind(SyntaxKind.AsExpression))
        {
            var typeSym = _ctx.Model.GetTypeInfo(bin.Right).Type;
            return $"({Translate(bin.Left)} as {TypeMapper.Map(typeSym)})";
        }

        var left = Translate(bin.Left);
        var right = Translate(bin.Right);
        var op = bin.OperatorToken.Text;

        if (op == "+" && (IsStringType(bin.Left) || IsStringType(bin.Right)))
            op = "^";

        return $"{left} {op} {right}";
    }

    private string TranslateAssignment(AssignmentExpressionSyntax asg)
    {
        var op = asg.OperatorToken.Text;

        if (op == "=")
            return TranslateAssignTo(asg.Left, Translate(asg.Right));

        // ManiaScript has no inline conditional, so `x ??= y` can only be expressed as the
        // statement-level if/else rewrite in StatementEmitter; bail out anywhere else.
        if (op == "??=")
            return Unsupported(asg, "'??=' (only supported as a top-level statement)");

        // Compound assignment (+=, -=, …) must fall through to normal emit since
        // ManiaScript has no compound-setter syntax.
        // [Alias] receiver → ManiaScript `<=>` operator.
        if (asg.Left is IdentifierNameSyntax lid)
        {
            var sym = _ctx.Model.GetSymbolInfo(lid).Symbol;
            if (sym is not null && sym.HasAttr("AliasAttribute")) op = "<=>";
        }
        return $"{Translate(asg.Left)} {op} {Translate(asg.Right)}";
    }

    /// <summary>
    /// Builds a `left = valueText`-equivalent assignment, routing through the property
    /// setter call or `&lt;=&gt;` alias operator when applicable. Shared by plain assignment
    /// translation and the statement-level ternary/`??=` if-else rewrites.
    /// </summary>
    public string TranslateAssignTo(ExpressionSyntax left, string valueText)
    {
        if (left is IdentifierNameSyntax lid2)
        {
            var lsym = _ctx.Model.GetSymbolInfo(lid2).Symbol;
            if (lsym is IPropertySymbol lp && !lp.HasAttr("ManialinkControlAttribute")
                && lp.SetMethod is not null && IsUserDefinedProperty(lp))
                return $"{NameMangler.Setter(lp)}({valueText})";
        }
        if (left is MemberAccessExpressionSyntax lma)
        {
            var lsym = _ctx.Model.GetSymbolInfo(lma).Symbol;
            // Only user-defined properties (with a C# body) translate to a Set*() call — a
            // plain auto-generated API property (e.g. CUILayer.IsVisible) is a real ManiaScript
            // field and must stay a plain assignment.
            if (lsym is IPropertySymbol lp && !lp.HasAttr("ManialinkControlAttribute")
                && lp.SetMethod is not null && IsUserDefinedProperty(lp))
            {
                var recv = Translate(lma.Expression);
                // `::` is reserved for library/enum/type-qualified receivers; any other
                // receiver (e.g. a plain instance) calls the Set*() function via `.`.
                var recvSym = _ctx.Model.GetSymbolInfo(lma.Expression).Symbol;
                var recvIsLib = recvSym is IFieldSymbol recvField && recvField.IsLibImplementation();
                var recvIsType = recvSym is INamedTypeSymbol;
                if (!_ctx.IsManialink && recvSym is INamedTypeSymbol recvLibType
                    && _ctx.TryGetLibraryAlias(recvLibType, out var includeAlias))
                    recv = includeAlias;
                if (_ctx.IsManialink && recvIsLib)
                    return $"{NameMangler.Setter(lp)}({valueText})";
                var sep = (recvIsLib || recvIsType) ? "::" : ".";
                return $"{recv}{sep}{NameMangler.Setter(lp)}({valueText})";
            }
        }

        var op = "=";
        if (left is IdentifierNameSyntax lid)
        {
            var sym = _ctx.Model.GetSymbolInfo(lid).Symbol;
            if (sym is not null && sym.HasAttr("AliasAttribute")) op = "<=>";
        }
        return $"{Translate(left)} {op} {valueText}";
    }

    private string TranslatePrefix(PrefixUnaryExpressionSyntax prefix)
    {
        if (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression))
        {
            var op = prefix.IsKind(SyntaxKind.PreIncrementExpression) ? "+=" : "-=";
            return $"{Translate(prefix.Operand)} {op} 1";
        }
        return $"{prefix.OperatorToken.Text}{Translate(prefix.Operand)}";
    }

    private string TranslatePostfix(PostfixUnaryExpressionSyntax post)
    {
        if (post.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            return $"{Translate(post.Operand)}/* not Null here */";

        if (post.OperatorToken.Text is "++" or "--")
        {
            var op = post.OperatorToken.Text == "++" ? "+=" : "-=";
            return $"{Translate(post.Operand)} {op} 1";
        }
        return $"{Translate(post.Operand)}{post.OperatorToken.Text}";
    }

    private string TranslateElementAccess(ElementAccessExpressionSyntax ea)
    {
        var a = string.Join(", ", ea.ArgumentList.Arguments.Select(arg => Translate(arg.Expression)));
        return $"{Translate(ea.Expression)}[{a}]";
    }

    // ------- Casts -------

    /// <summary>
    /// A C# explicit cast between two ManiaScript basic types (<c>Boolean</c>/<c>Integer</c>/
    /// <c>Real</c>/<c>Text</c>) has no direct syntax in ManiaScript — it must go through a
    /// TextLib/MathLib conversion call (or a no-op passthrough for same-family types).
    /// Casts that don't fall into a recognised basic-type pair (e.g. class downcasts) keep
    /// using ManiaScript's `as` operator.
    /// </summary>
    private string TranslateCast(CastExpressionSyntax cast)
    {
        var exprText = Translate(cast.Expression);
        var targetType = _ctx.Model.GetTypeInfo(cast.Type).Type;
        var from = Categorize(_ctx.Model.GetTypeInfo(cast.Expression).Type);
        var to = Categorize(targetType);

        if (from != Prim.None && to != Prim.None)
        {
            var converted = PrimitiveConversion(from, to, exprText, roundNarrowing: false);
            if (converted is not null) return converted;
            return Unsupported(cast, $"cast from {from} to {to} (ManiaScript has no boolean-to-number conversion; extract to an if/else assigning 1/0 explicitly)");
        }

        return $"({exprText} as {TypeMapper.Map(targetType)})";
    }

    /// <summary>ManiaScript basic-type family a C# type maps to, for cast/Convert translation.</summary>
    private enum Prim { None, Boolean, Integer, Real, Text }

    private static Prim Categorize(ITypeSymbol? t) => EnumSupport.IsCustomEnum(t) ? Prim.Integer : t?.SpecialType switch
    {
        SpecialType.System_Boolean => Prim.Boolean,
        SpecialType.System_Byte or SpecialType.System_SByte or
        SpecialType.System_Int16 or SpecialType.System_UInt16 or
        SpecialType.System_Int32 or SpecialType.System_UInt32 or
        SpecialType.System_Int64 or SpecialType.System_UInt64 => Prim.Integer,
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => Prim.Real,
        SpecialType.System_String => Prim.Text,
        _ => Prim.None,
    };

    /// <summary>
    /// Builds the ManiaScript expression converting <paramref name="expr"/> from one basic-type
    /// family to another. Same-family conversions (e.g. <c>long</c> → <c>int</c>) are a no-op
    /// since ManiaScript has only one Integer/Real type. <paramref name="roundNarrowing"/>
    /// selects Real→Integer semantics: <see langword="false"/> truncates toward zero (matches a
    /// C# explicit cast), <see langword="true"/> rounds to nearest (matches <c>Convert.ToInt32</c>
    /// and friends). Returns <see langword="null"/> for Boolean→Integer/Real: ManiaScript has no
    /// ternary operator and no built-in boolean-to-number function, so there is no expression form.
    /// </summary>
    private static string? PrimitiveConversion(Prim from, Prim to, string expr, bool roundNarrowing)
    {
        if (from == to) return expr;
        return (from, to) switch
        {
            (Prim.Integer, Prim.Real) => $"MathLib::ToReal({expr})",
            (Prim.Real, Prim.Integer) => roundNarrowing ? $"MathLib::NearestInteger({expr})" : $"MathLib::TruncInteger({expr})",
            (Prim.Boolean, Prim.Integer) or (Prim.Boolean, Prim.Real) => null,
            (Prim.Integer, Prim.Boolean) => $"({expr} != 0)",
            (Prim.Real, Prim.Boolean) => $"({expr} != 0.)",
            (Prim.Integer, Prim.Text) or (Prim.Real, Prim.Text) or (Prim.Boolean, Prim.Text) => $"TextLib::ToText({expr})",
            (Prim.Text, Prim.Integer) => $"TextLib::ToInteger({expr})",
            (Prim.Text, Prim.Real) => $"TextLib::ToReal({expr})",
            (Prim.Text, Prim.Boolean) => $"({expr} == \"True\")",
            _ => expr,
        };
    }

    /// <summary>
    /// Maps a <c>System.Convert.ToXxx(value)</c> call onto the same basic-type conversion
    /// table as <see cref="TranslateCast"/>, using rounding (not truncation) for Real→Integer
    /// narrowing to match <c>Convert.ToInt32</c> semantics. Returns <see langword="null"/> when
    /// either side isn't a recognised ManiaScript basic type, so the caller falls back to the
    /// default translation.
    /// </summary>
    private string? MapConvertCall(IMethodSymbol method, InvocationExpressionSyntax inv)
    {
        if (inv.ArgumentList.Arguments.Count != 1) return null;

        var to = method.Name switch
        {
            "ToBoolean" => Prim.Boolean,
            "ToByte" or "ToSByte" or "ToInt16" or "ToUInt16" or
            "ToInt32" or "ToUInt32" or "ToInt64" or "ToUInt64" => Prim.Integer,
            "ToSingle" or "ToDouble" or "ToDecimal" => Prim.Real,
            "ToString" => Prim.Text,
            _ => Prim.None,
        };
        if (to == Prim.None) return null;

        var argExpr = inv.ArgumentList.Arguments[0].Expression;
        var from = Categorize(_ctx.Model.GetTypeInfo(argExpr).Type);
        if (from == Prim.None) return null;

        var converted = PrimitiveConversion(from, to, Translate(argExpr), roundNarrowing: true);
        if (converted is not null) return converted;
        return Unsupported(inv, $"Convert.{method.Name} from {from} (ManiaScript has no boolean-to-number conversion; extract to an if/else assigning 1/0 explicitly)");
    }

    // ------- Strings -------

    private string TranslateInterpolatedString(InterpolatedStringExpressionSyntax istr)
    {
        // Raw string literals ($"""...""") → ManiaScript multiline strings with {{{expr}}} placeholders.
        // Regular interpolated strings ($"...") → "literal" ^ expr ^ "continuation" concatenation.
        bool isRaw = istr.StringStartToken.Text.Contains("\"\"\"")
                     || istr.StringStartToken.Text.Contains("@");

        if (isRaw)
        {
            var content = new StringBuilder();
            foreach (var c in istr.Contents)
            {
                switch (c)
                {
                    case InterpolatedStringTextSyntax t:
                        content.Append(t.TextToken.ValueText);
                        break;
                    case InterpolationSyntax i:
                        content.Append("{{{").Append(Translate(i.Expression)).Append("}}}");
                        break;
                }
            }

            var contentText = content.ToString();
            if (Encoding.UTF8.GetByteCount(contentText) <= MaxMultilineStringBytes
                && contentText.IndexOf("\"\"\"", StringComparison.Ordinal) < 0
                && !istr.Contents.OfType<InterpolatedStringTextSyntax>()
                    .Any(t => t.TextToken.ValueText.IndexOf("{{{", StringComparison.Ordinal) >= 0)
                && (content.Length == 0 || content[content.Length - 1] != '"'))
                return "\"\"\"" + content + "\"\"\"";

            var parts = new List<string>();
            foreach (var c in istr.Contents)
            {
                switch (c)
                {
                    case InterpolatedStringTextSyntax t when t.TextToken.ValueText.Length > 0:
                        parts.Add(FormatMultilineString(t.TextToken.ValueText));
                        break;
                    case InterpolationSyntax i:
                        parts.Add("\"\"\"{{{" + Translate(i.Expression) + "}}}\"\"\"");
                        break;
                }
            }
            return parts.Count == 0 ? FormatMultilineString("") : string.Join(" ^ ", parts);
        }
        else
        {
            // Build a ^ -concatenated expression.
            var parts = new List<string>();
            var textAccum = new StringBuilder();

            void FlushText()
            {
                if (textAccum.Length > 0)
                {
                    parts.Add("\"" + textAccum.ToString() + "\"");
                    textAccum.Clear();
                }
            }

            foreach (var c in istr.Contents)
            {
                switch (c)
                {
                    case InterpolatedStringTextSyntax t:
                        // TextToken.ValueText gives the unescaped content.
                        textAccum.Append(t.TextToken.ValueText);
                        break;
                    case InterpolationSyntax i:
                        FlushText();
                        parts.Add(Translate(i.Expression));
                        break;
                }
            }
            FlushText();

            if (parts.Count == 0) return "\"\"";
            return string.Join(" ^ ", parts);
        }
    }

    private const int MaxMultilineStringBytes = 65_535;

    /// <summary>
    /// Emits a ManiaScript multiline string expression whose UTF-8 content does not exceed the
    /// 65,535-byte per-literal limit. Sequences that would be parsed as a delimiter or
    /// interpolation are represented by ordinary quoted fragments instead.
    /// </summary>
    private static string FormatMultilineString(string text)
    {
        if (text.Length == 0) return "\"\"\"\"\"\"";

        var parts = new List<string>();
        var literal = new StringBuilder();
        var literalByteCount = 0;

        void FlushLiteral()
        {
            if (literal.Length == 0) return;

            // A quote immediately before the closing delimiter would form an ambiguous quote
            // run. Keep trailing quotes in a regular literal, where they are unambiguous.
            var trailingQuotes = 0;
            while (literal.Length > 0 && literal[literal.Length - 1] == '"')
            {
                literal.Length--;
                trailingQuotes++;
            }

            if (literal.Length > 0)
                parts.Add("\"\"\"" + literal + "\"\"\"");
            if (trailingQuotes > 0)
                parts.Add(QuoteText(new string('"', trailingQuotes)));
            literal.Clear();
            literalByteCount = 0;
        }

        for (var index = 0; index < text.Length;)
        {
            if (index + 3 <= text.Length
                && (string.Compare(text, index, "\"\"\"", 0, 3, StringComparison.Ordinal) == 0
                    || string.Compare(text, index, "{{{", 0, 3, StringComparison.Ordinal) == 0))
            {
                FlushLiteral();
                parts.Add(QuoteText(text.Substring(index, 3)));
                index += 3;
                continue;
            }

            var charCount = char.IsHighSurrogate(text[index])
                && index + 1 < text.Length
                && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
            var nextByteCount = Encoding.UTF8.GetByteCount(text.Substring(index, charCount));
            if (literalByteCount > 0 && literalByteCount + nextByteCount > MaxMultilineStringBytes)
                FlushLiteral();

            literal.Append(text, index, charCount);
            literalByteCount += nextByteCount;
            index += charCount;
        }
        FlushLiteral();

        return string.Join(" ^ ", parts);
    }

    private static string QuoteText(string text) => "\"" + text
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t") + "\"";

    // ------- new ... -------

    private string TranslateObjectCreation(ObjectCreationExpressionSyntax creation)
    {
        // TypeInfo is attached to the construction expression. Looking it up on the TypeSyntax
        // loses the symbol for nested/user-defined structs in some semantic-model contexts.
        var typeSym = _ctx.Model.GetTypeInfo(creation).Type;
        return TranslateNew(typeSym, creation.ArgumentList, creation.Initializer, creation.Type.ToString());
    }

    private string TranslateImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax ioc)
    {
        var typeSym = _ctx.Model.GetTypeInfo(ioc).Type;
        return TranslateNew(typeSym, ioc.ArgumentList, ioc.Initializer, typeSym?.Name ?? "?");
    }

    private string TranslateNew(ITypeSymbol? typeSym, ArgumentListSyntax? args, InitializerExpressionSyntax? init, string textualName)
    {
        // Vectors: new VecN(a, b[, c]) → <a, b[, c]>
        var name = typeSym?.Name ?? textualName;

        // Disallow instantiation of CNod-derived API classes.
        if (typeSym is INamedTypeSymbol createdType && createdType.IsCNodeDerived())
        {
            _ctx.Report(Diagnostics.CNodeInstantiation, null, name);
            return "";
        }

        if (name is "Vec2" or "Vec3" or "Int2" or "Int3" or "Vector2" or "Vector3" && args is not null && args.Arguments.Count > 0)
            return "<" + string.Join(", ", args.Arguments.Select(a => Translate(a.Expression))) + ">";

        // Collection initializer → [a, b]; dictionary initializer → ["k" => v]
        if (typeSym is INamedTypeSymbol { TypeKind: TypeKind.Struct } structType
            && init?.IsKind(SyntaxKind.ObjectInitializerExpression) == true)
            return TranslateStructInitializer(structType, init);

        if (init is not null)
            return TranslateInitializer(init);

        // Empty `new List<T>()` / `new Dictionary<K,V>()` → empty literal.
        if (typeSym is INamedTypeSymbol nts && nts.IsGenericType)
        {
            var def = nts.ConstructedFrom?.ToDisplayString();
            if (def is "System.Collections.Generic.List<T>"
                or "System.Collections.Generic.Dictionary<TKey, TValue>")
                return "[]";
        }

        // Bare new T() — usually used for struct construction; emit empty placeholder.
        return "";
    }

    private string TranslateStructInitializer(INamedTypeSymbol type, InitializerExpressionSyntax init)
    {
        var fields = init.Expressions.Select(TranslateStructInitializerField);
        var contents = string.Join(", ", fields);
        return contents.Length == 0
            ? $"{TypeMapper.Map(type)} {{}}"
            : $"{TypeMapper.Map(type)} {{ {contents} }}";
    }

    private string TranslateStructInitializerField(ExpressionSyntax expression)
    {
        if (expression is not AssignmentExpressionSyntax assignment
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            return Unsupported(expression, "struct initializer entry");

        var member = _ctx.Model.GetSymbolInfo(assignment.Left).Symbol;
        var name = member switch
        {
            IFieldSymbol { IsStatic: false, ContainingType: { TypeKind: TypeKind.Struct } } structField
                => NameMangler.StructField(structField),
            IFieldSymbol or IPropertySymbol => NameMangler.PascalCase(member.Name),
            _ => assignment.Left.ToString(),
        };
        return $"{name} = {Translate(assignment.Right)}";
    }

    private string TranslateInitializer(InitializerExpressionSyntax init)
    {
        // Dictionary entry form: { ["k"] = v }
        var hasDictEntries = init.Expressions.OfType<AssignmentExpressionSyntax>()
            .Any(a => a.Left is ImplicitElementAccessSyntax);
        if (hasDictEntries)
        {
            var items = init.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .Where(a => a.Left is ImplicitElementAccessSyntax)
                .Select(a =>
                {
                    var keyExpr = ((ImplicitElementAccessSyntax)a.Left).ArgumentList.Arguments[0].Expression;
                    return $"{Translate(keyExpr)} => {Translate(a.Right)}";
                });
            return "[" + string.Join(", ", items) + "]";
        }

        // Plain collection
        return "[" + string.Join(", ", init.Expressions.Select(Translate)) + "]";
    }

    private string TranslateCollectionExpr(CollectionExpressionSyntax ce)
        => "[" + string.Join(", ", ce.Elements.Select(e => e is ExpressionElementSyntax ee ? Translate(ee.Expression) : e.ToString())) + "]";

    // ------- Helpers -------

    private bool IsStringType(ExpressionSyntax e)
        => _ctx.Model.GetTypeInfo(e).Type?.SpecialType == SpecialType.System_String;

    private static bool IsLinqMethod(IMethodSymbol m)
        => (m.ReducedFrom ?? m).ContainingType?.ToDisplayString() == "System.Linq.Enumerable";

    private static bool IsLibType(INamedTypeSymbol type)
        => type.AllInterfaces.Any(i => i.Name == "ILib"
            && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp");

    private static bool IsUserDefinedLibType(INamedTypeSymbol? type)
        => type?.DeclaringSyntaxReferences.Any(r =>
            !r.SyntaxTree.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) == true;

    private static bool IsListLikeType(INamedTypeSymbol? t)
    {
        if (t is null) return false;
        var name = t.ConstructedFrom?.ToDisplayString() ?? t.ToDisplayString();
        return name is "System.Collections.Generic.List<T>"
            or "System.Collections.Generic.IList<T>"
            or "System.Collections.Generic.IReadOnlyList<T>"
            or "System.Collections.Generic.ICollection<T>"
            or "System.Collections.Generic.IEnumerable<T>"
            or "System.Collections.Immutable.ImmutableArray<T>";
    }

    internal static bool IsDictionaryType(INamedTypeSymbol? t)
    {
        if (t is null) return false;
        var name = t.ConstructedFrom?.ToDisplayString() ?? t.ToDisplayString();
        return name is "System.Collections.Generic.Dictionary<TKey, TValue>"
            or "System.Collections.Generic.IDictionary<TKey, TValue>"
            or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>";
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="sym"/> is the <c>Context</c>
    /// property from <c>ILib&lt;T&gt;</c>, including implicit implementations on concrete classes.
    /// </summary>
    private static bool IsLibContextAccess(ISymbol? sym)
    {
        if (sym is not IPropertySymbol prop) return false;
        return prop.IsLibContextProperty();
    }

    // ------- System.Math / System.MathF → MathLib -------

    /// <summary>
    /// Maps a <c>System.Math</c> or <c>System.MathF</c> static call to the equivalent
    /// ManiaScript <c>MathLib::</c> expression. Functions with no direct ManiaScript counterpart
    /// are derived from available primitives (change-of-base for logarithms, hyperbolic
    /// identities for Sinh/Cosh/Tanh, etc.). Where the argument expression must appear more
    /// than once in the output (Sign, Cbrt with negative values, Sinh, Cosh, Tanh, Log2,
    /// Log10, Log with explicit base), the argument sub-expression is translated twice; callers
    /// that pass complex expressions with side effects should be aware of this limitation.
    /// </summary>
    private string MapMathCall(IMethodSymbol method, InvocationExpressionSyntax inv)
    {
        var args = inv.ArgumentList;
        var a    = Args(args);
        var argc = args.Arguments.Count;

        switch (method.Name)
        {
            case "Abs":      return $"MathLib::Abs({a})";
            case "Acos":     return $"MathLib::Acos({a})";
            case "Asin":     return $"MathLib::Asin({a})";
            case "Atan" when argc == 1:
                // atan(x) = atan2(x, 1) — no single-arg Atan in MathLib.
                return $"MathLib::Atan2({a}, 1.)";
            case "Atan2":    return $"MathLib::Atan2({a})";
            case "Cbrt":
            {
                // Cube root: x^(1/3), handling negative base correctly.
                var arg = Translate(args.Arguments[0].Expression);
                return $"({arg} >= 0. ? MathLib::Pow({arg}, 0.3333333333333333) : -(MathLib::Pow(-({arg}), 0.3333333333333333)))";
            }
            case "Ceiling":  return $"MathLib::CeilingInteger({a})";
            case "Clamp":    return $"MathLib::Clamp({a})";
            case "Cos":      return $"MathLib::Cos({a})";
            case "Cosh":
            {
                // cosh(x) = (e^x + e^-x) / 2
                var arg = Translate(args.Arguments[0].Expression);
                return $"((MathLib::Exp({arg}) + MathLib::Exp(-({arg}))) / 2.)";
            }
            case "Exp":      return $"MathLib::Exp({a})";
            case "Floor":    return $"MathLib::FloorInteger({a})";
            case "Log" when argc == 1:
                return $"MathLib::Ln({a})";
            case "Log" when argc == 2:
            {
                // log_b(x) = ln(x) / ln(b) — change-of-base formula.
                var x = Translate(args.Arguments[0].Expression);
                var b = Translate(args.Arguments[1].Expression);
                return $"(MathLib::Ln({x}) / MathLib::Ln({b}))";
            }
            case "Log2":
            {
                var arg = Translate(args.Arguments[0].Expression);
                return $"(MathLib::Ln({arg}) / MathLib::Ln(2.))";
            }
            case "Log10":
            {
                var arg = Translate(args.Arguments[0].Expression);
                return $"(MathLib::Ln({arg}) / MathLib::Ln(10.))";
            }
            case "Max":      return $"MathLib::Max({a})";
            case "Min":      return $"MathLib::Min({a})";
            case "Pow":      return $"MathLib::Pow({a})";
            case "Round" when argc == 1:
                return $"MathLib::NearestInteger({a})";
            case "Sign":
            {
                // No Sign in MathLib; emit inline ternary.
                // The argument is translated twice — keep it a simple expression to avoid
                // side-effect duplication.
                var arg = Translate(args.Arguments[0].Expression);
                return $"({arg} > 0 ? 1 : ({arg} < 0 ? -1 : 0))";
            }
            case "Sin":      return $"MathLib::Sin({a})";
            case "Sinh":
            {
                // sinh(x) = (e^x - e^-x) / 2
                var arg = Translate(args.Arguments[0].Expression);
                return $"((MathLib::Exp({arg}) - MathLib::Exp(-({arg}))) / 2.)";
            }
            case "Sqrt":     return $"MathLib::Sqrt({a})";
            case "Tan":      return $"MathLib::Tan({a})";
            case "Tanh":
            {
                // tanh(x) = (e^x - e^-x) / (e^x + e^-x)
                var arg = Translate(args.Arguments[0].Expression);
                var ePos = $"MathLib::Exp({arg})";
                var eNeg = $"MathLib::Exp(-({arg}))";
                return $"(({ePos} - {eNeg}) / ({ePos} + {eNeg}))";
            }
            case "Truncate": return $"MathLib::TruncInteger({a})";
            default:
                return Unsupported(inv, $"Math.{method.Name}");
        }
    }

    // ------- System.String → TextLib -------

    /// <summary>
    /// Maps a <c>System.String</c> instance or static method call to the equivalent
    /// ManiaScript <c>TextLib::</c> expression. Returns <see langword="null"/> for methods
    /// with no applicable TextLib counterpart so the caller can fall through to the
    /// default translation path.
    /// </summary>
    private string? MapStringCall(IMethodSymbol method, InvocationExpressionSyntax inv)
    {
        var args = inv.ArgumentList;
        var argc = args.Arguments.Count;

        // ── Instance methods ──────────────────────────────────────────────────
        if (!method.IsStatic && inv.Expression is MemberAccessExpressionSyntax ma)
        {
            var recv = Translate(ma.Expression);
            switch (method.Name)
            {
                case "ToUpper" or "ToUpperInvariant":
                    return $"TextLib::ToUpperCase({recv})";

                case "ToLower" or "ToLowerInvariant":
                    return $"TextLib::ToLowerCase({recv})";

                case "Trim" when argc == 0:
                    return $"TextLib::Trim({recv})";

                case "Substring" when argc == 2:
                    return $"TextLib::SubString({recv}, {Args(args)})";

                case "Substring" when argc == 1:
                    // No end-index overload in TextLib; pass Length(recv) as the upper bound.
                    // If recv is a complex expression with side effects, prefer assigning it
                    // to a local variable before calling Substring.
                    return $"TextLib::SubString({recv}, {Translate(args.Arguments[0].Expression)}, TextLib::Length({recv}))";

                case "Contains" when argc == 1:
                    // Find(needle, haystack, formatSensitive, caseSensitive)
                    return $"TextLib::Find({Translate(args.Arguments[0].Expression)}, {recv}, True, True)";

                case "StartsWith" when argc == 1:
                    return $"TextLib::StartsWith({Translate(args.Arguments[0].Expression)}, {recv})";

                case "EndsWith" when argc == 1:
                    return $"TextLib::EndsWith({Translate(args.Arguments[0].Expression)}, {recv})";

                case "Replace" when argc == 2:
                    // TextLib::Replace(text, toReplace, replacement)
                    return $"TextLib::Replace({recv}, {Translate(args.Arguments[0].Expression)}, {Translate(args.Arguments[1].Expression)})";

                case "Split" when argc == 1:
                    // Char literals are already emitted as "x" by TranslateLiteral, so both
                    // char and string separators translate correctly here.
                    return $"TextLib::Split({Translate(args.Arguments[0].Expression)}, {recv})";
            }
            return null;
        }

        // ── Static methods ────────────────────────────────────────────────────
        if (method.IsStatic)
        {
            switch (method.Name)
            {
                case "Join" when argc == 2:
                    return $"TextLib::Join({Translate(args.Arguments[0].Expression)}, {Translate(args.Arguments[1].Expression)})";

                case "IsNullOrEmpty" when argc == 1:
                    return $"({Translate(args.Arguments[0].Expression)} == \"\")";

                case "IsNullOrWhiteSpace" when argc == 1:
                    // ManiaScript has no whitespace concept; map to empty-string check.
                    return $"({Translate(args.Arguments[0].Expression)} == \"\")";

                case "Concat" when argc >= 2:
                    return string.Join(" ^ ", args.Arguments.Select(a => Translate(a.Expression)));
            }
            return null;
        }

        return null;
    }
}
