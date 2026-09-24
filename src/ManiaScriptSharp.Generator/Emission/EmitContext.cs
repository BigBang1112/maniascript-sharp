using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ManiaScriptSharp.Generator.Naming;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Shared mutable state passed to every emitter. Owns the output writer, the semantic
/// model, the source-production context (for diagnostics), and bookkeeping such as the
/// virtual-method label set and the list of public-field initialisers deferred into main().
/// </summary>
internal sealed class EmitContext
{
    public ContextClassInfo Info { get; }
    public SourceProductionContext Spc { get; }
    public BuildSettings Settings { get; }
    public string RootNamespace { get; }
    public SemanticModel Model => Info.Model;

    /// <summary>Whether we are emitting a lib class (implements <c>ILib</c> or <c>ILib&lt;T&gt;</c>) rather than an <c>IContext</c> script.</summary>
    public bool IsLib => Info.IsLib;

    /// <summary>Whether the output is a Manialink XML file; ILib fields are inlined rather than #Include'd.</summary>
    public bool IsManialink => Info.IsManialink;
    public IndentedWriter W { get; }

    /// <summary>Methods recognised as labels (virtual / override) — calls become <c>+++Name+++</c>.</summary>
    public HashSet<IMethodSymbol> LabelMethods { get; } = new(SymbolEqualityComparer.Default);

    /// <summary>Returns whether <paramref name="method"/> is a registered label or overrides one.</summary>
    public bool IsLabelMethod(IMethodSymbol? method)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
            if (LabelMethods.Contains(current)) return true;
        return false;
    }

    /// <summary>Tracks <c>#Include</c> paths already emitted — shared across the consuming class and all inlined libs to prevent duplicates.</summary>
    public HashSet<string> EmittedIncludes { get; } = [];

    /// <summary>
    /// Returns the alias emitted by the consuming script's <c>#Include</c> directive for a
    /// library type. Every access to an included library must use this name: the C# type name
    /// is only the script filename and is not necessarily available in ManiaScript.
    /// </summary>
    public bool TryGetLibraryAlias(INamedTypeSymbol libraryType, out string alias)
    {
        foreach (var field in Info.Symbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsStatic || field.IsConst || !field.IsLibImplementation()) continue;
            if (!SymbolEqualityComparer.Default.Equals(field.Type, libraryType)) continue;

            // Built-in string/math lowering always uses TextLib::/MathLib::, so
            // these libraries keep canonical aliases even when fields are renamed.
            alias = libraryType.Name is "MathLib" or "TextLib"
                    && libraryType.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp"
                ? libraryType.Name
                : NameMangler.PascalCase(field.Name);
            return true;
        }

        alias = "";
        return false;
    }

    private bool? _usesMathLib;

    /// <summary>Whether emitted expressions need the built-in MathLib include.</summary>
    public bool UsesMathLib => _usesMathLib ??= DetectMathLibUsage();

    private bool? _usesTextLib;

    /// <summary>Whether emitted expressions need the built-in TextLib include.</summary>
    public bool UsesTextLib => _usesTextLib ??= DetectTextLibUsage();

    private IEnumerable<SyntaxNode> EmittedExpressions()
    {
        foreach (var node in Info.Declaration.DescendantNodes())
        {
            if (node is not InvocationExpressionSyntax
                and not MemberAccessExpressionSyntax
                and not CastExpressionSyntax)
                continue;

            // Constructors and nested classes are not emitted as part of this script.
            if (node.Ancestors().OfType<ConstructorDeclarationSyntax>().Any()
                || node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() != Info.Declaration)
                continue;

            if (node.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault() is { } variable
                && Model.GetDeclaredSymbol(variable) is IFieldSymbol { IsConst: true })
                continue;

            yield return node;
        }
    }

    private bool DetectMathLibUsage()
    {
        foreach (var node in EmittedExpressions())
        {
            if (node is InvocationExpressionSyntax invocation
                && Model.GetSymbolInfo(invocation.Expression).Symbol is IMethodSymbol method)
            {
                if (method.ContainingType?.ToDisplayString() is "System.Math" or "System.MathF")
                    return true;

                if (method.ContainingType?.ToDisplayString() == "System.Convert"
                    && invocation.ArgumentList.Arguments.Count == 1
                    && IsIntegerRealConversion(
                        Model.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type,
                        method.ReturnType))
                    return true;
            }

            if (node is MemberAccessExpressionSyntax member
                && member.Name.Identifier.ValueText is "PI" or "E" or "Tau"
                && Model.GetSymbolInfo(member.Expression).Symbol is INamedTypeSymbol type
                && type.ToDisplayString() is "System.Math" or "System.MathF")
                return true;

            if (node is CastExpressionSyntax cast
                && IsIntegerRealConversion(Model.GetTypeInfo(cast.Expression).Type,
                    Model.GetTypeInfo(cast.Type).Type))
                return true;
        }

        return false;
    }

    private bool DetectTextLibUsage()
    {
        foreach (var node in EmittedExpressions())
        {
            if (node is MemberAccessExpressionSyntax member
                && Model.GetSymbolInfo(member).Symbol is IPropertySymbol
                    { Name: "Length", ContainingType.SpecialType: SpecialType.System_String })
                return true;

            if (node is InvocationExpressionSyntax invocation
                && Model.GetSymbolInfo(invocation.Expression).Symbol is IMethodSymbol method)
            {
                var argumentCount = invocation.ArgumentList.Arguments.Count;
                if (method.ContainingType?.SpecialType == SpecialType.System_String
                    && UsesTextLibStringMethod(method, invocation.Expression, argumentCount))
                    return true;

                if (method is { IsStatic: true, Name: "Parse" }
                    && argumentCount == 1
                    && method.ContainingType?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Single)
                    return true;

                if (method.ContainingType?.ToDisplayString() == "System.Convert"
                    && argumentCount == 1
                    && IsTextConversion(
                        Model.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type,
                        method.ReturnType))
                    return true;
            }

            if (node is CastExpressionSyntax cast
                && IsTextConversion(Model.GetTypeInfo(cast.Expression).Type,
                    Model.GetTypeInfo(cast.Type).Type))
                return true;
        }

        return false;
    }

    private static bool UsesTextLibStringMethod(IMethodSymbol method, SyntaxNode callee, int argumentCount)
    {
        if (method.IsStatic)
            return method.Name == "Join" && argumentCount == 2;

        if (callee is not MemberAccessExpressionSyntax) return false;
        return method.Name switch
        {
            "ToUpper" or "ToUpperInvariant" or "ToLower" or "ToLowerInvariant" => true,
            "Trim" => argumentCount == 0,
            "Substring" => argumentCount is 1 or 2,
            "Contains" or "StartsWith" or "EndsWith" or "Split" => argumentCount == 1,
            "Replace" => argumentCount == 2,
            _ => false,
        };
    }

    private static bool IsIntegerRealConversion(ITypeSymbol? source, ITypeSymbol? target)
    {
        var from = PrimitiveFamily(source);
        var to = PrimitiveFamily(target);
        return (from == "Integer" && to == "Real") || (from == "Real" && to == "Integer");
    }

    private static bool IsTextConversion(ITypeSymbol? source, ITypeSymbol? target)
    {
        var from = PrimitiveFamily(source);
        var to = PrimitiveFamily(target);
        return (from == "Text" && to is "Integer" or "Real")
            || (to == "Text" && from is "Boolean" or "Integer" or "Real");
    }

    private static string? PrimitiveFamily(ITypeSymbol? type) => EnumSupport.IsCustomEnum(type)
        ? "Integer"
        : type?.SpecialType switch
        {
            SpecialType.System_Boolean => "Boolean",
            SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 => "Integer",
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => "Real",
            SpecialType.System_String => "Text",
            _ => null,
        };

    private Dictionary<INamedTypeSymbol, string>? _importedLibraryStructs;

    /// <summary>C# using aliases that explicitly request a ManiaScript nested-struct import.</summary>
    public IReadOnlyDictionary<INamedTypeSymbol, string> ImportedLibraryStructs
        => _importedLibraryStructs ??= CollectImportedLibraryStructs();

    /// <summary>Maps a type using the include alias, unless an explicit C# using alias imports it locally.</summary>
    public string MapType(ITypeSymbol? type) => TypeMapper.Map(type, ResolveLibraryStructName);

    private string? ResolveLibraryStructName(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Struct || type.ContainingType is not { } owner
            || !owner.AllInterfaces.Any(i => i.Name == "ILib"
                && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp"))
            return null;

        // A library's own structs, and structs from libraries inlined into a manialink,
        // are declared in this script rather than reached through an include alias.
        if (SymbolEqualityComparer.Default.Equals(owner, Info.Symbol)
            || IsInlinedLibrary(owner))
            return type.Name;

        if (!TryGetLibraryAlias(owner, out var libraryAlias)) return null;
        return ImportedLibraryStructs.TryGetValue(type, out var localName)
            ? localName
            : $"{libraryAlias}::{type.Name}";
    }

    private bool IsInlinedLibrary(INamedTypeSymbol libraryType)
        => IsManialink && libraryType.ContainingNamespace?.ToDisplayString() != "ManiaScriptSharp";

    private Dictionary<INamedTypeSymbol, string> CollectImportedLibraryStructs()
    {
        var result = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        var root = Info.Declaration.SyntaxTree.GetRoot() as CompilationUnitSyntax;
        if (root is null) return result;

        // Global aliases can be declared in another source file. Ordinary aliases are
        // visible only from this file or an enclosing namespace declaration.
        foreach (var tree in Model.Compilation.SyntaxTrees)
        {
            if (tree.GetRoot() is not CompilationUnitSyntax compilationUnit) continue;
            var model = Model.Compilation.GetSemanticModel(tree);
            foreach (var directive in compilationUnit.Usings.Where(u => u.GlobalKeyword.RawKind != 0))
                AddAlias(directive, model);
        }

        foreach (var directive in root.Usings.Where(u => u.GlobalKeyword.RawKind == 0))
            AddAlias(directive, Model);

        foreach (var namespaceDeclaration in Info.Declaration.Ancestors()
                     .OfType<BaseNamespaceDeclarationSyntax>().Reverse())
            foreach (var directive in namespaceDeclaration.Usings)
                AddAlias(directive, Model);

        return result;

        void AddAlias(UsingDirectiveSyntax directive, SemanticModel model)
        {
            if (directive.Alias is null
                || model.GetDeclaredSymbol(directive) is not IAliasSymbol
                    { Target: INamedTypeSymbol { TypeKind: TypeKind.Struct, ContainingType: { } owner } target }
                || !TryGetLibraryAlias(owner, out _)
                || IsInlinedLibrary(owner))
                return;

            result[target] = directive.Alias.Name.Identifier.ValueText;
        }
    }

    /// <summary>Field-initialiser statements that must run inside <c>main()</c> rather than at declaration.</summary>
    public List<DeferredInit> DeferredInits { get; } = [];

    /// <summary>Manialink-control bindings that <c>main()</c> must wire up via <c>Page.GetFirstChild</c>.</summary>
    public List<ManialinkBinding> ManialinkBindings { get; } = [];

    /// <summary>
    /// When set, <see cref="StatementEmitter"/> injects event-loop handling into any manually
    /// written <c>foreach (… in PendingEvents)</c> in Loop() instead of auto-generating one.
    /// </summary>
    public Action? EventLoopInjector { get; set; }

    /// <summary>Set to <c>true</c> by <see cref="StatementEmitter"/> when it performed a manual-foreach injection.</summary>
    public bool EventLoopWasInjected { get; set; }

    /// <summary>
    /// When <c>true</c>, <see cref="StatementEmitter"/> translates a bare <c>return;</c> as <c>continue;</c>.
    /// Set while emitting <c>Loop()</c> inside the generated unconditional <c>while (True)</c> loop.
    /// </summary>
    public bool ReturnIsContinue { get; set; }

    private readonly Stack<(bool IsWhile, IReadOnlyList<string>? ContinueIncrements)> _continueLoopTargets = [];

    /// <summary>Whether a currently emitted <c>continue</c> targets a ManiaScript <c>while</c> loop.</summary>
    public bool ContinueTargetsWhile => _continueLoopTargets.Count > 0 && _continueLoopTargets.Peek().IsWhile;

    /// <summary>Increment statements to emit before a <c>continue</c> targeting the current loop.</summary>
    public IReadOnlyList<string>? ContinueIncrements => _continueLoopTargets.Count > 0
        ? _continueLoopTargets.Peek().ContinueIncrements
        : null;

    public void PushContinueLoopTarget(bool isWhile, IReadOnlyList<string>? continueIncrements = null)
        => _continueLoopTargets.Push((isWhile, continueIncrements));

    public void PopContinueLoopTarget() => _continueLoopTargets.Pop();

    /// <summary>
    /// Maps C# out-var local names (as declared in <c>Persistent/Local/Metadata/Netwrite/Netread&lt;T&gt;.For()</c>)
    /// to their ManiaScript variable name (including prefix such as <c>Persistent_</c>, <c>Net_</c>).
    /// </summary>
    public Dictionary<string, string> DeclareForLocals { get; } = [];

    /// <summary>
    /// Maps zero-argument C# lambda locals to ManiaScript aliases. Invoking one of these locals
    /// reads the alias directly because ManiaScript aliases are values, not functions.
    /// </summary>
    public Dictionary<string, string> AliasLambdaLocals { get; } = [];

    /// <summary>
    /// Backing globals for <c>OnChange(value, oldValue => { ... })</c> calls, collected by
    /// <see cref="OnChangeCollector"/> before <see cref="GlobalEmitter"/> runs. Keyed by the
    /// generated global name (e.g. <c>OldScore</c>), value is its ManiaScript-mapped type.
    /// </summary>
    public Dictionary<string, ITypeSymbol> OnChangeGlobals { get; } = [];

    /// <summary>
    /// C# local variables whose initializers are lazy LINQ chains with no materialising terminal.
    /// The stored expression is the raw C# LINQ invocation chain (e.g. <c>source.Where(pred)</c>).
    /// When such a variable is later used as the source of a terminating LINQ chain, its stages are
    /// inlined at that point by <see cref="LinqChainEmitter"/> so a single foreach loop is emitted.
    /// Using a pending variable in any other context emits <see cref="Diagnostics.UnsupportedLinq"/>.
    /// </summary>
    public Dictionary<string, ExpressionSyntax> PendingLinqChains { get; } = [];

    /// <summary>
    /// Maps a <c>foreach (var pair in dict)</c> loop variable name to its synthesized ManiaScript
    /// Key/Value names (e.g. <c>PairKey</c>/<c>PairValue</c>), so <c>pair.Key</c>/<c>pair.Value</c>
    /// in the loop body translate to the corresponding bare identifier.
    /// </summary>
    public Dictionary<string, (string KeyName, string ValueName)> DictPairLocals { get; } = [];

    private int _tupleTempCounter;

    /// <summary>Next unique temporary name for lowering tuple deconstruction assignment (e.g. swaps).</summary>
    public string NextTupleTempName() => $"TupleTmp{++_tupleTempCounter}";

    private readonly bool _hasSpc;

    internal bool HasSourceProductionContext => _hasSpc;

    public EmitContext(ContextClassInfo info, SourceProductionContext spc, BuildSettings settings, string rootNamespace = "")
    {
        Info = info;
        Spc = spc;
        Settings = settings;
        RootNamespace = rootNamespace;
        W = new IndentedWriter(settings.UseSpaces, settings.IndentSize);
        _hasSpc = true;
    }

    /// <summary>Constructor for use in tests where no <see cref="SourceProductionContext"/> is available.</summary>
    internal EmitContext(ContextClassInfo info, BuildSettings settings, string rootNamespace = "")
    {
        Info = info;
        Settings = settings;
        RootNamespace = rootNamespace;
        W = new IndentedWriter(settings.UseSpaces, settings.IndentSize);
        // _hasSpc stays false → Report() is a no-op
    }

    /// <summary>Diagnostics reported via <see cref="Report"/>, kept regardless of <c>SourceProductionContext</c> availability so tests can assert on them.</summary>
    public List<Diagnostic> ReportedDiagnostics { get; } = [];

    public void Report(DiagnosticDescriptor d, Location? loc, params object?[] args)
    {
        var diagnostic = Diagnostic.Create(d, loc ?? Location.None, args);
        ReportedDiagnostics.Add(diagnostic);
        if (_hasSpc)
            Spc.ReportDiagnostic(diagnostic);
    }
}

internal readonly struct DeferredInit
{
    public string Name { get; }
    public ExpressionSyntax Value { get; }
    public DeferredInit(string name, ExpressionSyntax value) { Name = name; Value = value; }
}

internal readonly struct ManialinkBinding
{
    public string FieldName { get; }
    public string XmlId { get; }
    public string TypeName { get; }
    public bool IgnoreValidation { get; }
    public Location? SymbolLocation { get; }
    public ManialinkBinding(string field, string xmlId, string typeName, bool ignoreValidation = false, Location? symbolLocation = null)
    {
        FieldName = field; XmlId = xmlId; TypeName = typeName; IgnoreValidation = ignoreValidation; SymbolLocation = symbolLocation;
    }
}
