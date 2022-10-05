using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptHeadBuilder
{
    public INamedTypeSymbol ScriptSymbol { get; }
    public TextWriter Writer { get; }
    public bool IsEmbeddedInManialink { get; }

    public ManiaScriptHeadBuilder(INamedTypeSymbol scriptSymbol, TextWriter writer, bool isEmbeddedInManialink = false)
    {
        ScriptSymbol = scriptSymbol;
        Writer = writer;
        IsEmbeddedInManialink = isEmbeddedInManialink;
    }

    public ManiaScriptHead AnalyzeAndBuild() => new()
    {
        Context = BuildContext(),
        Structs = BuildStructs()
    };

    private INamedTypeSymbol BuildContext()
    {
        if (ScriptSymbol.BaseType is null)
        {
            throw new Exception("Context script requires a specific class context.");
        }
        
        if (IsEmbeddedInManialink)
        {
            return ScriptSymbol.BaseType;
        }
        
        var isOfficialSymbol = ScriptSymbol.BaseType.ContainingNamespace.ToDisplayString() == "ManiaScriptSharp";

        if (!isOfficialSymbol)
        {
            throw new NotSupportedException();
        }
        
        Writer.Write("#RequireContext ");
        Writer.WriteLine(ScriptSymbol.BaseType.Name);
        Writer.WriteLine();
        
        return ScriptSymbol.BaseType;
    }

    private ImmutableArray<INamedTypeSymbol> BuildStructs()
    {
        var nestedTypeSymbols = ScriptSymbol.GetTypeMembers()
            .Where(x => x.IsValueType)
            .ToImmutableArray();
        
        return nestedTypeSymbols;
    }
}