using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.Generator;

public static class Standardizer
{
    [Obsolete]
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
    

    public static string CSharpTypeToManiaScriptType(ITypeSymbol csharpType, HashSet<string>? knownStructNames)
    {
        if (csharpType is not INamedTypeSymbol namedTypeSymbol)
        {
            return CSharpTypeToManiaScriptType(csharpType.Name);
        }

        if (csharpType.TypeKind is TypeKind.Enum)
        {
            var name = namedTypeSymbol.Name;
            var containType = namedTypeSymbol.ContainingType;

            while (containType is not null)
            {
                name = $"{containType.Name}::{name}";
                containType = containType.ContainingType;
            }

            return name;
        }

        if (csharpType.Name == "Dictionary")
        {
            var keySymbol = namedTypeSymbol.TypeArguments[0];
            var valSymbol = namedTypeSymbol.TypeArguments[1];
            
            var val = valSymbol is INamedTypeSymbol namedTypeSymbolVal
                ? CSharpTypeToManiaScriptType(namedTypeSymbolVal, knownStructNames)
                : CSharpTypeToManiaScriptType(valSymbol.Name);
            
            var key = keySymbol is INamedTypeSymbol namedTypeSymbolKey
                ? CSharpTypeToManiaScriptType(namedTypeSymbolKey, knownStructNames)
                : CSharpTypeToManiaScriptType(valSymbol.Name);

            return $"{val}[{key}]";
        }

        if (csharpType.Name is "ImmutableArray" or "IList")
        {
            return CSharpTypeToManiaScriptType(namedTypeSymbol.TypeArguments[0], knownStructNames) + "[]";
        }

        if (csharpType.ContainingType?.IsStatic == true && knownStructNames?.Contains(csharpType.Name) == false)
        {
            return csharpType.ContainingType.Name + "::" + CSharpTypeToManiaScriptType(csharpType.Name);
        }

        return CSharpTypeToManiaScriptType(csharpType.Name);
    }

    public static string StandardizeName(string name)
    {
        if (name.Length == 0)
        {
            return "";
        }

        if (char.IsUpper(name[0]))
        {
            return name;
        }

        var charArray = name.ToCharArray();
        charArray[0] = char.ToUpper(charArray[0]);
        return new string(charArray);
    }
}