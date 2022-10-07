using System.Collections.Immutable;
using System.Xml.Schema;
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
        Structs = BuildStructs(),
        Includes = BuildIncludes(),
        Consts = BuildConsts(),
        Settings = BuildSettings(),
        Bindings = BuildBindings(),
        Globals = BuildGlobals()
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
        var structSymbols = ScriptSymbol.GetTypeMembers()
            .Where(x => x.IsValueType)
            .ToImmutableArray();

        foreach (var structSymbol in structSymbols)
        {
            Writer.Write("#Struct ");
            Writer.Write(Standardizer.StandardizeStructName(structSymbol.Name));
            Writer.WriteLine(" {");

            foreach (var memberSymbol in structSymbol.GetMembers().Where(x => x.DeclaredAccessibility == Accessibility.Public))
            {
                string type;
                string name;

                switch (memberSymbol)
                {
                    case IFieldSymbol fieldSymbol:
                        type = Standardizer.CSharpTypeToManiaScriptType(fieldSymbol.Type.Name);
                        name = fieldSymbol.Name;
                        break;
                    case IPropertySymbol propertySymbol:
                        
                        // TODO: be more flexible about getters and setters when they are not auto properties

                        type = Standardizer.CSharpTypeToManiaScriptType(propertySymbol.Type.Name);
                        name = propertySymbol.Name;
                        break;
                    default:
                        continue;
                }
                
                Writer.Write('\t');
                Writer.Write(type);
                Writer.Write(' ');
                Writer.Write(name);
                Writer.WriteLine(";");
            }

            Writer.WriteLine("}");
            Writer.WriteLine();
        }

        return structSymbols;
    }

    private ImmutableArray<INamedTypeSymbol> BuildIncludes()
    {
        // TODO: scan through all the syntax trees and find referenced types that are part of the same namespace
        return ImmutableArray<INamedTypeSymbol>.Empty;
    }

    private ImmutableArray<IFieldSymbol> BuildConsts()
    {
        var consts = ScriptSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(x => x.IsConst)
            .ToImmutableArray();
        
        foreach (var constSymbol in consts.Where(x => !x.GetAttributes().Any(y => y.AttributeClass?.Name == "SettingAttribute")))
        {
            Writer.Write("#Const ");
            Writer.Write(Standardizer.StandardizeConstName(constSymbol.Name));
            Writer.Write(' ');
            
            var isStr = constSymbol.ConstantValue is string;

            if (isStr)
            {
                Writer.Write('"');
            }

            Writer.Write(constSymbol.ConstantValue);

            if (isStr)
            {
                Writer.Write('"');
            }
            
            Writer.WriteLine();
        }

        if (consts.Length > 0)
        {
            Writer.WriteLine();
        }

        return consts;
    }

    private ImmutableArray<IFieldSymbol> BuildSettings()
    {
        var consts = ScriptSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(x => x.IsConst)
            .ToImmutableArray();
        
        foreach (var constSymbol in consts)
        {
            var settingAttribute = constSymbol.GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == "SettingAttribute");
            
            if (settingAttribute is null)
            {
                continue;
            }
            
            Writer.Write("#Setting ");
            Writer.Write(Standardizer.StandardizeSettingName(constSymbol.Name));
            Writer.Write(' ');
            
            var isStr = constSymbol.ConstantValue is string;

            if (isStr)
            {
                Writer.Write('"');
            }

            Writer.Write(constSymbol.ConstantValue);

            if (isStr)
            {
                Writer.Write('"');
            }

            var asValue = default(string);
            var translated = true;

            foreach (var namedArg in settingAttribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "As":
                        asValue = namedArg.Value.Value?.ToString();
                        break;
                    case "Translated":
                        translated = (bool)namedArg.Value.Value!;
                        break;
                }
            }

            if (asValue is not null)
            {
                Writer.Write(" as ");

                if (translated)
                {
                    Writer.Write("_(");
                }

                Writer.Write('"');
                Writer.Write(asValue);
                Writer.Write('"');

                if (translated)
                {
                    Writer.Write(")");
                }
            }

            Writer.WriteLine();
        }

        if (consts.Length > 0)
        {
            Writer.WriteLine();
        }

        return consts;
    }

    private ImmutableArray<IPropertySymbol> BuildBindings()
    {
        var bindings = ScriptSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(x => x.Type.IsSubclassOf(y => y.Name == "CMlControl") && x.GetAttributes()
                .Any(y => y.AttributeClass?.Name == "ManialinkControlAttribute")
            ).ToImmutableArray();

        foreach (var binding in bindings)
        {
            Writer.Write("declare ");
            Writer.Write(binding.Type.Name);
            Writer.Write(' ');
            Writer.Write(binding.Name);
            Writer.Write("; // Bound to \"");
            Writer.Write(binding.GetAttributes()
                .First(x => x.AttributeClass?.Name == "ManialinkControlAttribute")
                .ConstructorArguments[0].Value);
            Writer.WriteLine('"');
        }
        
        if (bindings.Length > 0)
        {
            Writer.WriteLine();
        }
        
        return bindings;
    }

    private ImmutableArray<ISymbol> BuildGlobals()
    {
        var globals = WriteGlobals().ToImmutableArray();
        
        IEnumerable<ISymbol> WriteGlobals()
        {
            foreach (var memberSymbol in ScriptSymbol.GetMembers().Where(x => x.DeclaredAccessibility == Accessibility.Public))
            {
                string type;
                string name;

                switch (memberSymbol)
                {
                    case IFieldSymbol fieldSymbol:
                        type = Standardizer.CSharpTypeToManiaScriptType(fieldSymbol.Type.Name);
                        name = fieldSymbol.Name;
                        break;
                    case IPropertySymbol propertySymbol:

                        // TODO: be more flexible about getters and setters when they are not auto properties

                        type = Standardizer.CSharpTypeToManiaScriptType(propertySymbol.Type.Name);
                        name = propertySymbol.Name;
                        break;
                    default:
                        continue;
                }

                if (memberSymbol.GetAttributes().Any(x => x.AttributeClass?.Name is "ManialinkControlAttribute"))
                {
                    continue;
                }

                Writer.Write("declare ");
                Writer.Write(type);
                Writer.Write(' ');
                Writer.Write(name);
                Writer.WriteLine(";");

                yield return memberSymbol;
            }
        }
        
        if (globals.Length > 0)
        {
            Writer.WriteLine();
        }
        
        return globals;
    }
}