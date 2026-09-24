using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator.Emission;

/// <summary>Emits host-exposed <c>#Command</c> directives declared with <c>[Command]</c>.</summary>
internal sealed class CommandEmitter
{
    private readonly EmitContext _ctx;

    public CommandEmitter(EmitContext ctx)
    {
        _ctx = ctx;
    }

    public void Emit()
    {
        var any = false;
        foreach (var attr in _ctx.Info.Symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "CommandAttribute") continue;

            var name = attr.Ctor<string>(0) ?? "";
            if (name.Length == 0) continue;

            var parameters = attr.ConstructorArguments.Length > 1
                ? attr.ConstructorArguments[1].Values
                    .Select(v => v.Value as ITypeSymbol)
                    .Where(static t => t is not null)
                    .Select(t => _ctx.MapType(t))
                : Enumerable.Empty<string>();
            var display = attr.Named<string>("As") ?? name;
            var translated = attr.Named<bool?>("Translated") ?? true;
            var label = translated ? "_(" + Quote(display) + ")" : Quote(display);

            _ctx.W.Line($"#Command {name} ({string.Join(", ", parameters)}) as {label}");
            any = true;
        }

        if (any) _ctx.W.Line();
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Quote(string text) => $"{(char)34}{Escape(text)}{(char)34}";
}
