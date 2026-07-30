using ManiaScriptSharp.Generator.Naming;
using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>Emits <c>#Const C_*</c> and <c>#Setting S_*</c> lines for the context class.</summary>
internal sealed class ConstSettingEmitter
{
    private readonly EmitContext _ctx;
    private readonly LiteralEmitter _lit;
    public ConstSettingEmitter(EmitContext ctx, LiteralEmitter lit) { _ctx = ctx; _lit = lit; }

    public void Emit()
    {
        var any = false;
        foreach (var f in _ctx.Info.Symbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (!f.IsConst) continue;
            if (f.HasAttr("SettingAttribute")) continue;
            _ctx.W.Line($"#Const {NameMangler.Const(f)} {_lit.OfField(f)}");
            any = true;
        }
        if (any) _ctx.W.Line();

        any = false;
        foreach (var f in _ctx.Info.Symbol.GetMembers().OfType<IFieldSymbol>())
        {
            var attr = f.GetAttr("SettingAttribute");
            if (attr is null) continue;

            var display = attr.Named<string>("As") ?? Humanize(NameMangler.PascalCase(f.Name));
            var translated = attr.Named<bool?>("Translated") ?? true;
            var labelExpr = translated ? $"_(\"{display}\")" : $"\"{display}\"";

            _ctx.W.Line($"#Setting {NameMangler.Setting(f)} {_lit.OfField(f)} as {labelExpr}");
            any = true;
        }
        if (any) _ctx.W.Line();
    }

    private static string Humanize(string s)
        => System.Text.RegularExpressions.Regex.Replace(s, "([a-z])([A-Z])", "$1 $2");
}
