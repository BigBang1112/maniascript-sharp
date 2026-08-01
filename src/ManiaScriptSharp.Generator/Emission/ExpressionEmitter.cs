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
/// → triple-quoted form, `^` concatenation, vector/list/dict literals, casts, patterns,
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
            PrefixUnaryExpressionSyntax pre => $"{pre.OperatorToken.Text}{Translate(pre.Operand)}",
            PostfixUnaryExpressionSyntax post => TranslatePostfix(post),
            ParenthesizedExpressionSyntax par => $"({Translate(par.Expression)})",
            ElementAccessExpressionSyntax ea => TranslateElementAccess(ea),
            InterpolatedStringExpressionSyntax istr => TranslateInterpolatedString(istr),
            CastExpressionSyntax cast => $"({Translate(cast.Expression)} as {cast.Type})",
            IsPatternExpressionSyntax isp when _patterns is not null => _patterns.TranslateAsExpression(isp),
            ObjectCreationExpressionSyntax oc => TranslateObjectCreation(oc.Type, oc.ArgumentList, oc.Initializer),
            ImplicitObjectCreationExpressionSyntax ioc => TranslateImplicitObjectCreation(ioc),
            CollectionExpressionSyntax ce => TranslateCollectionExpr(ce),
            InitializerExpressionSyntax init => TranslateInitializer(init),
            // ManiaScript has no inline conditional (?:) — only supported as a statement-level
            // if/else rewrite (see StatementEmitter.EmitTernaryAsIfElse).
            ConditionalExpressionSyntax => Unsupported(expr, "ternary operator '?:' (extract to an if/else statement)"),
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
        SyntaxKind.NullLiteralExpression => "Null",
        SyntaxKind.DefaultLiteralExpression => "Null",
        SyntaxKind.StringLiteralExpression => TranslateStringLiteral(lit),
        SyntaxKind.CharacterLiteralExpression => "\"" + lit.Token.ValueText + "\"",
        SyntaxKind.NumericLiteralExpression => TranslateNumeric(lit),
        _ => lit.Token.Text,
    };

    private static string TranslateStringLiteral(LiteralExpressionSyntax lit)
    {
        var raw = lit.Token.Text;
        // Verbatim strings @"..." become triple-quoted to avoid escape conversion.
        if (raw.StartsWith("@\""))
        {
            var inner = lit.Token.ValueText.Replace("\"\"", "\"");
            return "\"\"\"" + inner + "\"\"\"";
        }
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
                if (f.HasAttr("SettingAttribute")) return NameMangler.Setting(f);
                if (f.IsConst) return NameMangler.Const(f);
                if (f.HasAttr("ManialinkControlAttribute")) return NameMangler.PascalCase(f.Name);
                if (f.IsLibImplementation()) return NameMangler.PascalCase(f.Name);
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
                if (_ctx.LabelMethods.Contains(m.Name)) return $"+++{m.Name}+++";
                return NameMangler.Method(m);
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
        var name = m.Name.Identifier.Text;

        // Accessing .Context directly as a member on an ILib field (e.g., myLib.Context).
        if (!_ctx.IsLib && IsLibContextAccess(memberSym))
        {
            _ctx.Report(Diagnostics.LibContextAccess, m.GetLocation(), lhs);
            return lhs;
        }

        // ILib field access uses :: (namespace-scoped in ManiaScript).
        // In manialink mode, user-defined lib functions are inlined — strip the field receiver entirely.
        var leftIsLib = leftSym is IFieldSymbol libField && libField.IsLibImplementation();
        if (leftIsLib && _ctx.IsManialink)
        {
            var libType = ((IFieldSymbol)leftSym!).Type as INamedTypeSymbol;
            var isUserLib = libType?.ContainingNamespace?.ToDisplayString() != "ManiaScriptSharp";
            if (isUserLib) return m.Name.Identifier.Text;
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
        if (memberSym is IPropertySymbol prop && prop.Name == "Count" && IsListLikeType(prop.ContainingType))
            return $"{lhs}.count";

        // User-defined property read → getter call, e.g. `obj.Score` → `obj::GetScore()`.
        if (memberSym is IPropertySymbol userProp && !userProp.HasAttr("ManialinkControlAttribute")
            && userProp.GetMethod is not null
            && IsUserDefinedProperty(userProp))
            return $"{lhs}{sep}{NameMangler.Getter(userProp)}()";

        return $"{lhs}{sep}{name}";
    }

    /// <summary>
    /// Returns true when the property is defined in user source code (has a syntax reference
    /// in a non-generated file), as opposed to being an API-generated property from a
    /// <c>.g.cs</c> file or a compiled assembly.
    /// </summary>
    private static bool IsUserDefinedProperty(IPropertySymbol p)
    {
        var syntaxRef = p.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef?.GetSyntax() is not PropertyDeclarationSyntax) return false;
        if (p.ContainingType.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface) return false;
        if (p.IsLibContextProperty()) return false;
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

        // ToString on numerics → "" ^ x
        if (sym is { Name: "ToString" } && callee is MemberAccessExpressionSyntax tsMa)
        {
            var recvType = _ctx.Model.GetTypeInfo(tsMa.Expression).Type;
            if (IsNumericOrBoolText(recvType))
                return $"\"\" ^ {Translate(tsMa.Expression)}";
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
            var mapped = MapListMethod(sym, listMa, inv.ArgumentList);
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
        if (sym is not null && _ctx.LabelMethods.Contains(sym.Name) && callee is IdentifierNameSyntax)
            return $"+++{sym.Name}+++";

        return $"{Translate(callee)}({Args(inv.ArgumentList)})";
    }

    private string? MapListMethod(IMethodSymbol m, MemberAccessExpressionSyntax ma, ArgumentListSyntax args)
    {
        if (!IsListLikeType(m.ContainingType) && !IsDictionaryType(m.ContainingType)) return null;
        var recv = Translate(ma.Expression);
        var a = Args(args);
        return m.Name switch
        {
            "Add" => $"{recv}.add({a})",
            "Insert" when args.Arguments.Count == 2 => $"{recv}.addfirst({Translate(args.Arguments[1].Expression)})",
            "RemoveAt" => $"{recv}.removekey({a})",
            "Remove" => $"{recv}.remove({a})",
            "Clear" => $"{recv}.clear()",
            "Contains" => $"{recv}.exists({a})",
            "ContainsKey" => $"{recv}.existskey({a})",
            "IndexOf" => $"{recv}.keyof({a})",
            "Sort" or "OrderBy" => $"{recv}.sort()",
            "Reverse" or "OrderByDescending" => $"{recv}.sortreverse()",
            _ => null,
        };
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
            return $"({Translate(bin.Left)} as {bin.Right})";

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
                && lp.SetMethod is not null)
                return $"{NameMangler.Setter(lp)}({valueText})";
        }
        if (left is MemberAccessExpressionSyntax lma)
        {
            var lsym = _ctx.Model.GetSymbolInfo(lma).Symbol;
            if (lsym is IPropertySymbol lp && !lp.HasAttr("ManialinkControlAttribute")
                && lp.SetMethod is not null)
            {
                var recv = Translate(lma.Expression);
                return $"{recv}::{NameMangler.Setter(lp)}({valueText})";
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

    private string TranslatePostfix(PostfixUnaryExpressionSyntax post)
    {
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

    // ------- Strings -------

    private string TranslateInterpolatedString(InterpolatedStringExpressionSyntax istr)
    {
        // Raw string literals ($"""...""") → ManiaScript triple-quoted with {{{expr}}} placeholders.
        // Regular interpolated strings ($"...") → "literal" ^ expr ^ "continuation" concatenation.
        bool isRaw = istr.StringStartToken.Text.Contains("\"\"\"");

        if (isRaw)
        {
            var sb = new StringBuilder("\"\"\"");
            foreach (var c in istr.Contents)
            {
                switch (c)
                {
                    case InterpolatedStringTextSyntax t:
                        sb.Append(t.TextToken.ValueText);
                        break;
                    case InterpolationSyntax i:
                        sb.Append("{{{").Append(Translate(i.Expression)).Append("}}}");
                        break;
                }
            }
            sb.Append("\"\"\"");
            return sb.ToString();
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

    // ------- new ... -------

    private string TranslateObjectCreation(TypeSyntax typeSyntax, ArgumentListSyntax? args, InitializerExpressionSyntax? init)
    {
        var typeSym = _ctx.Model.GetTypeInfo(typeSyntax).Type;
        return TranslateNew(typeSym, args, init, typeSyntax.ToString());
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

        if (name is "Vec2" or "Vec3" or "Int3" or "Vector2" or "Vector3" && args is not null && args.Arguments.Count > 0)
            return "<" + string.Join(", ", args.Arguments.Select(a => Translate(a.Expression))) + ">";

        // Collection initializer → [a, b]; dictionary initializer → ["k" => v]
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

    private static bool IsNumericOrBoolText(ITypeSymbol? t) => t?.SpecialType switch
    {
        SpecialType.System_Boolean or
        SpecialType.System_Byte or SpecialType.System_SByte or
        SpecialType.System_Int16 or SpecialType.System_UInt16 or
        SpecialType.System_Int32 or SpecialType.System_UInt32 or
        SpecialType.System_Int64 or SpecialType.System_UInt64 or
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => true,
        _ => false,
    };

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
