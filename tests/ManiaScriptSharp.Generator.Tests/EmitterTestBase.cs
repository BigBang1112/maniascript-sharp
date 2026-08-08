using System;
using System.Collections.Generic;
using System.Linq;
using ManiaScriptSharp.Generator.Emission;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Tests;

/// <summary>
/// Provides helpers that compile C# snippets with a real Roslyn semantic model
/// and run expression/statement/pattern translation through the actual emitters.
/// </summary>
public class EmitterTestBase
{
    // Lazily built reference list from assemblies already loaded by the test runner.
    private static readonly Lazy<IReadOnlyList<MetadataReference>> SharedRefs = new(BuildRefs);

    private static IReadOnlyList<MetadataReference> BuildRefs()
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .GroupBy(a => a.GetName().Name)
            .Select(g => g.First())
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList<MetadataReference>();

        // Explicitly add ManiaScriptSharp.dll — it may not be loaded in the AppDomain
        // if the runtime doesn't eagerly load project-referenced assemblies.
        var msLocation = typeof(ManiaScriptSharp.ManiaScriptEventAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(msLocation) && refs.All(r => r.Display != msLocation))
            refs.Add(MetadataReference.CreateFromFile(msLocation));

        return refs;
    }

    private static CSharpCompilation Compile(string code, string path = "") =>
        CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(code, path: path)],
            SharedRefs.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static (EmitContext ctx, ExpressionEmitter expr, StatementEmitter stmt, PatternEmitter pattern)
        CreateEmitters(string classCode, BuildSettings? settings = null, string path = "")
    {
        var compilation = Compile(classCode, path);
        var tree = compilation.SyntaxTrees[0];
        var model = compilation.GetSemanticModel(tree);
        var classDecl = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var symbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classDecl)!;
        var info = new ContextClassInfo(classDecl, symbol, model);
        var ctx = new EmitContext(info, settings ?? BuildSettings.Default);
        var expr = new ExpressionEmitter(ctx);
        var stmt = new StatementEmitter(ctx, expr);
        var pat = new PatternEmitter(ctx, expr, stmt);
        expr.Bind(pat);
        stmt.Bind(pat);
        return (ctx, expr, stmt, pat);
    }

    /// <summary>
    /// Like <see cref="TranslateExpr"/>, but allows a custom class header (e.g. <c>: IContext</c>)
    /// and returns the diagnostics reported by <see cref="EmitContext.Report"/> alongside the output.
    /// </summary>
    protected static (string Output, IReadOnlyList<Diagnostic> Diagnostics) TranslateExprWithDiagnostics(
        string csharpExpr, string extraClassMembers = "", string classHeader = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
using ManiaScriptSharp;
class Test {classHeader} {{
    {extraClassMembers}
    void M() {{
        var __result__ = ({csharpExpr});
    }}
}}";
        var (ctx, expr, _, _) = CreateEmitters(code);
        var initExpr = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .First(v => v.Identifier.Text == "__result__")
            .Initializer!.Value;
        if (initExpr is ParenthesizedExpressionSyntax pe)
            initExpr = pe.Expression;
        var output = expr.Translate(initExpr);
        return (output, ctx.ReportedDiagnostics);
    }

    /// <summary>
    /// Like <see cref="TranslateExpr"/>, but parses the snippet as if it lived in a generator-produced
    /// <c>.g.cs</c> file — used to verify that plain auto-generated API properties (no user-written
    /// body) are treated as ManiaScript fields rather than routed through a Get/Set method call.
    /// </summary>
    protected static string TranslateExprFromGeneratedSource(string csharpExpr, string extraClassMembers = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
class Test {{
    {extraClassMembers}
    void M() {{
        {csharpExpr};
    }}
}}";
        var (ctx, expr, _, _) = CreateEmitters(code, path: "Test.g.cs");
        var stmt = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<ExpressionStatementSyntax>().First();
        return expr.Translate(stmt.Expression);
    }

    /// <summary>
    /// Compiles <paramref name="csharpExpr"/> as the RHS of a local variable initialiser
    /// and returns the ManiaScript translation produced by <see cref="ExpressionEmitter"/>.
    /// <paramref name="extraClassMembers"/> can supply fields/methods that the expression
    /// needs to be resolved correctly (class-level declarations).
    /// </summary>
    protected static string TranslateExpr(string csharpExpr, string extraClassMembers = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
class Test {{
    {extraClassMembers}
    void M() {{
        var __result__ = ({csharpExpr});
    }}
}}";
        var (ctx, expr, _, _) = CreateEmitters(code);
        var initExpr = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .First(v => v.Identifier.Text == "__result__")
            .Initializer!.Value;
        if (initExpr is ParenthesizedExpressionSyntax pe)
            initExpr = pe.Expression;
        return expr.Translate(initExpr);
    }

    /// <summary>
    /// Emits the first statement inside a test method and returns the
    /// normalised (LF, trimmed) ManiaScript output.
    /// </summary>
    protected static string TranslateStmt(string csharpStmt, string extraClassMembers = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
class Test {{
    {extraClassMembers}
    void M() {{
        {csharpStmt}
    }}
}}";
        var (ctx, _, stmt, _) = CreateEmitters(code);
        var firstStmt = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "M").Body!.Statements.First();
        stmt.Emit(firstStmt);
        return ctx.W.ToString().ReplaceLineEndings("\n").Trim();
    }

    /// <summary>
    /// Like <see cref="TranslateStmt"/>, but also returns the diagnostics reported by
    /// <see cref="EmitContext.Report"/> while emitting the statement.
    /// </summary>
    protected static (string Output, IReadOnlyList<Diagnostic> Diagnostics) TranslateStmtWithDiagnostics(
        string csharpStmt, string extraClassMembers = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
class Test {{
    {extraClassMembers}
    void M() {{
        {csharpStmt}
    }}
}}";
        var (ctx, _, stmt, _) = CreateEmitters(code);
        var firstStmt = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "M").Body!.Statements.First();
        stmt.Emit(firstStmt);
        return (ctx.W.ToString().ReplaceLineEndings("\n").Trim(), ctx.ReportedDiagnostics);
    }

    /// <summary>
    /// Emits statements with an extra label method registered so label-call
    /// rewriting can be tested.  The labeled method is declared in the class so
    /// Roslyn can resolve its symbol and the +++Name+++ rewriting fires.
    /// </summary>
    protected static string TranslateStmtWithLabel(string labelName, string csharpStmt)
    {
        // Declare the label method so the semantic model resolves its symbol.
        var code = $@"
class Test {{
    void {labelName}() {{ }}
    void M() {{ {csharpStmt} }}
}}";
        var (ctx, _, stmt, _) = CreateEmitters(code);
        ctx.LabelMethods.Add(labelName);
        var firstStmt = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "M").Body!.Statements.First();
        stmt.Emit(firstStmt);
        return ctx.W.ToString().ReplaceLineEndings("\n").Trim();
    }

    /// <summary>
    /// Compiles <paramref name="classBody"/> as the body of a top-level class and runs
    /// <see cref="FunctionEmitter.Emit"/> on it, returning the normalised output.
    /// </summary>
    protected static string EmitFunctions(string classBody)
    {
        var code = $@"
using System;
using System.Collections.Generic;
using ManiaScriptSharp;
class Test {{
    {classBody}
}}";
        var (ctx, expr, stmt, _) = CreateEmitters(code);
        var functions = new FunctionEmitter(ctx, stmt, expr);
        functions.CollectLabels();
        functions.Emit();
        return ctx.W.ToString().ReplaceLineEndings("\n").Trim();
    }

    /// <summary>
    /// Runs <see cref="OnChangeCollector"/> then <see cref="GlobalEmitter"/> on the given class
    /// body, returning the normalised declare-globals output (including any OnChange backing globals).
    /// </summary>
    protected static string EmitGlobalsWithOnChange(string classBody)
    {
        var code = $@"
using System;
using System.Collections.Generic;
using ManiaScriptSharp;
class Test {{
    {classBody}
}}";
        var (ctx, expr, _, _) = CreateEmitters(code);
        new OnChangeCollector(ctx).Collect();
        new GlobalEmitter(ctx, expr).Emit();
        return ctx.W.ToString().ReplaceLineEndings("\n").Trim();
    }

    /// <summary>
    /// Like <see cref="TranslateStmt"/> but also imports <c>ManiaScriptSharp</c> types so
    /// statements using <c>Persistent&lt;T&gt;</c>, <c>Local&lt;T&gt;</c>, etc. compile.
    /// </summary>
    protected static string TranslateStmtMs(string csharpStmt, string extraClassMembers = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
using ManiaScriptSharp;
class Test {{
    {extraClassMembers}
    void M() {{
        {csharpStmt}
    }}
}}";
        var (ctx, _, stmt, _) = CreateEmitters(code);
        var firstStmt = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "M").Body!.Statements.First();
        stmt.Emit(firstStmt);
        return ctx.W.ToString().ReplaceLineEndings("\n").Trim();
    }

    /// <summary>
    /// Like <see cref="TranslateStmtMs"/>, but also returns the diagnostics reported by
    /// <see cref="EmitContext.Report"/> while emitting the statement.
    /// </summary>
    protected static (string Output, IReadOnlyList<Diagnostic> Diagnostics) TranslateStmtWithDiagnosticsMs(
        string csharpStmt, string extraClassMembers = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
using ManiaScriptSharp;
class Test {{
    {extraClassMembers}
    void M() {{
        {csharpStmt}
    }}
}}";
        var (ctx, _, stmt, _) = CreateEmitters(code);
        var firstStmt = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "M").Body!.Statements.First();
        stmt.Emit(firstStmt);
        return (ctx.W.ToString().ReplaceLineEndings("\n").Trim(), ctx.ReportedDiagnostics);
    }

    /// <summary>
    /// Like <see cref="TranslateStmtMs"/> but emits ALL statements of the method body.
    /// Useful for testing multi-statement sequences (e.g. declare-for then use).
    /// </summary>
    protected static string TranslateBodyMs(string csharpBody, string extraClassMembers = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
using System.Linq;
using ManiaScriptSharp;
class Test {{
    {extraClassMembers}
    void M() {{
        {csharpBody}
    }}
}}";
        var (ctx, _, stmt, _) = CreateEmitters(code);
        var stmts = ctx.Info.Model.SyntaxTree.GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "M").Body!.Statements;
        foreach (var s in stmts) stmt.Emit(s);
        return ctx.W.ToString().ReplaceLineEndings("\n").Trim();
    }

    /// <summary>
    /// Compiles <paramref name="classBody"/> as the body of a top-level class named <c>Test</c>
    /// (with <c>ManiaScriptSharp</c> types in scope) and runs <see cref="MainEmitter.Emit()"/>.
    /// <paramref name="extraCode"/> may supply additional top-level type declarations that are
    /// visible to <c>Test</c> (e.g. a helper control type for external-event tests); the
    /// <c>Test</c> class must appear first so <c>CreateEmitters</c> selects it correctly.
    /// <paramref name="classAttributes"/> may supply attributes (e.g. <c>[NoLoop]</c>) applied
    /// directly above the <c>Test</c> class declaration.
    /// </summary>
    protected static string EmitMain(string classBody, string extraCode = "", string classAttributes = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
using ManiaScriptSharp;
{classAttributes}
class Test {{
    {classBody}
}}
{extraCode}";
        var (ctx, expr, stmt, _) = CreateEmitters(code);
        var events = new EventCollector(ctx);
        var main = new MainEmitter(ctx, stmt, expr, events);
        main.Emit();
        return ctx.W.ToString().ReplaceLineEndings("\n").Trim();
    }

    protected static int CountEventRegistrations(string classBody, string extraCode = "")
    {
        var code = $@"
using System;
using System.Collections.Generic;
using ManiaScriptSharp;
class Test {{
    {classBody}
}}
{extraCode}";
        var (ctx, _, _, _) = CreateEmitters(code);
        return new EventCollector(ctx).Collect().Count;
    }

    /// <summary>
    /// Translates a C# pattern (everything after the subject in <c>x is …</c>)
    /// with a pre-supplied lhs string.  Uses a stub compilation; safe as long as
    /// the pattern's constant sub-expressions are literals (no symbol look-ups).
    /// </summary>
    protected static string TranslatePattern(string lhs, string csharpIsPattern)
    {
        // Build a minimal stub context just to satisfy EmitContext's constructor.
        var (ctx, expr, stmt, _) = CreateEmitters("class Stub { }");
        var pat = new PatternEmitter(ctx, expr, stmt);
        expr.Bind(pat);
        stmt.Bind(pat);

        // Parse the full `is` expression to obtain the PatternSyntax.
        var parseExpr = (IsPatternExpressionSyntax)SyntaxFactory.ParseExpression($"__lhs__ {csharpIsPattern}");
        return pat.Translate(lhs, parseExpr.Pattern);
    }
}
