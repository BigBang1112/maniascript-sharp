using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    public SemanticModel Model => Info.Model;

    /// <summary>Whether we are emitting a lib class (implements <c>ILib&lt;T&gt;</c>) rather than an <c>IContext</c> script.</summary>
    public bool IsLib => Info.IsLib;

    /// <summary>Whether the output is a Manialink XML file; ILib fields are inlined rather than #Include'd.</summary>
    public bool IsManialink => Info.IsManialink;
    public IndentedWriter W { get; }

    /// <summary>Methods recognised as labels (virtual / override) — calls become <c>+++Name+++</c>.</summary>
    public HashSet<string> LabelMethods { get; } = [];

    /// <summary>Tracks <c>#Include</c> paths already emitted — shared across the consuming class and all inlined libs to prevent duplicates.</summary>
    public HashSet<string> EmittedIncludes { get; } = [];

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
    /// Set while emitting the body of <c>Loop()</c> so that early-exit skips the rest of the iteration.
    /// </summary>
    public bool ReturnIsContinue { get; set; }

    /// <summary>
    /// Maps C# out-var local names (as declared in <c>Persistent/Local/Metadata/Netwrite/Netread&lt;T&gt;.For()</c>)
    /// to their ManiaScript variable name (including prefix such as <c>Persistent_</c>, <c>Net_</c>).
    /// </summary>
    public Dictionary<string, string> DeclareForLocals { get; } = [];

    /// <summary>
    /// C# local variables whose initializers are lazy LINQ chains with no materialising terminal.
    /// The stored expression is the raw C# LINQ invocation chain (e.g. <c>source.Where(pred)</c>).
    /// When such a variable is later used as the source of a terminating LINQ chain, its stages are
    /// inlined at that point by <see cref="LinqChainEmitter"/> so a single foreach loop is emitted.
    /// Using a pending variable in any other context emits <see cref="Diagnostics.UnsupportedLinq"/>.
    /// </summary>
    public Dictionary<string, ExpressionSyntax> PendingLinqChains { get; } = [];

    private readonly bool _hasSpc;

    public EmitContext(ContextClassInfo info, SourceProductionContext spc, BuildSettings settings)
    {
        Info = info;
        Spc = spc;
        Settings = settings;
        W = new IndentedWriter(settings.UseSpaces, settings.IndentSize);
        _hasSpc = true;
    }

    /// <summary>Constructor for use in tests where no <see cref="SourceProductionContext"/> is available.</summary>
    internal EmitContext(ContextClassInfo info, BuildSettings settings)
    {
        Info = info;
        Settings = settings;
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
