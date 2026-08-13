using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>
/// Walks the context class <c>Main()</c> to find <c>X.SomeEvent += handler</c>
/// and <c>SomeEvent += handler</c> registrations and turns them into <see cref="EventRegistration"/>
/// values that drive the <c>foreach (Event in …)</c> loop.
/// </summary>
internal sealed class EventCollector
{
    private readonly EmitContext _ctx;
    public EventCollector(EmitContext ctx) { _ctx = ctx; }

    public IReadOnlyList<EventRegistration> Collect()
    {
        var result = new List<EventRegistration>();
        ScanMethod(_ctx.Info.Symbol.GetMembers("Main").OfType<IMethodSymbol>().FirstOrDefault(), result);
        return result;
    }

    private void ScanMethod(IMethodSymbol? method, List<EventRegistration> result)
    {
        var syntax = method?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        var body = syntax switch
        {
            ConstructorDeclarationSyntax c => c.Body,
            MethodDeclarationSyntax m => m.Body,
            _ => null,
        };
        if (body is null) return;

        foreach (var stmt in body.Statements)
        {
            if (stmt is not ExpressionStatementSyntax es) continue;
            if (es.Expression is not AssignmentExpressionSyntax asg) continue;
            if (!asg.OperatorToken.IsKind(SyntaxKind.PlusEqualsToken)) continue;

            if (asg.Left is MemberAccessExpressionSyntax ma)
            {
                // Control-specific: QuadMapName.MouseClick += handler
                // The event must have [ManiaScriptExternalEvent] attribute.
                if (_ctx.Model.GetSymbolInfo(ma).Symbol is not IEventSymbol extSym) continue;
                var extAttr = extSym.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "ManiaScriptExternalEventAttribute");
                if (extAttr is null) continue;

                var eventList = extAttr.ConstructorArguments.Length > 1
                    ? extAttr.ConstructorArguments[1].Value?.ToString() ?? "PendingEvents"
                    : "PendingEvents";
                var eventKind = extAttr.ConstructorArguments.Length > 2
                    ? extAttr.ConstructorArguments[2].Value?.ToString() ?? ""
                    : "";

                // Derive param→Event.* mapping from the delegate's invoke method,
                // so lambda handlers can reference params by name instead of _PascalCase.
                IReadOnlyList<string> extParamNames = [];
                if (extSym.Type is INamedTypeSymbol extDelType)
                {
                    var invoke = extDelType.DelegateInvokeMethod;
                    if (invoke is not null)
                        extParamNames = invoke.Parameters.Select(p =>
                        {
                            var memberAttr = p.GetAttributes()
                                .FirstOrDefault(a => a.AttributeClass?.Name == "MemberNameAttribute");
                            if (memberAttr?.ConstructorArguments.Length > 0)
                                return memberAttr.ConstructorArguments[0].Value?.ToString() ?? "";
                            var n = p.Name;
                            return n.Length == 0 ? "" : char.ToUpper(n[0]) + n.Substring(1);
                        }).ToList();
                }

                result.Add(new EventRegistration(
                    control: ma.Expression,
                    eventListName: eventList,
                    eventTypeName: eventKind,
                    handler: asg.Right,
                    paramMemberNames: extParamNames));
            }
            else if (asg.Left is IdentifierNameSyntax ins)
            {
                // Class-level: MouseClick += handler  (on CMlScript or subclass)
                // The event's delegate type must have [ManiaScriptEvent] attribute.
                if (_ctx.Model.GetSymbolInfo(ins).Symbol is not IEventSymbol classSym) continue;
                if (classSym.Type is not INamedTypeSymbol delegateType) continue;

                var maniaAttr = delegateType.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "ManiaScriptEventAttribute");
                if (maniaAttr is null) continue;

                var eventList = maniaAttr.ConstructorArguments.Length > 0
                    ? maniaAttr.ConstructorArguments[0].Value?.ToString() ?? "PendingEvents"
                    : "PendingEvents";

                var invoke = delegateType.DelegateInvokeMethod;
                if (invoke is null) continue;

                // General handler: single parameter named "e" — receives the whole Event object.
                bool isGeneral = invoke.Parameters.Length == 1 && invoke.Parameters[0].Name == "e";

                string eventTypeName = "";
                IReadOnlyList<string> paramMemberNames = [];

                if (!isGeneral)
                {
                    // Derive ManiaScript event type from delegate name: strip "EventHandler".
                    var dn = delegateType.Name;
                    eventTypeName = dn.EndsWith("EventHandler", StringComparison.Ordinal)
                        ? dn.Substring(0, dn.Length - "EventHandler".Length)
                        : dn;

                    // Map each delegate parameter to Event.{MemberName}.
                    paramMemberNames = invoke.Parameters.Select(p =>
                    {
                        var memberAttr = p.GetAttributes()
                            .FirstOrDefault(a => a.AttributeClass?.Name == "MemberNameAttribute");
                        if (memberAttr?.ConstructorArguments.Length > 0)
                            return memberAttr.ConstructorArguments[0].Value?.ToString() ?? "";
                        var n = p.Name;
                        return n.Length == 0 ? "" : char.ToUpper(n[0]) + n.Substring(1);
                    }).ToList();
                }

                result.Add(new EventRegistration(
                    control: null,
                    eventListName: eventList,
                    eventTypeName: eventTypeName,
                    handler: asg.Right,
                    isGeneral: isGeneral,
                    paramMemberNames: paramMemberNames));
            }
        }
    }
}

/// <summary>
/// Describes a single C# event subscription and how it maps to ManiaScript.
/// </summary>
internal readonly struct EventRegistration
{
    /// <summary>Control expression for control-specific events; <c>null</c> for class-level events.</summary>
    public ExpressionSyntax? Control { get; }

    /// <summary>Name of the ManiaScript event list (e.g. <c>"PendingEvents"</c>).</summary>
    public string EventListName { get; }

    /// <summary>ManiaScript event type name (e.g. <c>"MouseClick"</c>). Empty for general handlers.</summary>
    public string EventTypeName { get; }

    /// <summary>The C# handler: method group, lambda, or anonymous method.</summary>
    public ExpressionSyntax Handler { get; }

    /// <summary>
    /// <c>true</c> when the handler receives the whole Event object (single param named <c>e</c>).
    /// Generates <c>handler(Event);</c> outside any type switch.
    /// </summary>
    public bool IsGeneral { get; }

    /// <summary>
    /// For typed class-level handlers, the ManiaScript property name on <c>Event</c> for each
    /// delegate parameter (in order). E.g. <c>["Control", "ControlId"]</c> for MouseClick.
    /// Empty for control-specific (external) events or general handlers.
    /// </summary>
    public IReadOnlyList<string> ParamMemberNames { get; }

    public EventRegistration(
        ExpressionSyntax? control,
        string eventListName,
        string eventTypeName,
        ExpressionSyntax handler,
        bool isGeneral = false,
        IReadOnlyList<string>? paramMemberNames = null)
    {
        Control = control;
        EventListName = eventListName;
        EventTypeName = eventTypeName;
        Handler = handler;
        IsGeneral = isGeneral;
        ParamMemberNames = paramMemberNames ?? [];
    }
}

