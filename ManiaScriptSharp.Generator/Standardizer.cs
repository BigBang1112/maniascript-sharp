namespace ManiaScriptSharp.Generator;

public static class Standardizer
{
    public static string StandardizeStructName(string name)
    {
        if (name.Length == 0)
        {
            throw new Exception("Name is empty.");
        }
        
        if (name[0] != 'S' || name.Length < 2 || char.IsLower(name[1]))
        {
            return "S" + name;
        }

        return name;
    }
    
    public static string StandardizeUnderscorePrefixName(string name, char prefix)
    {
        if (name.Length == 0)
        {
            throw new Exception("Name is empty.");
        }
        
        if (name.Length >= 2 && name[0] == prefix && name[1] == '_')
        {
            return name;
        }
        
        return prefix + "_" + name;
    }
    
    public static string StandardizeConstName(string name)
    {
        return StandardizeUnderscorePrefixName(name, 'C');
    }
    
    public static string StandardizeSettingName(string name)
    {
        return StandardizeUnderscorePrefixName(name, 'S');
    }

    public static string CSharpTypeToManiaScriptType(string csharpType) => csharpType switch
    {
        "void" => "Void",
        "int" => "Integer",
        "float" => "Real",
        "bool" => "Boolean",
        "string" => "Text",
        _ => "X"
    };
}