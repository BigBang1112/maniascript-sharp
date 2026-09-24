using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>Emits <c>#RequireContext</c> / <c>#Extends</c> and <c>#Include</c> directives.</summary>
internal sealed class DirectivesEmitter
{
    private readonly EmitContext _ctx;
    public DirectivesEmitter(EmitContext ctx) { _ctx = ctx; }

    public void Emit()
    {
        EmitBase();
        EmitIncludes(_ctx.EmittedIncludes);
        EmitStructImports();
    }

    /// <summary>Emits only the <c>#Include</c> lines — used when inlining a lib into a manialink script.</summary>
    internal void EmitIncludesOnly(HashSet<string> seenPaths) => EmitIncludes(seenPaths);

    private void EmitBase()
    {
        if (_ctx.IsLib)
        {
            // A library runs in the context of its consumer. ILib<T> is C# typing only;
            // an ILib that inherits its context type needs no ManiaScript directive either.
            return;
        }

        // #RequireContext belongs only to executable IContext scripts. Ordinary helper
        // classes and libraries must not turn their C# base type into a script directive.
        if (!_ctx.Info.IsContext) return;

        var bt = _ctx.Info.Symbol.BaseType;
        if (bt is null || bt.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Object) return;

        var ns = bt.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns == "ManiaScriptSharp" || ns == "ManiaScriptSharp.Scripts")
            _ctx.W.Line($"#RequireContext {bt.Name}");
        else
        {
            var scriptPath = ManiaScriptGenerator.GetNamespacePath(ns, _ctx.RootNamespace)
                .Replace(System.IO.Path.DirectorySeparatorChar, '/');
            var filePath = scriptPath.Length > 0
                ? scriptPath + "/" + bt.Name + ".Script.txt"
                : bt.Name + ".Script.txt";
            _ctx.W.Line($"#Extends \"{filePath}\"");
        }
        _ctx.W.Line();
    }

    private void EmitIncludes(HashSet<string>? seenPaths = null)
    {
        var any = false;
        var emittedPaths = seenPaths ?? new HashSet<string>();

        // Auto-include: any instance field whose type implements ILib.
        foreach (var f in _ctx.Info.Symbol.GetMembers().OfType<Microsoft.CodeAnalysis.IFieldSymbol>())
        {
            if (f.IsStatic || f.IsConst) continue;

            if (f.Type is not Microsoft.CodeAnalysis.INamedTypeSymbol fieldType) continue;

            var isLib = fieldType.AllInterfaces.Any(static i =>
                i.Name == "ILib"
                && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp");
            if (!isLib) continue;

            var typeName = fieldType.Name;
            var typeNs = fieldType.ContainingNamespace?.ToDisplayString() ?? "";
            var isOfficialLib = typeNs == "ManiaScriptSharp";

            // In manialink scripts #Include is not available; user libs will be inlined by ScriptEmitter.
            if (!isOfficialLib && _ctx.IsManialink) continue;

            string includePath;
            if (isOfficialLib)
            {
                // Built-in libs like TextLib, MathLib → include by name only.
                includePath = typeName;
            }
            else
            {
                // Use the same namespace path as the generated library file.
                // E.g. ManiaScriptSharp.Scripts.Libs.Nadeo → Libs/Nadeo/Layers2.Script.txt
                var nsPath = ManiaScriptGenerator.GetNamespacePath(typeNs, _ctx.RootNamespace)
                    .Replace(System.IO.Path.DirectorySeparatorChar, '/');
                includePath = nsPath.Length > 0
                    ? nsPath + "/" + typeName + ".Script.txt"
                    : typeName + ".Script.txt";
            }
            if (!emittedPaths.Add(typeName)) continue; // deduplicate by type (a lib can only be included once)

            _ctx.TryGetLibraryAlias(fieldType, out var alias);
            _ctx.W.Line($"#Include \"{includePath}\" as {alias}");
            any = true;
        }

        if (any) _ctx.W.Line();
    }

    private void EmitStructImports()
    {
        var aliases = new Dictionary<Microsoft.CodeAnalysis.INamedTypeSymbol, string>(
            Microsoft.CodeAnalysis.SymbolEqualityComparer.Default);
        foreach (var field in _ctx.Info.Symbol.GetMembers().OfType<Microsoft.CodeAnalysis.IFieldSymbol>())
        {
            if (field.Type is not Microsoft.CodeAnalysis.INamedTypeSymbol type || !IsLib(type)) continue;
            if (_ctx.IsManialink && IsUserDefined(type)) continue;
            if (!aliases.ContainsKey(type) && _ctx.TryGetLibraryAlias(type, out var alias))
                aliases.Add(type, alias);
        }

        var imported = new HashSet<Microsoft.CodeAnalysis.INamedTypeSymbol>(
            Microsoft.CodeAnalysis.SymbolEqualityComparer.Default);
        foreach (var typeSyntax in _ctx.Info.Declaration.DescendantNodes()
                     .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeSyntax>())
        {
            if (_ctx.Model.GetTypeInfo(typeSyntax).Type is not Microsoft.CodeAnalysis.INamedTypeSymbol
                { TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct, ContainingType: { } owner } type) continue;
            if (!aliases.TryGetValue(owner, out var alias) || !imported.Add(type)) continue;
            _ctx.W.Line($"#Struct {alias}::{type.Name} as {type.Name}");
        }

        if (imported.Count > 0) _ctx.W.Line();
    }

    private static bool IsLib(Microsoft.CodeAnalysis.INamedTypeSymbol type)
        => type.AllInterfaces.Any(static i =>
            i.Name == "ILib" && i.ContainingNamespace?.ToDisplayString() == "ManiaScriptSharp");

    private static bool IsUserDefined(Microsoft.CodeAnalysis.INamedTypeSymbol type)
        => type.DeclaringSyntaxReferences.Any(r =>
            !r.SyntaxTree.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase));
}
