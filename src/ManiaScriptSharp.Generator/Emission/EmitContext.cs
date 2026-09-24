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

            alias = NameMangler.PascalCase(field.Name);
            return true;
        }

        alias = "";
        return false;
    }

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
