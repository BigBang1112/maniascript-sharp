using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public static class Standardizer
{
    public static string StandardizeStructName(string name)
    {
        if (name.Length == 0)
        {
            return "";
        }
        
        if (name[0] != 'S' || name.Length < 2 || char.IsLower(name[1]))
        {
            return "S" + name;
        }

        return name;
    }
    
    public static string StandardizeUnderscoreName(string name)
    {
        if (name.Length == 0)
        {
            return "";
        }
        
        if (name[0] == '_' && name.Length >= 2)
        {
            return char.IsLower(name[1])
                ? "_" + char.ToUpper(name[1]) + name.Substring(2)
                : name;
        }
        
        return "_" + char.ToUpper(name[0]) + name.Substring(1);
    }
    
    public static string StandardizeUnderscorePrefixName(string name, char prefix)
    {
        if (name.Length == 0)
        {
            return "";
        }
        
        if (name.Length >= 2 && name[0] == prefix && name[1] == '_')
        {
            return name;
        }
        
        return prefix + "_" + char.ToUpper(name[0]) + name.Substring(1);
    }
    
    public static string StandardizeConstName(string name)
    {
        return StandardizeUnderscorePrefixName(name, 'C');
    }
    
    public static string StandardizeSettingName(string name)
    {
        return StandardizeUnderscorePrefixName(name, 'S');
    }
    
    public static string StandardizeGlobalName(string name)
    {
        return StandardizeUnderscorePrefixName(name, 'G');
    }

    public static string CSharpTypeToManiaScriptType(string csharpType) => csharpType switch
    {
        "void" => "Void",
        nameof(Int32) => "Integer",
        nameof(Single) => "Real",
        nameof(Boolean) => "Boolean",
        nameof(String) => "Text",
        _ => csharpType
    };
    

    public static string CSharpTypeToManiaScriptType(INamedTypeSymbol csharpType)
    {
        switch (csharpType.Name)
        {
            case "ImmutableArray":
                return csharpType.TypeArguments[0].Name switch
                {
                    "Int32" => "Integer[]",
                    "Single" => "Real[]",
                    "Boolean" => "Boolean[]",
                    "String" => "Text[]",
                    _ => csharpType.TypeArguments[0].Name + "[]"
                };
            default:
                return CSharpTypeToManiaScriptType(csharpType.Name);
        }
    }
}