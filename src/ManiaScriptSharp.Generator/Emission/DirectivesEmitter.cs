using ManiaScriptSharp.Generator.Naming;

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
    }

    /// <summary>Emits only the <c>#Include</c> lines — used when inlining a lib into a manialink script.</summary>
    internal void EmitIncludesOnly(HashSet<string> seenPaths) => EmitIncludes(seenPaths);

    private void EmitBase()
    {
        if (_ctx.IsLib)
        {
            // For a lib class, emit #RequireContext from the ILib<T> type parameter.
            var t = _ctx.Info.LibContextType;
            if (t is not null)
            {
                _ctx.W.Line($"#RequireContext {t.Name}");
                _ctx.W.Line();
            }
            return;
        }

        var bt = _ctx.Info.Symbol.BaseType;
        if (bt is null || bt.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Object) return;

        var ns = bt.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns == "ManiaScriptSharp" || ns == "ManiaScriptSharp.Scripts")
            _ctx.W.Line($"#RequireContext {bt.Name}");
        else
        {
            const string scriptsNsPrefix = "ManiaScriptSharp.Scripts.";
            const string baseNsPrefix = "ManiaScriptSharp.";
            var scriptPath = ns.StartsWith(scriptsNsPrefix, StringComparison.Ordinal)
                ? ns.Substring(scriptsNsPrefix.Length).Replace('.', '/')
                : ns.StartsWith(baseNsPrefix, StringComparison.Ordinal)
                    ? ns.Substring(baseNsPrefix.Length).Replace('.', '/')
                    : ns.Replace('.', '/');
            _ctx.W.Line($"#Extends \"{scriptPath}/{bt.Name}.Script.txt\"");
        }
        _ctx.W.Line();
    }

    private void EmitIncludes(HashSet<string>? seenPaths = null)
    {
        var any = false;
        var emittedPaths = seenPaths ?? new HashSet<string>();

        // Explicit [Include] attributes on the context class.
        foreach (var attr in _ctx.Info.Symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "IncludeAttribute") continue;
            var path = attr.Ctor<string>(0) ?? "";
            var alias = attr.Named<string>("As") ?? "";
            if (string.IsNullOrEmpty(alias))
            {
                var leaf = path.Split('/', '\\').Last();
                alias = leaf.Replace(".Script.txt", "");
            }
            _ctx.W.Line($"#Include \"{path}\" as {alias}");
            emittedPaths.Add(path);
            any = true;
        }

        // Auto-include: any public or internal field whose type implements ILib.
        foreach (var f in _ctx.Info.Symbol.GetMembers().OfType<Microsoft.CodeAnalysis.IFieldSymbol>())
        {
            if (f.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public
                && f.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Internal) continue;
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
                // Libs from script files have a namespace like ManiaScriptSharp.Scripts.Libs.Nadeo.
                // Strip the "ManiaScriptSharp.Scripts." prefix, convert dots to slashes, append filename.
                // E.g. ManiaScriptSharp.Scripts.Libs.Nadeo → Libs/Nadeo/Layers2.Script.txt
                const string scriptsPrefix = "ManiaScriptSharp.Scripts.";
                const string msPrefix = "ManiaScriptSharp.";
                var nsPath = typeNs.StartsWith(scriptsPrefix, StringComparison.Ordinal)
                    ? typeNs.Substring(scriptsPrefix.Length).Replace('.', '/')
                    : typeNs.StartsWith(msPrefix, StringComparison.Ordinal)
                        ? typeNs.Substring(msPrefix.Length).Replace('.', '/')
                        : typeNs.Replace('.', '/');
                includePath = nsPath.Length > 0
                    ? nsPath + "/" + typeName + ".Script.txt"
                    : typeName + ".Script.txt";
            }
            if (!emittedPaths.Add(typeName)) continue; // deduplicate by type (a lib can only be included once)

            var alias = NameMangler.PascalCase(f.Name);
            _ctx.W.Line($"#Include \"{includePath}\" as {alias}");
            any = true;
        }

        if (any) _ctx.W.Line();
    }
}
