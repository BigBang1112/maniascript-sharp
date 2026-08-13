using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>Emits the synthetic <c>main()</c> function combining Main/Loop, manialink wiring and the event loop.</summary>
internal sealed class MainEmitter
{
    private readonly EmitContext _ctx;
    private readonly StatementEmitter _stmt;
    private readonly ExpressionEmitter _expr;
    private readonly EventCollector _events;

    public MainEmitter(EmitContext ctx, StatementEmitter stmt, ExpressionEmitter expr, EventCollector events)
    { _ctx = ctx; _stmt = stmt; _expr = expr; _events = events; }

    public void Emit()
    {
        var main = _ctx.Info.Symbol.GetMembers("Main").OfType<IMethodSymbol>().FirstOrDefault();
        var loopRaw = _ctx.Info.Symbol.GetMembers("Loop").OfType<IMethodSymbol>().FirstOrDefault();
        // [NoLoop] fully suppresses the while(True) wrapper for one-off scripts — Loop() is
        // never emitted, even if it has a body.
        var noLoop = _ctx.Info.Symbol.HasAttr("NoLoopAttribute");
        // A Loop() whose body is only `throw new NotImplementedException(...)` is treated as absent —
        // it is a placeholder stub and should not generate a while(True) loop.
        var loop = (noLoop || (loopRaw is not null && IsNotImplementedStub(loopRaw))) ? null : loopRaw;
        var bindings = _ctx.ManialinkBindings;
        var deferred = _ctx.DeferredInits;
        var eventRegs = _events.Collect();
        var anyLoopBody = !noLoop && (loop is not null || eventRegs.Count > 0);

        if (noLoop && eventRegs.Count > 0)
            _ctx.Report(Diagnostics.NoLoopIgnoresEvents, _ctx.Info.Declaration.Identifier.GetLocation(), _ctx.Info.Symbol.Name);

        if (main is null && !anyLoopBody && bindings.Count == 0 && deferred.Count == 0)
            return;

        // ManiaScript requires a main() entry point whenever functions are defined in the
        // script, because bare top-level statements are not allowed alongside function
        // definitions.  When there are no user-defined functions/labels the code can be
        // emitted directly at the top level.
        var hasFunctions = _ctx.Info.Symbol.GetMembers().OfType<IMethodSymbol>()
            .Any(m => m.MethodKind == MethodKind.Ordinary
                   && m.Name is not ("Main" or "Loop"));
        var needsMainWrapper = hasFunctions;

        if (needsMainWrapper)
        {
            _ctx.W.Line("main() {");
            _ctx.W.Push();
        }

        // 1. Deferred public-field initialisers.
        foreach (var d in deferred)
            _ctx.W.Line($"{d.Name} = {_expr.Translate(d.Value)};");
        if (deferred.Count > 0 && (bindings.Count > 0 || main is not null || anyLoopBody))
            _ctx.W.Line();

        // 2. Manialink bindings.
        foreach (var b in bindings)
            _ctx.W.Line($"{b.FieldName} = (Page.GetFirstChild(\"{b.XmlId}\") as {b.TypeName});");
        if (bindings.Count > 0 && (main is not null || anyLoopBody))
            _ctx.W.Line();

        // 3. Main() body — event subscription statements (+=) are silently skipped by StatementEmitter;
        //    they were already consumed by EventCollector to build the event loop.
        if (main is not null) EmitBody(main);

        // 4. while-true loop with yield + Loop() body + event handling.
        if (anyLoopBody)
        {
            _ctx.W.Line("while (True) {");
            _ctx.W.Push();
            _ctx.W.Line("yield;");

            // Prepare the event-loop injector so StatementEmitter can inject it into a
            // manually-written foreach if the user placed one in Loop().
            if (eventRegs.Count > 0)
            {
                _ctx.EventLoopInjector = () => EmitEventLoopBody(eventRegs);
                _ctx.EventLoopWasInjected = false;
            }

            if (loop is not null)
            {
                _ctx.ReturnIsContinue = true;
                EmitBody(loop);
                _ctx.ReturnIsContinue = false;
            }

            // Auto-generate the event foreach only if Loop() did not contain a manual one.
            if (eventRegs.Count > 0 && !_ctx.EventLoopWasInjected)
                EmitEventLoop(eventRegs);

            _ctx.EventLoopInjector = null;
            _ctx.EventLoopWasInjected = false;

            _ctx.W.Pop();
            _ctx.W.Line("}");
        }

        if (needsMainWrapper)
        {
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
    }

    private void EmitBody(IMethodSymbol m)
    {
        if (m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax decl) return;
        if (decl.Body is null) return;
        foreach (var s in decl.Body.Statements) _stmt.Emit(s);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="m"/> has a body consisting of a single
    /// <c>throw new NotImplementedException(…)</c> statement — indicating a placeholder stub.
    /// </summary>
    private static bool IsNotImplementedStub(IMethodSymbol m)
    {
        if (m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax decl)
            return false;
        var stmts = decl.Body?.Statements;
        if (stmts is not { Count: 1 }) return false;
        if (stmts.Value[0] is not ThrowStatementSyntax ts) return false;
        if (ts.Expression is not ObjectCreationExpressionSyntax oc) return false;
        var typeName = oc.Type.ToString();
        return typeName is "NotImplementedException"
                        or "System.NotImplementedException";
    }

    private void EmitEventLoop(IReadOnlyList<EventRegistration> regs)
    {
        // Group by event list in case multiple event sources exist.
        foreach (var listGroup in regs.GroupBy(r => r.EventListName))
        {
            _ctx.W.Line($"foreach (Event in {listGroup.Key}) {{");
            _ctx.W.Push();
            EmitEventLoopBody(listGroup.ToList());
            _ctx.W.Pop();
            _ctx.W.Line("}");
        }
    }

    /// <summary>
    /// Emits the event-dispatching body that goes inside a <c>foreach (Event in …)</c> block.
    /// Called either from <see cref="EmitEventLoop"/> or injected by <see cref="StatementEmitter"/>
    /// into a manually-written foreach in Loop().
    /// </summary>
    internal void EmitEventLoopBody(IEnumerable<EventRegistration> regs)
    {
        var regList = regs as IReadOnlyList<EventRegistration> ?? regs.ToList();

        // 1. General handlers — receive the whole Event object, no type switch.
        foreach (var r in regList.Where(r => r.IsGeneral))
            EmitHandlerCall(r, passWholeEvent: true);

        // 2. Typed handlers grouped by ManiaScript event type.
        var typed = regList.Where(r => !r.IsGeneral)
                           .GroupBy(r => r.EventTypeName)
                           .ToList();
        if (typed.Count == 0) return;

        _ctx.W.Line("switch (Event.Type) {");
        _ctx.W.Push();

        foreach (var typeGroup in typed)
        {
            _ctx.W.Line($"case CMlScriptEvent::Type::{typeGroup.Key}: {{");
            _ctx.W.Push();

            // a. Class-level typed subscriptions (no control switch).
            foreach (var r in typeGroup.Where(r => r.Control == null))
                EmitTypedCall(r);

            // b. Control-specific subscriptions grouped by control expression.
            var byControl = typeGroup.Where(r => r.Control != null)
                                     .GroupBy(r => _expr.Translate(r.Control!))
                                     .ToList();
            if (byControl.Count > 0)
            {
                _ctx.W.Line("switch (Event.Control) {");
                _ctx.W.Push();
                foreach (var ctrlGroup in byControl)
                {
                    _ctx.W.Line($"case {ctrlGroup.Key}: {{");
                    _ctx.W.Push();
                    foreach (var r in ctrlGroup)
                        EmitHandlerCall(r, passWholeEvent: false);
                    _ctx.W.Pop();
                    _ctx.W.Line("}");
                }
                _ctx.W.Pop();
                _ctx.W.Line("}");
            }

            _ctx.W.Pop();
            _ctx.W.Line("}");
        }

        _ctx.W.Pop();
        _ctx.W.Line("}");
    }

    /// <summary>Emits the call for a typed class-level event (no control switch).</summary>
    private void EmitTypedCall(EventRegistration r)
    {
        if (r.ParamMemberNames.Count == 0)
        {
            // No param mapping — fall back to plain call (e.g. control-specific re-routed).
            EmitHandlerCall(r, passWholeEvent: false);
            return;
        }

        switch (r.Handler)
        {
            case ParenthesizedLambdaExpressionSyntax pl:
                // Bind lambda params to Event.* fields, then emit the lambda body.
                BindLambdaParams(pl.ParameterList.Parameters, r.ParamMemberNames);
                if (pl.Block is { } blk) { foreach (var s in blk.Statements) _stmt.Emit(s); }
                else if (pl.ExpressionBody is { } eb) _ctx.W.Line(_expr.Translate(eb) + ";");
                break;

            default:
                // Method group or identifier: call with Event.* arguments.
                var args = string.Join(", ", r.ParamMemberNames.Select(m => $"Event.{m}"));
                _ctx.W.Line($"{_expr.Translate(r.Handler)}({args});");
                break;
        }
    }

    /// <summary>Binds lambda parameters to Event field values via local declare statements.</summary>
    private void BindLambdaParams(
        SeparatedSyntaxList<ParameterSyntax> parms,
        IReadOnlyList<string> memberNames)
    {
        for (int i = 0; i < parms.Count && i < memberNames.Count; i++)
        {
            var csName = parms[i].Identifier.Text;
            var msName = NameMangler.Local(csName);
            _ctx.W.Line($"declare {msName} = Event.{memberNames[i]};");
            // Register the local so ExpressionEmitter resolves references to it.
            _ctx.DeclareForLocals[csName] = msName;
        }
    }

    /// <summary>
    /// Emits the handler call for control-specific or general events (no param expansion).
    /// Lambdas are inlined; method groups are called with <c>()</c> or <c>(Event)</c>.
    /// </summary>
    private void EmitHandlerCall(EventRegistration r, bool passWholeEvent)
    {
        switch (r.Handler)
        {
            case ParenthesizedLambdaExpressionSyntax pl when pl.Block is { } body:
                if (r.ParamMemberNames.Count > 0)
                    BindLambdaParams(pl.ParameterList.Parameters, r.ParamMemberNames);
                foreach (var s in body.Statements) _stmt.Emit(s);
                break;
            case ParenthesizedLambdaExpressionSyntax pl when pl.ExpressionBody is { } eb:
                if (r.ParamMemberNames.Count > 0)
                    BindLambdaParams(pl.ParameterList.Parameters, r.ParamMemberNames);
                _ctx.W.Line(_expr.Translate(eb) + ";");
                break;
            case SimpleLambdaExpressionSyntax sl when sl.Block is { } body:
                if (r.ParamMemberNames.Count > 0)
                    BindLambdaParams([sl.Parameter], r.ParamMemberNames);
                foreach (var s in body.Statements) _stmt.Emit(s);
                break;
            case SimpleLambdaExpressionSyntax sl when sl.ExpressionBody is { } eb:
                if (r.ParamMemberNames.Count > 0)
                    BindLambdaParams([sl.Parameter], r.ParamMemberNames);
                _ctx.W.Line(_expr.Translate(eb) + ";");
                break;
            default:
                _ctx.W.Line(passWholeEvent
                    ? $"{_expr.Translate(r.Handler)}(Event);"
                    : $"{_expr.Translate(r.Handler)}();");
                break;
        }
    }
}

