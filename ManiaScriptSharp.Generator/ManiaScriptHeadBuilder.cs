using System.Collections.Immutable;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptHeadBuilder
{
    private ImmutableArray<IPropertySymbol> additionalConsts;
    
    public INamedTypeSymbol ScriptSymbol { get; }
    public SemanticModel SemanticModel { get; }
    public TextWriter Writer { get; }
    public GeneratorHelper Helper { get; }
    public XmlDocument? ManialinkXml { get; }

    public ManiaScriptHeadBuilder(INamedTypeSymbol scriptSymbol, SemanticModel semanticModel, TextWriter writer,
        GeneratorHelper helper, XmlDocument? manialinkXml = null)
    {
        ScriptSymbol = scriptSymbol;
        SemanticModel = semanticModel;
        Writer = writer;
        Helper = helper;
        ManialinkXml = manialinkXml;
    }

    public ManiaScriptHead AnalyzeAndBuild() => new()
    {
        Context = BuildContext(),
        AdditionalConsts = BuildAdditionalConsts(),
        Structs = BuildStructs(),
        Includes = BuildIncludes(),
        Consts = BuildConsts(),
        Settings = BuildSettings(),
        Globals = BuildGlobals(),
        Netwrites = BuildNetwrites(),
        Netreads = BuildNetreads(),
        Locals = BuildLocals(),
        Bindings = BuildBindings()
    };

    private INamedTypeSymbol BuildContext()
    {
        if (ScriptSymbol.BaseType is null)
        {
            throw new Exception("Context script requires a specific class context.");
        }
        
        if (ManialinkXml is not null)
        {
            return ScriptSymbol.BaseType;
        }
        
        var isOfficialSymbol = ScriptSymbol.BaseType.ContainingNamespace.ToDisplayString() == "ManiaScriptSharp";

        if (isOfficialSymbol)
        {
            Writer.Write("#RequireContext ");
            Writer.WriteLine(ScriptSymbol.BaseType.Name);
        }
        else
        {
            var filePath = string.Join("/", ManiaScriptGenerator.CreateFilePathFromScriptSymbolInReverse(ScriptSymbol.BaseType, isEmbeddedScript: false, Helper.RootNamespace).Reverse().Skip(1));

            Writer.Write("#Extends \"");
            Writer.Write(filePath);
            Writer.WriteLine("\"");
        }

        Writer.WriteLine();
        
        return ScriptSymbol.BaseType;
    }

    private ImmutableArray<IPropertySymbol> BuildAdditionalConsts()
    {
        if (ManialinkXml is not null) // Additional consts currently apply only for regular scripts
        {
            return ImmutableArray<IPropertySymbol>.Empty;
        }
        
        var modeInterface = ScriptSymbol.Interfaces.FirstOrDefault(x => x.Name == "IMode");

        if (modeInterface is null)
        {
            return ImmutableArray<IPropertySymbol>.Empty;
        }

        Writer.Write("#Const ScriptName \"");
        Writer.Write(ScriptSymbol.Name);
        Writer.WriteLine(".Script.txt\"");

        additionalConsts = WriteAdditionalConsts(modeInterface).ToImmutableArray();

        Writer.WriteLine();
        
        return additionalConsts;
    }

    private IEnumerable<IPropertySymbol> WriteAdditionalConsts(INamedTypeSymbol modeInterface)
    {
        foreach (var interfaceMember in modeInterface.GetMembers().OfType<IPropertySymbol>())
        {
            var member = ScriptSymbol.GetMembers(interfaceMember.Name)
                .OfType<IPropertySymbol>()
                .FirstOrDefault();

            if (member?.DeclaringSyntaxReferences[0].GetSyntax() is not PropertyDeclarationSyntax syntax)
            {
                continue;
            }

            var expression = default(ExpressionSyntax);

            if (syntax.Initializer is not null)
            {
                expression = syntax.Initializer.Value;
            }
            else if (syntax.ExpressionBody is not null)
            {
                expression = syntax.ExpressionBody.Expression;
            }

            if (expression is null)
            {
                continue;
            }

            Writer.Write("#Const ");
            Writer.Write(interfaceMember.Name);
            Writer.Write(' ');

            if (expression is LiteralExpressionSyntax literal)
            {
                switch (literal.Token.Value)
                {
                    case null:
                        Writer.Write("Null");
                        break;
                    case string str:
                        Writer.Write($"\"{str}\"");
                        break;
                    default:
                        Writer.Write(literal.Token.Value);
                        break;
                }
            }
            else if (expression is InvocationExpressionSyntax invocation)
            {
                var symbol = SemanticModel.GetSymbolInfo(invocation).Symbol;
                
                if (symbol is not IMethodSymbol method)
                {
                    continue;
                }
                
                if (method.Name == "Create" && method.ContainingType.Name == "ImmutableArray")
                {
                    Writer.Write('[');

                    for (var i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
                    {
                        var arg = invocation.ArgumentList.Arguments[i];
                        
                        if (arg.Expression is not LiteralExpressionSyntax l)
                        {
                            continue;
                        }
                        
                        if (i != 0)
                        {
                            Writer.Write(", ");
                        }

                        switch (l.Token.Value)
                        {
                            case null:
                                Writer.Write("Null");
                                break;
                            case string str:
                                Writer.Write($"\"{str}\"");
                                break;
                            default:
                                Writer.Write(l.Token.Value);
                                break;
                        }
                    }

                    Writer.Write(']');
                }
            }

            Writer.WriteLine();

            yield return member;
        }
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
        foreach (var libName in new[] {"AnimLib", "ColorLib", "MapUnits", "MathLib", "TextLib", "TimeLib" })
        {
            Writer.Write("#Include \"");
            Writer.Write(libName);
            Writer.Write("\" as ");
            Writer.WriteLine(libName);
        }

        Writer.WriteLine();

        return ImmutableArray<INamedTypeSymbol>.Empty;
    }

    private ImmutableArray<IFieldSymbol> BuildConsts()
    {
        var consts = ScriptSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(x => x.IsConst);
        
        var correctConsts = WriteConsts(consts).ToImmutableArray();

        if (correctConsts.Length == 0)
        {
            Writer.WriteLine("// No consts");
        }
        
        Writer.WriteLine();

        return correctConsts;
    }

    private IEnumerable<IFieldSymbol> WriteConsts(IEnumerable<IFieldSymbol> consts)
    {
        foreach (var constSymbol in consts.Where(x =>
                     !x.GetAttributes().Any(y => y.AttributeClass?.Name == "SettingAttribute")))
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
            
            yield return constSymbol;
        }
    }

    private ImmutableArray<IFieldSymbol> BuildSettings()
    {
        var fields = ScriptSymbol.GetMembers()
            .OfType<IFieldSymbol>();
        
        var settings = WriteSettings(fields).ToImmutableArray();

        if (settings.Length == 0)
        {
            Writer.WriteLine("// No settings");
        }
        
        Writer.WriteLine();

        return settings;
    }

    private IEnumerable<IFieldSymbol> WriteSettings(IEnumerable<IFieldSymbol> fields)
    {
        foreach (var fieldSymbol in fields)
        {
            var settingAttribute = fieldSymbol.GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.SettingAttribute);

            if (settingAttribute is null)
            {
                continue;
            }

            Writer.Write("#Setting ");
            Writer.Write(Standardizer.StandardizeSettingName(fieldSymbol.Name));
            Writer.Write(' ');

            var value = fieldSymbol.IsConst
                ? fieldSymbol.ConstantValue
                : (fieldSymbol.DeclaringSyntaxReferences[0].GetSyntax() as VariableDeclaratorSyntax)?.Initializer?.Value.ToString();

            var isStr = value is string;

            if (fieldSymbol.IsConst && isStr)
            {
                Writer.Write('"');
            }

            Writer.Write(Standardizer.StandardizeName(value?.ToString() ?? ""));

            if (fieldSymbol.IsConst && isStr)
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
                        translated = (bool) namedArg.Value.Value!;
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
                    Writer.Write(')');
                }
            }

            Writer.WriteLine();
            
            yield return fieldSymbol;
        }
    }

    private ImmutableArray<ISymbol> BuildGlobals()
    {
        var globals = WriteGlobals().ToImmutableArray();
        
        if (globals.Length > 0)
        {
            Writer.WriteLine();
        }
        
        return globals;
    }
    
    private IEnumerable<ISymbol> WriteGlobals()
    {
        var globals = ScriptSymbol.GetMembers()
            .Where(x => x.DeclaredAccessibility == Accessibility.Public
                        && (additionalConsts.IsDefaultOrEmpty || !additionalConsts.Contains(x, SymbolEqualityComparer.Default)));
        
        foreach (var memberSymbol in globals)
        {
            string type;
            string name;

            switch (memberSymbol)
            {
                case IFieldSymbol fieldSymbol:
                    if (fieldSymbol.IsConst) continue;
                    type = fieldSymbol.Type is INamedTypeSymbol fieldNamedTypeSymbol
                        ? Standardizer.CSharpTypeToManiaScriptType(fieldNamedTypeSymbol)
                        : Standardizer.CSharpTypeToManiaScriptType(fieldSymbol.Type.Name);
                    name = fieldSymbol.Name;
                    break;
                case IPropertySymbol propertySymbol:

                    // TODO: be more flexible about getters and setters when they are not auto properties

                    type = propertySymbol.Type is INamedTypeSymbol propNamedTypeSymbol
                        ? Standardizer.CSharpTypeToManiaScriptType(propNamedTypeSymbol)
                        : Standardizer.CSharpTypeToManiaScriptType(propertySymbol.Type.Name);
                    name = propertySymbol.Name;
                    break;
                default:
                    continue;
            }

            if (memberSymbol.GetAttributes().Any(x => x.AttributeClass?.Name
                is NameConsts.ManialinkControlAttribute
                or NameConsts.SettingAttribute
                or NameConsts.NetwriteAttribute
                or NameConsts.NetreadAttribute
                or NameConsts.LocalAttribute))
            {
                continue;
            }

            Writer.Write("declare ");
            Writer.Write(type);
            Writer.Write(' ');
            Writer.Write(Standardizer.StandardizeGlobalName(name));
            Writer.WriteLine(";");

            yield return memberSymbol;
        }
    }

    private Dictionary<string, bool> GetNetOverwrites(ImmutableArray<IPropertySymbol> netVariables)
    {
        var typeToScan = ScriptSymbol.BaseType;
        var names = netVariables.Select(x => x.Name).ToImmutableHashSet();
        var netOverwrites = new Dictionary<string, bool>();

        while (typeToScan is not null)
        {
            if (typeToScan.ContainingNamespace?.Name == "ManiaScriptSharp")
            {
                foreach (var prop in typeToScan.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(x => names.Contains(x.Name)))
                {
                    netOverwrites[prop.Name] = prop.IsReadOnly;
                }
            }

            typeToScan = typeToScan.BaseType;
        }

        return netOverwrites;
    }

    private ImmutableArray<IPropertySymbol> BuildNetwrites()
    {
        var netwrites = ScriptSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(x => x.GetAttributes().Any(y => y.AttributeClass?.Name == NameConsts.NetwriteAttribute))
            .ToImmutableArray();

        var netOverwrites = GetNetOverwrites(netwrites);

        foreach (var netwrite in netwrites)
        {
            var isNetOverwrite = netOverwrites.TryGetValue(netwrite.Name, out var isReadOnly);

            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(netwrite.Type));
            Writer.Write(" Get");
            Writer.Write(Standardizer.StandardizeName(netwrite.Name));
            Writer.WriteLine("() {");
            DeclareNetwrite(indent: 1, netwrite);

            if (isNetOverwrite)
            {
                Writer.Write(indent: 1, "Net_");
                Writer.Write(netwrite.Name);
                Writer.Write(" = ");
                Writer.Write(netwrite.Name);
                Writer.WriteLine(';');
            }

            Writer.Write(indent: 1, "return Net_");
            Writer.Write(Standardizer.StandardizeName(netwrite.Name));
            Writer.WriteLine(";");
            Writer.WriteLine("}");
            Writer.WriteLine();

            Writer.Write("Void Set");
            Writer.Write(Standardizer.StandardizeName(netwrite.Name));
            Writer.Write('(');
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(netwrite.Type));
            Writer.WriteLine(" _Value) {");
            DeclareNetwrite(indent: 1, netwrite);

            if (isNetOverwrite && !isReadOnly)
            {
                Writer.Write(indent: 1, netwrite.Name);
                Writer.WriteLine(" = _Value;");
            }

            Writer.Write(indent: 1, "Net_");
            Writer.Write(Standardizer.StandardizeName(netwrite.Name));
            Writer.WriteLine(" = _Value;");
            Writer.WriteLine("}");
            Writer.WriteLine();

            if (netwrite.Type.Name is "Dictionary" or "IList" or "ImmutableArray" or "ICollection")
            {
                Writer.Write("Void Clear");
                Writer.Write(Standardizer.StandardizeName(netwrite.Name));
                Writer.WriteLine("() {");
                DeclareNetwrite(indent: 1, netwrite);
                Writer.Write(indent: 1, "Net_");
                Writer.Write(Standardizer.StandardizeName(netwrite.Name));
                Writer.WriteLine(".clear();");
                Writer.WriteLine("}");
                Writer.WriteLine();
            }
        }

        return netwrites;

        void DeclareNetwrite(int indent, IPropertySymbol netwrite)
        {
            Writer.Write(indent, "declare netwrite ");
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(netwrite.Type));
            Writer.Write(" Net_");
            Writer.Write(Standardizer.StandardizeName(netwrite.Name));
            Writer.WriteLine(" for Teams[0];");
        }
    }

    private ImmutableArray<IPropertySymbol> BuildNetreads()
    {
        var netreads = ScriptSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(x => x.GetAttributes().Any(y => y.AttributeClass?.Name == NameConsts.NetreadAttribute))
            .ToImmutableArray();

        foreach (var netread in netreads)
        {
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(netread.Type));
            Writer.Write(" Get");
            Writer.Write(Standardizer.StandardizeName(netread.Name));
            Writer.WriteLine("() {");
            DeclareNetread(indent: 1, netread);
            Writer.Write(indent: 1, "return Net_");
            Writer.Write(Standardizer.StandardizeName(netread.Name));
            Writer.WriteLine(";");
            Writer.WriteLine("}");
            Writer.WriteLine();
        }

        return netreads;

        void DeclareNetread(int indent, IPropertySymbol netwrite)
        {
            Writer.Write(indent, "declare netread ");
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(netwrite.Type));
            Writer.Write(" Net_");
            Writer.Write(Standardizer.StandardizeName(netwrite.Name));
            Writer.WriteLine(" for Teams[0];");
        }
    }

    private ImmutableArray<IPropertySymbol> BuildLocals()
    {
        var locals = ScriptSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(x => x.GetAttributes().Any(y => y.AttributeClass?.Name == NameConsts.LocalAttribute))
            .ToImmutableArray();

        foreach (var local in locals)
        {
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(local.Type));
            Writer.Write(" Get");
            Writer.Write(Standardizer.StandardizeName(local.Name));
            Writer.WriteLine("() {");
            DeclareLocal(indent: 1, local);

            Writer.Write(indent: 1, "return ");
            Writer.Write(Standardizer.StandardizeName(local.Name));
            Writer.WriteLine(";");
            Writer.WriteLine("}");
            Writer.WriteLine();

            Writer.Write("Void Set");
            Writer.Write(Standardizer.StandardizeName(local.Name));
            Writer.Write('(');
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(local.Type));
            Writer.WriteLine(" _Value) {");
            DeclareLocal(indent: 1, local);

            Writer.Write(indent: 1, Standardizer.StandardizeName(local.Name));
            Writer.WriteLine(" = _Value;");
            Writer.WriteLine("}");
            Writer.WriteLine();
        }

        void DeclareLocal(int indent, IPropertySymbol local)
        {
            Writer.Write(indent, "declare ");
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(local.Type));
            Writer.Write(' ');
            Writer.Write(Standardizer.StandardizeName(local.Name));
            Writer.WriteLine(" for This;");
        }

        return locals;
    }

    private ImmutableArray<ISymbol> BuildBindings()
    {
        if (ManialinkXml is null) // Bindings are only supported for manialink scripts currently
        {
            return ImmutableArray<ISymbol>.Empty;
        }
        
        var bindings = ScriptSymbol.GetMembers()
            .Where(x => (
                (x is IPropertySymbol prop && prop.Type.IsSubclassOf(y => y.Name == NameConsts.CMlControl)) ||
                (x is IFieldSymbol field && field.Type.IsSubclassOf(y => y.Name == NameConsts.CMlControl)))
                && x.GetAttributes().Any(y => y.AttributeClass?.Name == NameConsts.ManialinkControlAttribute)
            ).ToImmutableArray();

        foreach (var binding in bindings)
        {
            var bindingAttribute = binding.GetAttributes()
                .First(x => x.AttributeClass?.Name == NameConsts.ManialinkControlAttribute);

            var boundIdName = bindingAttribute.ConstructorArguments.Length == 0
                ? binding.Name
                : bindingAttribute.ConstructorArguments[0].Value?.ToString() ?? binding.Name;

            var pageFirstChild = ManialinkXml.SelectSingleNode($"descendant::node()[@id='{boundIdName}']");

            if (pageFirstChild is null)
            {
                var descriptorError = new DiagnosticDescriptor(
                    "MSSG003", 
                    "Manialink XML Page.GetFirstChild() validation",
                    $"Could not find control with ID '{boundIdName}'", 
                    "Manialink",  
                    DiagnosticSeverity.Error,
                    true);

                var linePosition = new LinePosition();

                var location = Location.Create($"{ScriptSymbol.Name}.xml", new(), new(linePosition, linePosition));

                Helper.Context.ReportDiagnostic(Diagnostic.Create(descriptorError, location));
            }

            var type = binding switch
            {
                IPropertySymbol prop => prop.Type.Name,
                IFieldSymbol field => field.Type.Name,
                _ => throw new Exception("This should never happen")
            };

            Writer.Write("declare ");
            Writer.Write(type);
            Writer.Write(' ');
            Writer.Write(binding.Name);
            Writer.Write("; // Bound to \"");
            Writer.Write(boundIdName);
            Writer.WriteLine('"');
        }
        
        if (bindings.Length > 0)
        {
            Writer.WriteLine();
        }
        
        return bindings;
    }
}