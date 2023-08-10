using System.Collections.Immutable;
using System.Xml;
using ManiaScriptSharp.Generator.Expressions;
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

        if (!isOfficialSymbol)
        {
            throw new NotSupportedException("Currently only official classes are supported for context");
        }
        
        Writer.Write("#RequireContext ");
        Writer.WriteLine(ScriptSymbol.BaseType.Name);
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
        // TODO: scan through all the syntax trees and find referenced types that are part of the same namespace
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
        foreach (var constSymbol in fields)
        {
            var settingAttribute = constSymbol.GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == NameConsts.SettingAttribute);

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
            
            yield return constSymbol;
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

            if (memberSymbol.GetAttributes().Any(x => x.AttributeClass?.Name is NameConsts.ManialinkControlAttribute or NameConsts.SettingAttribute))
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